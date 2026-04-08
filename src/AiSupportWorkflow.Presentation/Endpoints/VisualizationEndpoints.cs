namespace AiSupportWorkflow.Presentation.Endpoints;

using System.Text.Json;
using Akka.Actor;
using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.Messages;
using Microsoft.Extensions.Options;

public static class VisualizationEndpoints
{
    public static IEndpointRouteBuilder MapVisualizationEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/support/stream", async (
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

        routes.MapGet("/api/support/agents", async (
            ActorSystem actorSystem,
            IOptions<WorkflowConfiguration> config,
            CancellationToken ct) =>
        {
            if (!config.Value.EnableVisualization)
                return Results.NotFound(new { Error = "Visualization is disabled." });

            var agentIds = config.Value.Teams
                .SelectMany(team => team.Agents.Select(agent => $"{team.TeamName}_{agent.Role}"))
                .ToList();

            var responses = await QueryAgentStatusesAsync(actorSystem, agentIds, ct);
            return Results.Ok(responses);
        });

        return routes;
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

    private static async Task<List<AgentStatusResponse>> QueryAgentStatusesAsync(
        ActorSystem actorSystem,
        List<string> agentIds,
        CancellationToken ct)
    {
        var responses = new List<AgentStatusResponse>();

        foreach (var agentId in agentIds)
            responses.Add(await QuerySingleAgentStatusAsync(actorSystem, agentId, ct));

        return responses;
    }

    private static async Task<AgentStatusResponse> QuerySingleAgentStatusAsync(
        ActorSystem actorSystem,
        string agentId,
        CancellationToken ct)
    {
        try
        {
            var agentActor = await actorSystem
                .ActorSelection($"/user/supervisor/{agentId}")
                .ResolveOne(TimeSpan.FromSeconds(2), ct);

            return await agentActor.Ask<AgentStatusResponse>(
                new AgentStatusQuery(),
                TimeSpan.FromSeconds(5),
                ct);
        }
        catch
        {
            return new AgentStatusResponse(agentId, "Unavailable", null);
        }
    }
}
