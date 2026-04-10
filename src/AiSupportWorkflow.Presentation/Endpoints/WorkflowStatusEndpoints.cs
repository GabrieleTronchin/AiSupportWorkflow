namespace AiSupportWorkflow.Presentation.Endpoints;

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
    }
}
