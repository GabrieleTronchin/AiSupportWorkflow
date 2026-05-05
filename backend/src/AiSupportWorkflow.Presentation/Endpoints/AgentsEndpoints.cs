namespace AiSupportWorkflow.Presentation.Endpoints;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Presentation.Endpoints.Primitives;
using Microsoft.Extensions.Options;

public class AgentsEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support").WithTags("Agents");

        group.MapGet("/agents", async (
            AgentStatusService agentStatusService,
            IOptions<WorkflowConfiguration> config,
            CancellationToken ct) =>
        {
            if (!config.Value.EnableVisualization)
                return Results.NotFound(new { Error = "Visualization is disabled." });

            var agents = await agentStatusService.GetAllAgentStatusesAsync(ct);
            return Results.Ok(agents);
        }).WithSummary("Get all configured agents with current status (Idle/Working)");
    }
}
