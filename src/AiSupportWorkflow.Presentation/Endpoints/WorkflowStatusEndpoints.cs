namespace AiSupportWorkflow.Presentation.Endpoints;

using AiSupportWorkflow.Domain.Interfaces;

public static class WorkflowStatusEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowStatusEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/support/issues/{id:guid}", (Guid id, IWorkflowStateTracker stateTracker) =>
        {
            var state = stateTracker.GetState(id);
            return Results.Ok(state);
        });

        routes.MapGet("/api/support/issues", (IWorkflowStateTracker stateTracker) =>
        {
            var states = stateTracker.GetAllStates();
            return Results.Ok(states);
        });

        return routes;
    }
}
