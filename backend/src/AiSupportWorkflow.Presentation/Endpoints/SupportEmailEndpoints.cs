namespace AiSupportWorkflow.Presentation.Endpoints;

using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Presentation.Endpoints.Primitives;

public class SupportEmailEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support").WithTags("Support Emails");

        group.MapPost("/emails", async (IncomingEmail email, InboxService inboxService, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(email.Subject) || string.IsNullOrWhiteSpace(email.Body))
                return Results.BadRequest(new { Error = "Subject and Body are required." });

            var messageId = await inboxService.SubmitEmailAsync(email, ct);

            return Results.Accepted($"/api/support/inbox/{messageId}", new { MessageId = messageId });
        }).WithSummary("Submit a support email (async — saved to inbox, returns 202)");
    }
}
