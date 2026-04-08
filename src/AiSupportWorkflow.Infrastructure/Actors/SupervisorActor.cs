namespace AiSupportWorkflow.Infrastructure.Actors;

using Akka.Actor;
using AiSupportWorkflow.Domain.Interfaces;

public class SupervisorActor : ReceiveActor
{
    private readonly Dictionary<string, IActorRef> _agentActors = new();

    public SupervisorActor(IEnumerable<IAIAgent> agents)
    {
        foreach (var agent in agents)
        {
            var props = Props.Create(() => new AIAgentActor(agent));
            var actorRef = Context.ActorOf(props, agent.AgentId);
            _agentActors[agent.AgentId] = actorRef;
        }

        Receive<AssignIssueMessage>(msg => HandleAssignIssue(msg));
        Receive<AgentStatusQuery>(msg => HandleStatusQuery(msg));
    }

    private void HandleAssignIssue(AssignIssueMessage message)
    {
        // Forward to all agents — the orchestrator should target a specific agent via its path
        // This is a broadcast fallback; normally the orchestrator sends directly to the child actor
        foreach (var actor in _agentActors.Values)
            actor.Forward(message);
    }

    private void HandleStatusQuery(AgentStatusQuery message)
    {
        foreach (var actor in _agentActors.Values)
            actor.Forward(message);
    }

    public IActorRef? GetAgent(string agentId) =>
        _agentActors.GetValueOrDefault(agentId);

    protected override SupervisorStrategy SupervisorStrategy()
    {
        return new OneForOneStrategy(
            maxNrOfRetries: 3,
            withinTimeRange: TimeSpan.FromMinutes(1),
            decider: Decider.From(ex => Directive.Restart));
    }
}
