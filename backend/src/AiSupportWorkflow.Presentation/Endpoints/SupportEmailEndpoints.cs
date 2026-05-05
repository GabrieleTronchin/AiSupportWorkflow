namespace AiSupportWorkflow.Presentation.Endpoints;

using System.Text.Json;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.Infrastructure.Persistence.Entities;
using AiSupportWorkflow.Presentation.Endpoints.Primitives;

public class SupportEmailEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support").WithTags("Support Emails");

        group.MapPost("/emails", async (IncomingEmail email, WorkflowDbContext dbContext, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(email.Subject) || string.IsNullOrWhiteSpace(email.Body))
                return Results.BadRequest(new { Error = "Subject and Body are required." });

            var message = new InboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = "SupportEmail",
                Payload = JsonSerializer.Serialize(email),
                ReceivedAt = DateTimeOffset.UtcNow,
            };

            dbContext.InboxMessages.Add(message);
            await dbContext.SaveChangesAsync(ct);

            return Results.Accepted($"/api/support/inbox/{message.Id}", new { MessageId = message.Id });
        }).WithSummary("Submit a support email (async — saved to inbox, returns 202)");
    }
}
