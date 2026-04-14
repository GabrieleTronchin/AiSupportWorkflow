namespace AiSupportWorkflow.PropertyTests;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

public class TimeoutProperties
{
    // Feature: developer-experience-improvements, Property 1: Configurable timeout respects fallback rule
    // **Validates: Requirements 1.3, 1.6**
    [Property(MaxTest = 100)]
    public Property ConfigurableTimeout_RespectsPositiveValueOrFallsBackTo120(int timeoutSeconds)
    {
        var capturedTimeout = TimeSpan.Zero;

        var emailProcessor = Substitute.For<IEmailProcessor>();
        var issueClassifier = Substitute.For<IIssueClassifier>();
        var teamRouter = Substitute.For<ITeamRouter>();
        var agentSelector = Substitute.For<IAgentSelector>();
        var codeChangeGenerator = Substitute.For<ICodeChangeGenerator>();
        var stateTracker = Substitute.For<IWorkflowStateTracker>();
        var supervisorBridge = Substitute.For<ISupervisorActorBridge>();

        var issue = new IssueRecord(Guid.NewGuid(), "user@test.com", "Bug in ApplicationA",
            "The /orders endpoint returns 500 in ApplicationA", DateTimeOffset.UtcNow);

        emailProcessor.Process(Arg.Any<IncomingEmail>())
            .Returns(Result<IssueRecord>.Success(issue));
        issueClassifier.ClassifyAsync(Arg.Any<IssueRecord>(), Arg.Any<CancellationToken>())
            .Returns(new ClassificationResult(true, IssueCategory.BackendBug, 0.9, "Backend issue"));
        teamRouter.Route(Arg.Any<IssueRecord>(), Arg.Any<ClassificationResult>())
            .Returns(Result<TeamAssignment>.Success(new TeamAssignment("TeamA", "ApplicationA")));
        agentSelector.Select(Arg.Any<TeamAssignment>(), Arg.Any<IssueCategory>())
            .Returns(new AgentAssignment("TeamA_BackendDeveloper", "TeamA", AgentRole.BackendDeveloper));

        var resolution = new ResolutionReport(issue.Id, "Root cause", "Component", "High", "Fix it", false, null);
        supervisorBridge.AssignIssueAsync(
            Arg.Any<string>(), Arg.Any<IssueRecord>(), Arg.Any<IssueCategory>(),
            Arg.Do<TimeSpan>(t => capturedTimeout = t), Arg.Any<CancellationToken>())
            .Returns(resolution);

        var pr = new PullRequest(Guid.NewGuid(), issue.Id, "Fix", "Description", ["file.cs"], "diff");
        codeChangeGenerator.GenerateAsync(Arg.Any<ResolutionReport>(), Arg.Any<CancellationToken>())
            .Returns(pr);

        var config = Options.Create(new WorkflowConfiguration
        {
            EnableVisualization = false,
            ActorAskTimeoutSeconds = timeoutSeconds
        });

        var sut = new Orchestrator(
            emailProcessor, issueClassifier, teamRouter, agentSelector,
            codeChangeGenerator, stateTracker, supervisorBridge,
            NullLogger<Orchestrator>.Instance, config);

        sut.ProcessIssueAsync(new IncomingEmail("user@test.com", "Bug in ApplicationA",
            "The /orders endpoint returns 500 in ApplicationA")).GetAwaiter().GetResult();

        var expectedSeconds = timeoutSeconds > 0 ? timeoutSeconds : 120;
        var expected = TimeSpan.FromSeconds(expectedSeconds);

        return (capturedTimeout == expected)
            .ToProperty()
            .Label($"timeout={timeoutSeconds}s → expected {expected}, got {capturedTimeout}");
    }
}
