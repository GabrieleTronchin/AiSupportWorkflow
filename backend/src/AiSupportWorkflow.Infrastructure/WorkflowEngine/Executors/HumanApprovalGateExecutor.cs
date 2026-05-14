namespace AiSupportWorkflow.Infrastructure.WorkflowEngine.Executors;

using System.Collections.Concurrent;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Agents.AI.Workflows;

internal sealed partial class HumanApprovalGateExecutor(
    IWorkflowStateTracker stateTracker) : Executor("HumanApprovalGateExecutor")
{
    private readonly ConcurrentDictionary<Guid, PendingApproval> _pendingApprovals = new();

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) =>
        protocolBuilder.AddClassAttributeTypes(GetType()).YieldsOutput<WorkflowResult>();

    [MessageHandler]
    private async ValueTask<ApprovalDecision> HandleAsync(
        ResolutionReport report, IWorkflowContext context, CancellationToken ct)
    {
        var issueId = report.IssueId;

        await stateTracker.TransitionAsync(issueId, WorkflowStage.AwaitingApproval, report.ProposedFixSummary);

        var tcs = new TaskCompletionSource<ApprovalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new PendingApproval(issueId, report, tcs);
        _pendingApprovals[issueId] = pending;

        try
        {
            // Register cancellation to unblock if the workflow is cancelled
            await using var registration = ct.Register(() => tcs.TrySetCanceled(ct));

            var decision = await tcs.Task;

            if (!decision.Approved)
            {
                await stateTracker.TransitionAsync(issueId, WorkflowStage.ManualReviewRequired, decision.Reason);
                await context.YieldOutputAsync(WorkflowResult.Failed(issueId, decision.Reason ?? "Rejected"), ct);
            }

            return decision;
        }
        finally
        {
            _pendingApprovals.TryRemove(issueId, out _);
        }
    }

    /// <summary>
    /// Completes the pending approval for the given issue, unblocking the workflow.
    /// </summary>
    public bool TryCompleteApproval(Guid issueId, ApprovalDecision decision)
    {
        if (_pendingApprovals.TryGetValue(issueId, out var pending))
        {
            return pending.CompletionSource.TrySetResult(decision);
        }

        return false;
    }

    /// <summary>
    /// Gets all currently pending approvals.
    /// </summary>
    public IReadOnlyList<PendingApprovalInfo> GetPendingApprovals()
    {
        return _pendingApprovals.Values
            .Select(p => new PendingApprovalInfo(p.IssueId, p.Report))
            .ToList();
    }

    /// <summary>
    /// Checks whether a specific issue is currently awaiting approval.
    /// </summary>
    public bool IsAwaitingApproval(Guid issueId) => _pendingApprovals.ContainsKey(issueId);

    internal sealed record PendingApproval(
        Guid IssueId,
        ResolutionReport Report,
        TaskCompletionSource<ApprovalDecision> CompletionSource);
}

public record PendingApprovalInfo(Guid IssueId, ResolutionReport Report);
