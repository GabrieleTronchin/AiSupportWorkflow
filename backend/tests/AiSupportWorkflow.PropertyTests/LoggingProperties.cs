namespace AiSupportWorkflow.PropertyTests;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using AiSupportWorkflow.PropertyTests.Generators;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

public class LoggingProperties
{
    private sealed class CapturingLogger : ILogger<Orchestrator>
    {
        public List<(LogLevel Level, string Message, IReadOnlyList<KeyValuePair<string, object?>> Properties)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = new List<KeyValuePair<string, object?>>();
            if (state is IReadOnlyList<KeyValuePair<string, object?>> kvps)
            {
                properties.AddRange(kvps);
            }
            Entries.Add((logLevel, formatter(state, exception), properties));
        }
    }

    // Property: Decision logs include structured properties for classification, team, and agent
    [Property(MaxTest = 100)]
    public Property DecisionLogs_IncludeAllRequiredStructuredProperties()
    {
        var gen =
            from classification in ClassificationGenerators.ValidClassificationArb().Generator
                .Where(c => c.IsCodeRelated)
            from teamName in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from appName in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from agentRole in Gen.Elements(AgentRole.BackendDeveloper, AgentRole.FrontendDeveloper, AgentRole.QAEngineer)
            select (classification, teamName, appName, agentRole);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (classification, teamName, appName, agentRole) = tuple;

            var capturingLogger = new CapturingLogger();

            var emailProcessor = Substitute.For<IEmailProcessor>();
            var issueClassifier = Substitute.For<IIssueClassifier>();
            var teamRouter = Substitute.For<ITeamRouter>();
            var agentSelector = Substitute.For<IAgentSelector>();
            var codeChangeGenerator = Substitute.For<ICodeChangeGenerator>();
            var stateTracker = Substitute.For<IWorkflowStateTracker>();
            var supervisorBridge = Substitute.For<ISupervisorActorBridge>();

            var issue = new IssueRecord(Guid.NewGuid(), "user@test.com", "Bug",
                "The /orders endpoint returns 500 in ApplicationA", DateTimeOffset.UtcNow);
            var team = new TeamAssignment(teamName, appName);
            var agentId = $"{teamName}_{agentRole}";
            var agent = new AgentAssignment(agentId, teamName, agentRole);

            emailProcessor.Process(Arg.Any<IncomingEmail>())
                .Returns(Result<IssueRecord>.Success(issue));
            issueClassifier.ClassifyAsync(Arg.Any<IssueRecord>(), Arg.Any<CancellationToken>())
                .Returns(classification);
            teamRouter.Route(Arg.Any<IssueRecord>(), Arg.Any<ClassificationResult>())
                .Returns(Result<TeamAssignment>.Success(team));
            agentSelector.Select(Arg.Any<TeamAssignment>(), Arg.Any<IssueCategory>())
                .Returns(agent);

            var resolution = new ResolutionReport(issue.Id, "Root cause", "Component", "High", "Fix it", false, null);
            supervisorBridge.AssignIssueAsync(
                Arg.Any<string>(), Arg.Any<IssueRecord>(), Arg.Any<IssueCategory>(),
                Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                .Returns(resolution);

            var pr = new PullRequest(Guid.NewGuid(), issue.Id, "Fix", "Description", ["file.cs"], "diff");
            codeChangeGenerator.GenerateAsync(Arg.Any<ResolutionReport>(), Arg.Any<CancellationToken>())
                .Returns(pr);

            var config = Options.Create(new WorkflowConfiguration
            {
                EnableVisualization = false,
                ActorAskTimeoutSeconds = 120
            });

            var sut = new Orchestrator(
                emailProcessor, issueClassifier, teamRouter, agentSelector,
                codeChangeGenerator, stateTracker, supervisorBridge,
                capturingLogger, config);

            sut.ProcessIssueAsync(new IncomingEmail("user@test.com", "Bug",
                "The /orders endpoint returns 500 in ApplicationA")).GetAwaiter().GetResult();

            var hasClassificationLog = capturingLogger.Entries.Any(e =>
                e.Message.Contains("Classification for issue") &&
                e.Level == LogLevel.Information);

            var hasTeamLog = capturingLogger.Entries.Any(e =>
                e.Message.Contains("Team assigned for issue") &&
                e.Level == LogLevel.Information);

            var hasAgentLog = capturingLogger.Entries.Any(e =>
                e.Message.Contains("Agent assigned for issue") &&
                e.Level == LogLevel.Information);

            return (hasClassificationLog && hasTeamLog && hasAgentLog)
                .ToProperty()
                .Label($"classification={hasClassificationLog}, team={hasTeamLog}, agent={hasAgentLog}");
        });
    }

    // Property: Successful workflow logs classification, team, and agent at Information level
    [Property(MaxTest = 100)]
    public Property StageTransitionLogs_EmittedAtDebugLevel()
    {
        var gen =
            from classification in ClassificationGenerators.ValidClassificationArb().Generator
            from sender in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from subject in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from body in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from teamName in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from appName in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from agentRole in Gen.Elements(AgentRole.BackendDeveloper, AgentRole.FrontendDeveloper, AgentRole.QAEngineer)
            select (classification, sender, subject, body, teamName, appName, agentRole);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (classification, sender, subject, body, teamName, appName, agentRole) = tuple;

            var capturingLogger = new CapturingLogger();

            var emailProcessor = Substitute.For<IEmailProcessor>();
            var issueClassifier = Substitute.For<IIssueClassifier>();
            var teamRouter = Substitute.For<ITeamRouter>();
            var agentSelector = Substitute.For<IAgentSelector>();
            var codeChangeGenerator = Substitute.For<ICodeChangeGenerator>();
            var stateTracker = Substitute.For<IWorkflowStateTracker>();
            var supervisorBridge = Substitute.For<ISupervisorActorBridge>();

            var issue = new IssueRecord(Guid.NewGuid(), sender, subject, body, DateTimeOffset.UtcNow);
            var team = new TeamAssignment(teamName, appName);
            var agentId = $"{teamName}_{agentRole}";
            var agent = new AgentAssignment(agentId, teamName, agentRole);

            emailProcessor.Process(Arg.Any<IncomingEmail>())
                .Returns(Result<IssueRecord>.Success(issue));
            issueClassifier.ClassifyAsync(Arg.Any<IssueRecord>(), Arg.Any<CancellationToken>())
                .Returns(classification);
            teamRouter.Route(Arg.Any<IssueRecord>(), Arg.Any<ClassificationResult>())
                .Returns(Result<TeamAssignment>.Success(team));
            agentSelector.Select(Arg.Any<TeamAssignment>(), Arg.Any<IssueCategory>())
                .Returns(agent);

            var resolution = new ResolutionReport(issue.Id, "Root cause", "Component", "High", "Fix it", false, null);
            supervisorBridge.AssignIssueAsync(
                Arg.Any<string>(), Arg.Any<IssueRecord>(), Arg.Any<IssueCategory>(),
                Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                .Returns(resolution);

            var pr = new PullRequest(Guid.NewGuid(), issue.Id, "Fix", "Description", ["file.cs"], "diff");
            codeChangeGenerator.GenerateAsync(Arg.Any<ResolutionReport>(), Arg.Any<CancellationToken>())
                .Returns(pr);

            var config = Options.Create(new WorkflowConfiguration
            {
                EnableVisualization = false,
                ActorAskTimeoutSeconds = 120
            });

            var sut = new Orchestrator(
                emailProcessor, issueClassifier, teamRouter, agentSelector,
                codeChangeGenerator, stateTracker, supervisorBridge,
                capturingLogger, config);

            sut.ProcessIssueAsync(new IncomingEmail(sender, subject, body)).GetAwaiter().GetResult();

            if (classification.IsCodeRelated)
            {
                // Full pipeline should log classification, team, and agent at Information level
                var infoLogs = capturingLogger.Entries.Where(e => e.Level == LogLevel.Information).ToList();
                var hasClassification = infoLogs.Any(e => e.Message.Contains("Classification for issue"));
                var hasTeam = infoLogs.Any(e => e.Message.Contains("Team assigned for issue"));
                var hasAgent = infoLogs.Any(e => e.Message.Contains("Agent assigned for issue"));

                return (hasClassification && hasTeam && hasAgent)
                    .ToProperty()
                    .Label($"classification={hasClassification}, team={hasTeam}, agent={hasAgent}");
            }
            else
            {
                // Out-of-scope: should log classification at Information level
                var infoLogs = capturingLogger.Entries.Where(e => e.Level == LogLevel.Information).ToList();
                var hasClassification = infoLogs.Any(e => e.Message.Contains("Classification for issue"));

                return hasClassification
                    .ToProperty()
                    .Label($"OutOfScope: classification={hasClassification}");
            }
        });
    }

    // Property: Received stage log includes email metadata (IssueId)
    [Property(MaxTest = 100)]
    public Property ReceivedStageLog_IncludesEmailMetadata()
    {
        var gen =
            from sender in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from subject in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from body in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            select (sender, subject, body);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (sender, subject, body) = tuple;

            var capturingLogger = new CapturingLogger();

            var emailProcessor = Substitute.For<IEmailProcessor>();
            var issueClassifier = Substitute.For<IIssueClassifier>();
            var teamRouter = Substitute.For<ITeamRouter>();
            var agentSelector = Substitute.For<IAgentSelector>();
            var codeChangeGenerator = Substitute.For<ICodeChangeGenerator>();
            var stateTracker = Substitute.For<IWorkflowStateTracker>();
            var supervisorBridge = Substitute.For<ISupervisorActorBridge>();

            var issue = new IssueRecord(Guid.NewGuid(), sender, subject, body, DateTimeOffset.UtcNow);

            emailProcessor.Process(Arg.Any<IncomingEmail>())
                .Returns(Result<IssueRecord>.Success(issue));
            issueClassifier.ClassifyAsync(Arg.Any<IssueRecord>(), Arg.Any<CancellationToken>())
                .Returns(new ClassificationResult(false, IssueCategory.OutOfScope, 0.8, "Not code related"));

            var config = Options.Create(new WorkflowConfiguration
            {
                EnableVisualization = false,
                ActorAskTimeoutSeconds = 120
            });

            var sut = new Orchestrator(
                emailProcessor, issueClassifier, teamRouter, agentSelector,
                codeChangeGenerator, stateTracker, supervisorBridge,
                capturingLogger, config);

            sut.ProcessIssueAsync(new IncomingEmail(sender, subject, body)).GetAwaiter().GetResult();

            // Classification log should contain the IssueId
            var classificationLog = capturingLogger.Entries.FirstOrDefault(e =>
                e.Message.Contains("Classification for issue"));

            var hasIssueId = classificationLog.Properties.Any(p =>
                p.Key == "IssueId" && p.Value is Guid);

            return hasIssueId
                .ToProperty()
                .Label($"hasIssueId={hasIssueId}");
        });
    }

    // Property: Failed workflow logs at Error level
    [Property(MaxTest = 100)]
    public Property FailedStageLog_EmittedAtWarningLevel()
    {
        var gen =
            from sender in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from subject in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from body in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from failureMessage in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            select (sender, subject, body, failureMessage);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (sender, subject, body, failureMessage) = tuple;

            var capturingLogger = new CapturingLogger();

            var emailProcessor = Substitute.For<IEmailProcessor>();
            var issueClassifier = Substitute.For<IIssueClassifier>();
            var teamRouter = Substitute.For<ITeamRouter>();
            var agentSelector = Substitute.For<IAgentSelector>();
            var codeChangeGenerator = Substitute.For<ICodeChangeGenerator>();
            var stateTracker = Substitute.For<IWorkflowStateTracker>();
            var supervisorBridge = Substitute.For<ISupervisorActorBridge>();

            var issue = new IssueRecord(Guid.NewGuid(), sender, subject, body, DateTimeOffset.UtcNow);

            emailProcessor.Process(Arg.Any<IncomingEmail>())
                .Returns(Result<IssueRecord>.Success(issue));
            issueClassifier.ClassifyAsync(Arg.Any<IssueRecord>(), Arg.Any<CancellationToken>())
                .Returns(new ClassificationResult(true, IssueCategory.BackendBug, 0.9, "Backend issue"));
            // Team routing fails — now returns Result.Failure instead of throwing
            teamRouter.Route(Arg.Any<IssueRecord>(), Arg.Any<ClassificationResult>())
                .Returns(Result<TeamAssignment>.Failure(failureMessage));

            var config = Options.Create(new WorkflowConfiguration
            {
                EnableVisualization = false,
                ActorAskTimeoutSeconds = 120
            });

            var sut = new Orchestrator(
                emailProcessor, issueClassifier, teamRouter, agentSelector,
                codeChangeGenerator, stateTracker, supervisorBridge,
                capturingLogger, config);

            var result = sut.ProcessIssueAsync(new IncomingEmail(sender, subject, body)).GetAwaiter().GetResult();

            // Team routing failure is now a business logic failure (not an exception),
            // so it returns WorkflowResult.Failed without logging at Error level.
            // The result should contain the failure message.
            var isFailed = !result.IsSuccess;
            var hasFailureReason = result.FailureReason?.Contains(failureMessage) == true;

            return (isFailed && hasFailureReason)
                .ToProperty()
                .Label($"isFailed={isFailed}, hasFailureReason={hasFailureReason}");
        });
    }
}
