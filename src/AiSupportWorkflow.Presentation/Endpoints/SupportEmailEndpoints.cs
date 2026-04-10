namespace AiSupportWorkflow.Presentation.Endpoints;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Presentation.Endpoints.Primitives;

public class SupportEmailEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support").WithTags("Support Emails");

        group.MapPost("/emails", async (IncomingEmail email, IOrchestrator orchestrator, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(email.Subject) || string.IsNullOrWhiteSpace(email.Body))
                return Results.BadRequest(new { Error = "Subject and Body are required." });

            var result = await orchestrator.ProcessIssueAsync(email, ct);

            return result.IsSuccess
                ? Results.Ok(result)
                : Results.BadRequest(new { result.FailureReason });
        });
    }
}
