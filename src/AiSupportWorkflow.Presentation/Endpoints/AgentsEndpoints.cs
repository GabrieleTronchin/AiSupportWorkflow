namespace AiSupportWorkflow.Presentation.Endpoints;

using Akka.Actor;
using Akka.Hosting;
using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Messages;
using AiSupportWorkflow.Infrastructure.Actors;
using AiSupportWorkflow.Presentation.Endpoints.Primitives;
using Microsoft.Extensions.Options;

public class AgentsEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support").WithTags("Agents");

        group.MapGet("/agents", async (
            IRequiredActor<SupervisorActor> supervisorActor,
            IOptions<WorkflowConfiguration> config,
            CancellationToken ct) =>
        {
            if (!config.Value.EnableVisualization)
                return Results.NotFound(new { Error = "Visualization is disabled." });

            // Get active agent statuses from Akka
            var supervisor = supervisorActor.ActorRef;
            var response = await supervisor.Ask<AggregatedAgentStatusResponse>(
                new AgentStatusQuery(null),
                TimeSpan.FromSeconds(10),
                ct);

            var activeAgents = response.Statuses.ToDictionary(s => s.AgentId, s => s);

            // Build full list from configuration, merging with active status
            var allAgents = config.Value.Teams
                .SelectMany(team => team.Agents.Select(agent =>
                {
                    var agentId = $"{team.TeamName}_{agent.Role}";
                    var isActive = activeAgents.TryGetValue(agentId, out var status);
                    return new
                    {
                        AgentId = agentId,
                        Team = team.TeamName,
                        Role = agent.Role.ToString(),
                        Status = isActive ? status!.Status : "Idle",
                        LastAction = isActive ? status!.LastAction : (string?)null,
                    };
                }))
                .ToList();

            return Results.Ok(allAgents);
        }).WithSummary("Get all configured agents with current status (Idle/Working)");
    }
}
