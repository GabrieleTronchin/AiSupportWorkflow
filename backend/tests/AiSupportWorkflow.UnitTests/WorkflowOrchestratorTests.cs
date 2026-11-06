namespace AiSupportWorkflow.UnitTests;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Infrastructure.WorkflowEngine;
using AiSupportWorkflow.Infrastructure.WorkflowEngine.Executors;
using AiSupportWorkflow.UnitTests.Helpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

/// <summary>
/// Runs the real Agent Framework workflow end-to-end with a fake LLM client.
/// </summary>
public class WorkflowOrchestratorTests
{
    private readonly IWorkflowStateTracker _stateTracker = Substitute.For<IWorkflowStateTracker>();
    private HumanApprovalGateExecutor _approvalGate = null!;

    private const string CodeRelatedClassification =
        """{"IsCodeRelated":true,"Category":"BackendBug","ConfidenceScore":0.9,"Reasoning":"Backend crash"}""";

    private WorkflowOrchestrator CreateSut(IChatClient chatClient)
    {
        var options = Options.Create(new WorkflowConfiguration
        {
            Teams =
            [
                new TeamConfiguration
                {
                    TeamName = "TeamA",
                    ApplicationName = "ApplicationA",
                    Agents = [new AgentRoleConfiguration { Role = AgentRole.BackendDeveloper, Persona = "Backend dev" }],
                },
            ],
        });

        _approvalGate = new HumanApprovalGateExecutor(_stateTracker);
        var factory = new SupportWorkflowFactory(
            new ClassificationExecutor(chatClient, _stateTracker),
            new TeamAssignmentExecutor(new TeamRouter(options), _stateTracker),
            new AgentAssignmentExecutor(new AgentSelector(options), _stateTracker),
            new ResolutionExecutor(chatClient, _stateTracker),
            _approvalGate,
            new CodeGenerationExecutor(chatClient, _stateTracker));

        return new WorkflowOrchestrator(factory, new EmailProcessor(), _stateTracker, NullLogger<WorkflowOrchestrator>.Instance);
    }

    /// <summary>Answers each pipeline LLM call based on the prompt shape used by the executors.</summary>
    private static FakeChatClient PipelineChatClient() => new(prompt =>
    {
        if (prompt.StartsWith("Agent:", StringComparison.Ordinal))
        {
            return """{"IssueId":"00000000-0000-0000-0000-000000000000","RootCauseDescription":"Items is null","AffectedComponent":"OrderController","SeverityAssessment":"High","ProposedFixSummary":"Initialize Items","RequiresEscalation":false,"EscalationReason":null}""";
        }

        if (prompt.StartsWith("Issue ID:", StringComparison.Ordinal))
        {
            return """{"Id":"11111111-1111-1111-1111-111111111111","IssueId":"00000000-0000-0000-0000-000000000000","Title":"Fix null Items","Description":"Initialize Items","AffectedFilePaths":["src/OrderController.cs"],"SimulatedDiff":"+ Items = [];"}""";
        }

        return CodeRelatedClassification;
    });

    private async Task ApproveWhenPendingAsync(bool approve, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var pending = _approvalGate.GetPendingApprovals();
            if (pending.Count > 0)
            {
                _approvalGate.TryCompleteApproval(pending[0].IssueId, new(approve, approve ? null : "Not acceptable"));
                return;
            }

            await Task.Delay(20, ct);
        }
    }

    [Fact]
    public async Task ProcessIssueAsync_ApprovedIssue_CompletesWithPullRequest()
    {
        // Arrange
        var sut = CreateSut(PipelineChatClient());
        var email = new IncomingEmail("dev@company.com", "NullReferenceException - Application A", "Crash in OrderController");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        // Act
        var processing = sut.ProcessIssueAsync(email, cts.Token);
        await ApproveWhenPendingAsync(approve: true, cts.Token);
        var result = await processing;

        // Assert
        Assert.True(result.IsSuccess, result.FailureReason);
        Assert.NotNull(result.PullRequest);
        Assert.Equal("Fix null Items", result.PullRequest!.Title);
        await _stateTracker.Received(1).TransitionAsync(result.IssueId, WorkflowStage.CodeChangeGenerated, Arg.Any<string?>());
        await _stateTracker.DidNotReceive().TransitionAsync(Arg.Any<Guid>(), WorkflowStage.Failed, Arg.Any<string?>());
    }

    [Fact]
    public async Task ProcessIssueAsync_RejectedIssue_ReturnsFailedWithManualReview()
    {
        // Arrange
        var sut = CreateSut(PipelineChatClient());
        var email = new IncomingEmail("dev@company.com", "NullReferenceException - Application A", "Crash in OrderController");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        // Act
        var processing = sut.ProcessIssueAsync(email, cts.Token);
        await ApproveWhenPendingAsync(approve: false, cts.Token);
        var result = await processing;

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Not acceptable", result.FailureReason);
        await _stateTracker.Received(1).TransitionAsync(result.IssueId, WorkflowStage.ManualReviewRequired, "Not acceptable");
        await _stateTracker.DidNotReceive().TransitionAsync(Arg.Any<Guid>(), WorkflowStage.Failed, Arg.Any<string?>());
    }

    [Fact]
    public async Task ProcessIssueAsync_LlmCallFails_TransitionsIssueToFailedWithErrorDetail()
    {
        // Arrange — e.g. OpenAI returning 429 insufficient_quota
        var sut = CreateSut(new FakeChatClient(new InvalidOperationException("HTTP 429 (insufficient_quota)")));
        var email = new IncomingEmail("dev@company.com", "NullReferenceException - Application A", "Crash in OrderController");

        // Act
        var result = await sut.ProcessIssueAsync(email, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("insufficient_quota", result.FailureReason);
        await _stateTracker.Received(1).TransitionAsync(
            result.IssueId, WorkflowStage.Failed, Arg.Is<string?>(d => d != null && d.Contains("insufficient_quota")));
    }

    [Fact]
    public async Task ProcessIssueAsync_RoutingFails_TransitionsIssueToFailed()
    {
        // Arrange — code-related, but the email mentions neither Application A nor B
        var sut = CreateSut(new FakeChatClient(CodeRelatedClassification));
        var email = new IncomingEmail("dev@company.com", "API crash", "The API crashes on every request");

        // Act
        var result = await sut.ProcessIssueAsync(email, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        await _stateTracker.Received(1).TransitionAsync(result.IssueId, WorkflowStage.Failed, Arg.Any<string?>());
    }

    [Fact]
    public async Task ProcessIssueAsync_OutOfScope_ReturnsOutOfScopeWithoutFailedTransition()
    {
        // Arrange
        const string classification =
            """{"IsCodeRelated":false,"Category":"OutOfScope","ConfidenceScore":0.95,"Reasoning":"Not a code issue"}""";
        var sut = CreateSut(new FakeChatClient(classification));
        var email = new IncomingEmail("hr@company.com", "Lunch", "What time is lunch?");

        // Act
        var result = await sut.ProcessIssueAsync(email, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsOutOfScope, result.FailureReason);
        await _stateTracker.DidNotReceive().TransitionAsync(Arg.Any<Guid>(), WorkflowStage.Failed, Arg.Any<string?>());
    }
}
