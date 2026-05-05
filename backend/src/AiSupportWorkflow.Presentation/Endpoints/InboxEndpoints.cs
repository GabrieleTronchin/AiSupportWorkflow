namespace AiSupportWorkflow.Presentation.Endpoints;

using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Presentation.Endpoints.Primitives;

public class InboxEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support").WithTags("Inbox");

        group.MapGet("/inbox", async (InboxService inboxService, string? status, CancellationToken ct) =>
        {
            var messages = await inboxService.GetMessagesAsync(status, ct);
            return Results.Ok(messages);
        }).WithSummary("List inbox messages with optional status filter");
    }
}
