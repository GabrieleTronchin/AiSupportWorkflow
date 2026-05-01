namespace AiSupportWorkflow.Presentation.Endpoints;

using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.Presentation.Endpoints.Primitives;
using Microsoft.EntityFrameworkCore;

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

        group.MapGet("/events", async (WorkflowDbContext dbContext, int? limit) =>
        {
            var maxLimit = Math.Min(limit ?? 200, 200);
            var events = await dbContext.Events
                .AsNoTracking()
                .OrderByDescending(e => e.Timestamp)
                .Take(maxLimit)
                .Select(e => new
                {
                    e.Id,
                    e.IssueId,
                    PreviousStage = e.PreviousStage != null ? e.PreviousStage.ToString() : null,
                    NewStage = e.NewStage.ToString(),
                    e.Timestamp,
                    e.Detail,
                })
                .ToListAsync();

            return Results.Ok(events);
        }).WithSummary("List persistent state transition events (max 200)");
    }
}
