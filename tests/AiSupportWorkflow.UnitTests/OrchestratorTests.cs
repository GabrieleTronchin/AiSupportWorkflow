namespace AiSupportWorkflow.UnitTests;

using Akka.Actor;
using Akka.TestKit.Xunit2;
using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.Messages;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

public class OrchestratorTests : TestKit
{
    private readonly IEmailProcessor _emailProcessor = Substitute.For<IEmailProcessor>();
    private readonly IIssueClassifier _issueClassifier = Substitute.For<IIssueClassifier>();
    private readonly ITeamRouter _teamRouter = Substitute.For<ITeamRouter>();
    private readonly IAgentSelector _agentSelector = Substitute.For<IAgentSelector>();
    private readonly ICodeChangeGenerator _codeChangeGenerator = Substitute.For<ICodeChangeGenerator>();
    private readonly IWorkflowStateTracker _stateTracker = Substitute.For<IWorkflowStateTracker>();

    private static IncomingEmail ValidEmail() =>
        new("user@test.com", "Bug in ApplicationA", "The /orders endpoint returns 500 in ApplicationA");

    private static IssueRecord MakeIssue(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "user@test.com", "Bug in ApplicationA",
            "The /orders endpoint returns 500 in ApplicationA", DateTimeOffset.UtcNow);

    private Orchestrator CreateSut()
    {
        var config = Options.Create(new WorkflowConfiguration { EnableVisualization = false });
        return new Orchestrator(
            _emailProcessor, _issueClassifier, _teamRouter, _agentSelector,
            _codeChangeGenerator, _stateTracker, Sys,
            NullLogger<Orchestrator>.Instance, config);
    }

    private void SetupFullPipeline(IssueRecord issue)
    {
        _emailProcessor.Process(Arg.Any<IncomingEmail>())
            .Returns(Result<IssueRecord>.Success(issue));
        _issueClassifier.ClassifyAsync(Arg.Any<IssueRecord>(), Arg.Any<CancellationToken>())
            .Returns(new ClassificationResult(true, IssueCategory.BackendBug, 0.9, "Backend issue"));
        _teamRouter.Route(Arg.Any<IssueRecord>(), Arg.Any<ClassificationResult>())
            .Returns(Result<TeamAssignment>.Success(new TeamAssignment("TeamA", "ApplicationA")));
        _agentSelector.Select(Arg.Any<TeamAssignment>(), Arg.Any<IssueCategory>())
            .Returns(new AgentAssignment("TeamA_BackendDeveloper", "TeamA", AgentRole.BackendDeveloper));

        var resolution = new ResolutionReport(issue.Id, "Root cause", "Component", "High", "Fix it", false, null);
        var pr = new PullRequest(Guid.NewGuid(), issue.Id, "Fix", "Description", ["file.cs"], "diff");
        _codeChangeGenerator.GenerateAsync(Arg.Any<ResolutionReport>(), Arg.Any<CancellationToken>())
            .Returns(pr);

        // Create proper actor hierarchy: /user/supervisor/TeamA_BackendDeveloper
        var supervisorProps = Props.Create(() => new StubSupervisorActor(resolution));
        Sys.ActorOf(supervisorProps, "supervisor");
    }

    [Fact]
    public async Task ProcessIssueAsync_FullPipeline_ReturnsCompleted()
    {
        var issue = MakeIssue();
        SetupFullPipeline(issue);
        var sut = CreateSut();

        var result = await sut.ProcessIssueAsync(ValidEmail());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.PullRequest);
        Assert.False(result.IsOutOfScope);
    }

    [Fact]
    public async Task ProcessIssueAsync_OutOfScopeClassification_ReturnsOutOfScope()
    {
        var issue = MakeIssue();
        _emailProcessor.Process(Arg.Any<IncomingEmail>())
            .Returns(Result<IssueRecord>.Success(issue));
        _issueClassifier.ClassifyAsync(Arg.Any<IssueRecord>(), Arg.Any<CancellationToken>())
            .Returns(new ClassificationResult(false, IssueCategory.OutOfScope, 0.8, "Not code related"));

        var sut = CreateSut();
        var result = await sut.ProcessIssueAsync(ValidEmail());

        Assert.True(result.IsOutOfScope);
    }

    [Fact]
    public async Task ProcessIssueAsync_TeamRoutingFails_ReturnsFailed()
    {
        var issue = MakeIssue();
        _emailProcessor.Process(Arg.Any<IncomingEmail>())
            .Returns(Result<IssueRecord>.Success(issue));
        _issueClassifier.ClassifyAsync(Arg.Any<IssueRecord>(), Arg.Any<CancellationToken>())
            .Returns(new ClassificationResult(true, IssueCategory.BackendBug, 0.9, "Backend"));
        _teamRouter.Route(Arg.Any<IssueRecord>(), Arg.Any<ClassificationResult>())
            .Returns(Result<TeamAssignment>.Failure("Cannot determine application"));

        var sut = CreateSut();
        var result = await sut.ProcessIssueAsync(ValidEmail());

        Assert.False(result.IsSuccess);
        Assert.Contains("routing failed", result.FailureReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessIssueAsync_EmailProcessingFails_ReturnsFailed()
    {
        _emailProcessor.Process(Arg.Any<IncomingEmail>())
            .Returns(Result<IssueRecord>.Failure("Empty subject"));

        var sut = CreateSut();
        var result = await sut.ProcessIssueAsync(ValidEmail());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ProcessIssueAsync_ConcurrentEmails_AllGetUniqueIds()
    {
        var issues = Enumerable.Range(0, 3).Select(_ => MakeIssue()).ToList();
        var callIndex = 0;

        _emailProcessor.Process(Arg.Any<IncomingEmail>())
            .Returns(ci =>
            {
                var idx = Interlocked.Increment(ref callIndex) - 1;
                return Result<IssueRecord>.Success(issues[idx % issues.Count]);
            });
        _issueClassifier.ClassifyAsync(Arg.Any<IssueRecord>(), Arg.Any<CancellationToken>())
            .Returns(new ClassificationResult(false, IssueCategory.OutOfScope, 0.8, "Out of scope"));

        var sut = CreateSut();
        var tasks = Enumerable.Range(0, 3)
            .Select(_ => sut.ProcessIssueAsync(new IncomingEmail("u@t.com", "Bug", "Body")))
            .ToList();

        var results = await Task.WhenAll(tasks);
        var uniqueIds = results.Select(r => r.IssueId).Distinct().ToList();
        Assert.Equal(3, uniqueIds.Count);
    }

    private class StubSupervisorActor : ReceiveActor
    {
        public StubSupervisorActor(ResolutionReport report)
        {
            var agentProps = Props.Create(() => new StubAgentActor(report));
            Context.ActorOf(agentProps, "TeamA_BackendDeveloper");
        }
    }

    private class StubAgentActor : ReceiveActor
    {
        public StubAgentActor(ResolutionReport report)
        {
            Receive<AssignIssueMessage>(_ =>
                Sender.Tell(new ResolutionCompleteMessage(report.IssueId, report)));
        }
    }
}
