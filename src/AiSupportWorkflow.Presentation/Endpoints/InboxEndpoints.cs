namespace AiSupportWorkflow.Presentation.Endpoints;

using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.Presentation.Endpoints.Primitives;
using Microsoft.EntityFrameworkCore;

public class InboxEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support").WithTags("Inbox");

        group.MapGet("/inbox", async (WorkflowDbContext dbContext, string? status) =>
        {
            var query = dbContext.InboxMessages.AsNoTracking().AsQueryable();

            query = status?.ToLowerInvariant() switch
            {
                "queued" => query.Where(m => m.ProcessedAt == null),
                "processed" => query.Where(m => m.ProcessedAt != null && m.Error == null),
                "failed" => query.Where(m => m.Error != null),
                _ => query,
            };

            var messages = await query
                .OrderByDescending(m => m.ReceivedAt)
                .Select(m => new
                {
                    m.Id,
                    m.MessageType,
                    m.ReceivedAt,
                    m.ProcessedAt,
                    m.Error,
                    Status = m.ProcessedAt == null ? "queued"
                        : m.Error != null ? "failed"
                        : "processed",
                })
                .ToListAsync();

            return Results.Ok(messages);
        }).WithSummary("List inbox messages with optional status filter");
    }
}
