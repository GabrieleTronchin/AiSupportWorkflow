namespace AiSupportWorkflow.Infrastructure.Services;

using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.Infrastructure.WorkflowEngine.Executors;

internal sealed class WorkflowApprovalService(
    HumanApprovalGateExecutor approvalGate,
    WorkflowCheckpointStore checkpointStore,
    IWorkflowStateTracker stateTracker)
{
    public Task<IReadOnlyList<PendingApprovalInfo>> GetPendingApprovalsAsync()
    {
        var pending = approvalGate.GetPendingApprovals();
        return Task.FromResult(pending);
    }

    public async Task ApproveAsync(Guid issueId, CancellationToken ct = default)
    {
        EnsureAwaitingApproval(issueId);

        var decision = new ApprovalDecision(Approved: true);
        var completed = approvalGate.TryCompleteApproval(issueId, decision);

        if (!completed)
        {
            throw new InvalidOperationException(
                $"Failed to complete approval for issue {issueId}. The workflow may have already been resolved.");
        }

        await checkpointStore.MarkResumedAsync(issueId);
    }

    public async Task RejectAsync(Guid issueId, string? reason, CancellationToken ct = default)
    {
        EnsureAwaitingApproval(issueId);

        var decision = new ApprovalDecision(Approved: false, Reason: reason);
        var completed = approvalGate.TryCompleteApproval(issueId, decision);

        if (!completed)
        {
            throw new InvalidOperationException(
                $"Failed to complete rejection for issue {issueId}. The workflow may have already been resolved.");
        }

        await checkpointStore.MarkResumedAsync(issueId);
    }

    private void EnsureAwaitingApproval(Guid issueId)
    {
        if (!approvalGate.IsAwaitingApproval(issueId))
        {
            var currentState = stateTracker.GetState(issueId);

            if (currentState.Stage == WorkflowStage.AwaitingApproval)
            {
                throw new InvalidOperationException(
                    $"Issue {issueId} is in AwaitingApproval state but no pending gate was found. " +
                    "The workflow may need to be restarted.");
            }

            throw new InvalidOperationException(
                $"Issue {issueId} is not awaiting approval. Current state: {currentState.Stage}.");
        }
    }
}
