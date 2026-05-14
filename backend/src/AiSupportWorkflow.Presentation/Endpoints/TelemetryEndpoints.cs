namespace AiSupportWorkflow.Presentation.Endpoints;

using AiSupportWorkflow.Infrastructure.AgentFramework;
using AiSupportWorkflow.Presentation.Endpoints.Primitives;

public class TelemetryEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support").WithTags("Telemetry");

        group.MapGet("/agents/{agentId}/telemetry", (string agentId, LlmTelemetryStore store) =>
        {
            var telemetry = store.GetAgentTelemetry(agentId);
            return Results.Ok(telemetry);
        }).WithSummary("Get agent-specific LLM telemetry");

        group.MapGet("/telemetry/summary", (LlmTelemetryStore store) =>
        {
            var summary = store.GetGlobalSummary();
            return Results.Ok(summary);
        }).WithSummary("Get global LLM usage statistics");
    }
}
