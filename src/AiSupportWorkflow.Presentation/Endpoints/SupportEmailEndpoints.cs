namespace AiSupportWorkflow.Presentation.Endpoints;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Interfaces;

public static class SupportEmailEndpoints
{
    public static IEndpointRouteBuilder MapSupportEmailEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/support/emails", async (IncomingEmail email, IOrchestrator orchestrator, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(email.Subject) || string.IsNullOrWhiteSpace(email.Body))
                return Results.BadRequest(new { Error = "Subject and Body are required." });

            var result = await orchestrator.ProcessIssueAsync(email, ct);

            return result.IsSuccess
                ? Results.Ok(result)
                : Results.BadRequest(new { result.FailureReason });
        });

        return routes;
    }
}
