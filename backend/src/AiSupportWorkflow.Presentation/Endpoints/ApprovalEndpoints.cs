namespace AiSupportWorkflow.Presentation.Endpoints;

using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Infrastructure.Services;
using AiSupportWorkflow.Infrastructure.WorkflowEngine;
using AiSupportWorkflow.Presentation.Endpoints.Primitives;

public class ApprovalEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support/approvals").WithTags("Human Approval");

        group.MapGet("/pending", async (WorkflowApprovalService approvalService) =>
        {
            var pending = await approvalService.GetPendingApprovalsAsync();
            return Results.Ok(pending);
        }).WithSummary("List workflows awaiting approval");

        group.MapPost("/{issueId:guid}/approve", async (
            Guid issueId, WorkflowApprovalService approvalService, CancellationToken ct) =>
        {
            try
            {
                await approvalService.ApproveAsync(issueId, ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not awaiting approval"))
            {
                return Results.Conflict(new { Error = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("no pending gate"))
            {
                return Results.NotFound(new { Error = ex.Message });
            }
        }).WithSummary("Approve a workflow");

        group.MapPost("/{issueId:guid}/reject", async (
            Guid issueId, RejectRequest? request, WorkflowApprovalService approvalService, CancellationToken ct) =>
        {
            try
            {
                await approvalService.RejectAsync(issueId, request?.Reason, ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not awaiting approval"))
            {
                return Results.Conflict(new { Error = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("no pending gate"))
            {
                return Results.NotFound(new { Error = ex.Message });
            }
        }).WithSummary("Reject a workflow with optional reason");

        app.MapPost("/api/support/issues/{issueId:guid}/abort", async (
            Guid issueId,
            IWorkflowStateTracker stateTracker,
            WorkflowRunRegistry runRegistry) =>
        {
            // Running workflow: cancel it. The orchestrator records the single Failed transition
            // (this also releases a pending approval gate without a ManualReviewRequired transition).
            if (runRegistry.TryAbort(issueId))
            {
                return Results.Accepted();
            }

            // Not running (e.g. after a restart): only non-terminal issues can be aborted.
            var state = stateTracker.GetState(issueId);
            if (state.LastUpdated == DateTimeOffset.MinValue)
            {
                return Results.NotFound(new { Error = $"Issue {issueId} not found." });
            }

            if (state.IsTerminal)
            {
                return Results.Conflict(new { Error = $"Issue {issueId} has already completed ({state.Stage})." });
            }

            await stateTracker.TransitionAsync(issueId, WorkflowStage.Failed, WorkflowRunRegistry.AbortReason);
            return Results.NoContent();
        }).WithTags("Workflow Control").WithSummary("Abort a running workflow; the issue ends in Failed state");
    }
}

public record RejectRequest(string? Reason);
