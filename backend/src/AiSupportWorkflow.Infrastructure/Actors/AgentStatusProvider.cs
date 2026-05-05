namespace AiSupportWorkflow.Infrastructure.Actors;

using Akka.Actor;
using Akka.Hosting;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.Messages;

public sealed class AgentStatusProvider(IRequiredActor<SupervisorActor> supervisorActor) : IAgentStatusProvider
{
    private readonly IActorRef _supervisor = supervisorActor.ActorRef;

    public async Task<IReadOnlyList<AgentStatusInfo>> GetAgentStatusesAsync(CancellationToken ct = default)
    {
        var response = await _supervisor.Ask<AggregatedAgentStatusResponse>(
            new AgentStatusQuery(null),
            TimeSpan.FromSeconds(10),
            ct);

        return response.Statuses
            .Select(s => new AgentStatusInfo(s.AgentId, s.Status, s.LastAction))
            .ToList();
    }
}
