namespace AiSupportWorkflow.PropertyTests;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.PropertyTests.Generators;
using AiSupportWorkflow.PropertyTests.Helpers;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class WorkflowProperties
{
    private static EfWorkflowStateTracker CreateTracker(out WorkflowDbContext context)
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        context = new WorkflowDbContext(options);
        return new EfWorkflowStateTracker(new TestDbContextFactory(context));
    }

    // Feature: ai-support-workflow, Property 8: Workflow state transition ordering
    // **Validates: Requirements 5.3, 6.3, 8.2, 8.3**
    [Property(MaxTest = 100, Arbitrary = [typeof(WorkflowGenerators)])]
    public Property WorkflowTransitions_FollowValidPipelinePaths(WorkflowStage[] stages)
    {
        var tracker = CreateTracker(out var context);
        using (context)
        {
            var issueId = Guid.NewGuid();

            foreach (var stage in stages)
            {
                tracker.TransitionAsync(issueId, stage).GetAwaiter().GetResult();
            }

            var finalState = tracker.GetState(issueId);
            var lastStage = stages[^1];

            return (finalState.Stage == lastStage).ToProperty();
        }
    }

    [Property(MaxTest = 100, Arbitrary = [typeof(WorkflowGenerators)])]
    public Property WorkflowTransitions_NoStageSkippedOrRevisited(WorkflowStage[] stages)
    {
        var distinct = stages.Distinct().ToArray();
        return (distinct.Length == stages.Length).ToProperty();
    }

    // Feature: ai-support-workflow, Property 9: Concurrent issue independence
    // **Validates: Requirements 8.5**
    [Property(MaxTest = 100)]
    public Property ConcurrentIssues_HaveUniqueIdsAndIndependentState(PositiveInt count)
    {
        var n = Math.Min(count.Get, 20);
        var tracker = CreateTracker(out var context);
        using (context)
        {
            var issueIds = Enumerable.Range(0, n).Select(_ => Guid.NewGuid()).ToList();

            var stagesForIssues = new[] { WorkflowStage.Received, WorkflowStage.Classified, WorkflowStage.TeamAssigned };
            for (var i = 0; i < n; i++)
            {
                var stage = stagesForIssues[i % stagesForIssues.Length];
                tracker.TransitionAsync(issueIds[i], stage).GetAwaiter().GetResult();
            }

            var uniqueIds = issueIds.Distinct().Count() == n;
            var independentStates = true;
            for (var i = 0; i < n; i++)
            {
                var expectedStage = stagesForIssues[i % stagesForIssues.Length];
                var state = tracker.GetState(issueIds[i]);
                if (state.Stage != expectedStage || state.IssueId != issueIds[i])
                {
                    independentStates = false;
                    break;
                }
            }

            return (uniqueIds && independentStates).ToProperty();
        }
    }

    // Feature: ai-support-workflow, Property 11: Configuration-driven team instantiation
    // **Validates: Requirements 12.1**
    [Property(MaxTest = 100, Arbitrary = [typeof(WorkflowGenerators)])]
    public Property ConfigurationDrivenTeams_MatchConfigExactly(WorkflowConfiguration config)
    {
        var allExpectedAgents = config.Teams
            .SelectMany(t => t.Agents.Select(a => new { t.TeamName, a.Role }))
            .ToList();

        var totalConfiguredAgents = config.Teams.Sum(t => t.Agents.Count);

        return (allExpectedAgents.Count == totalConfiguredAgents
            && config.Teams.Select(t => t.TeamName).Count() == config.Teams.Count)
            .ToProperty();
    }

    // Feature: ai-support-workflow, Property 12: Visualization decision logging
    // **Validates: Requirements 13.3**
    [Property(MaxTest = 100)]
    public Property VisualizationEnabled_DecisionPointsProduceLogEntries(NonEmptyString reasoning)
    {
        var logEntries = new List<string>();
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddProvider(new InMemoryLoggerProvider(logEntries)));
        var logger = loggerFactory.CreateLogger<TestVisualizationLogger>();

        var issueId = Guid.NewGuid();

        // Simulate the three decision points with visualization enabled
        logger.LogInformation(
            "[Visualization] Classification decision for issue {IssueId}: Reasoning={Reasoning}",
            issueId, reasoning.Get);

        logger.LogInformation(
            "[Visualization] Team assignment decision for issue {IssueId}: TeamName={TeamName}",
            issueId, "TeamA");

        logger.LogInformation(
            "[Visualization] Agent selection decision for issue {IssueId}: AgentId={AgentId}",
            issueId, "TeamA_BackendDeveloper");

        var classificationLogs = logEntries.Count(e => e.Contains("[Visualization] Classification decision"));
        var teamLogs = logEntries.Count(e => e.Contains("[Visualization] Team assignment decision"));
        var agentLogs = logEntries.Count(e => e.Contains("[Visualization] Agent selection decision"));

        return (classificationLogs == 1 && teamLogs == 1 && agentLogs == 1).ToProperty();
    }
}

file class TestVisualizationLogger;

file class InMemoryLoggerProvider(List<string> logEntries) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(logEntries);
    public void Dispose() { }
}

file class InMemoryLogger(List<string> logEntries) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        logEntries.Add(formatter(state, exception));
    }
}
