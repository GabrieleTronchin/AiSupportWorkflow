namespace AiSupportWorkflow.Presentation.Endpoints;

using System.Text.Json;
using Akka.Actor;
using Akka.Hosting;
using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.Messages;
using AiSupportWorkflow.Infrastructure.Actors;
using AiSupportWorkflow.Presentation.Endpoints.Primitives;
using Microsoft.Extensions.Options;

public class VisualizationEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support").WithTags("Visualization");

        group.MapGet("/stream", async (
            HttpContext context,
            IWorkflowStateTracker stateTracker,
            IOptions<WorkflowConfiguration> config,
            CancellationToken ct) =>
        {
            if (!config.Value.EnableVisualization)
                return Results.NotFound(new { Error = "Visualization is disabled." });

            await StreamWorkflowStatesAsync(context, stateTracker, ct);
            return Results.Empty;
        });

        group.MapGet("/agents", async (
            IRequiredActor<SupervisorActor> supervisorActor,
            IOptions<WorkflowConfiguration> config,
            CancellationToken ct) =>
        {
            if (!config.Value.EnableVisualization)
                return Results.NotFound(new { Error = "Visualization is disabled." });

            var supervisor = supervisorActor.ActorRef;
            var response = await supervisor.Ask<AggregatedAgentStatusResponse>(
                new AgentStatusQuery(null),
                TimeSpan.FromSeconds(10),
                ct);

            return Results.Ok(response.Statuses);
        });
    }

    private static async Task StreamWorkflowStatesAsync(
        HttpContext context,
        IWorkflowStateTracker stateTracker,
        CancellationToken ct)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        while (!ct.IsCancellationRequested)
        {
            var states = stateTracker.GetAllStates();
            var json = JsonSerializer.Serialize(states);
            await context.Response.WriteAsync($"data: {json}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
            await Task.Delay(1000, ct);
        }
    }
}
