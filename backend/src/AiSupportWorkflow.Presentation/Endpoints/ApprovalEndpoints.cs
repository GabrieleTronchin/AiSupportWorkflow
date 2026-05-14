namespace AiSupportWorkflow.Presentation.Endpoints;

using AiSupportWorkflow.Infrastructure.Services;
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
    }
}

public record RejectRequest(string? Reason);
