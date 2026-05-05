namespace AiSupportWorkflow.Presentation.Endpoints;

using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Presentation.Endpoints.Primitives;

public class WorkflowStatusEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support").WithTags("Workflow Status");

        group.MapGet("/issues/{id:guid}", (Guid id, IWorkflowStateTracker stateTracker) =>
        {
            var state = stateTracker.GetState(id);
            return Results.Ok(state);
        });

        group.MapGet("/issues", (IWorkflowStateTracker stateTracker) =>
        {
            var states = stateTracker.GetAllStates();
            return Results.Ok(states);
        });

        group.MapGet("/events", async (WorkflowQueryService queryService, int? limit, CancellationToken ct) =>
        {
            var events = await queryService.GetEventsAsync(limit, ct);
            return Results.Ok(events);
        }).WithSummary("List persistent state transition events (max 200)");
    }
}
