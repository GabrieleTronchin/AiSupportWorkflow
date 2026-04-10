namespace AiSupportWorkflow.Infrastructure.Actors;

using Akka.Actor;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.Messages;
using Microsoft.Extensions.Logging;

public class SupervisorActor : ReceiveActor
{
    private readonly Dictionary<string, IActorRef> _agentActors = new();
    private readonly ILogger<SupervisorActor> _logger;

    public SupervisorActor(IEnumerable<IAIAgent> agents, ILogger<SupervisorActor> logger)
    {
        _logger = logger;

        foreach (var agent in agents)
        {
            var props = Props.Create(() => new AIAgentActor(agent));
            var actorRef = Context.ActorOf(props, agent.AgentId);
            _agentActors[agent.AgentId] = actorRef;
        }

        Receive<AssignIssueMessage>(HandleAssignIssue);
        ReceiveAsync<AgentStatusQuery>(HandleStatusQuery);
    }

    private void HandleAssignIssue(AssignIssueMessage message)
    {
        if (_agentActors.TryGetValue(message.TargetAgentId, out var agent))
        {
            agent.Forward(message);
        }
        else
        {
            Sender.Tell(new AgentNotFoundMessage(message.TargetAgentId));
        }
    }

    private async Task HandleStatusQuery(AgentStatusQuery message)
    {
        var sender = Sender;

        if (message.TargetAgentId is not null)
        {
            if (_agentActors.TryGetValue(message.TargetAgentId, out var agent))
            {
                agent.Forward(message);
            }
            else
            {
                sender.Tell(new AgentNotFoundMessage(message.TargetAgentId));
            }
            return;
        }

        // Aggregate all agent statuses in parallel
        var tasks = _agentActors.Select(kvp =>
            kvp.Value.Ask<AgentStatusResponse>(
                new AgentStatusQuery(null),
                TimeSpan.FromSeconds(5))
            .ContinueWith(t => t.IsCompletedSuccessfully
                ? t.Result
                : new AgentStatusResponse(kvp.Key, "Unavailable", null)));

        var responses = await Task.WhenAll(tasks);
        sender.Tell(new AggregatedAgentStatusResponse(responses.ToList()));
    }

    protected override SupervisorStrategy SupervisorStrategy()
    {
        return new OneForOneStrategy(
            maxNrOfRetries: 3,
            withinTimeRange: TimeSpan.FromMinutes(1),
            decider: Decider.From(ex =>
            {
                var actorName = Context.Sender?.Path?.Name ?? "unknown";
                var directive = ex switch
                {
                    TimeoutException or HttpRequestException => Directive.Restart,
                    ArgumentException or InvalidOperationException => Directive.Stop,
                    OutOfMemoryException => Directive.Escalate,
                    _ => Directive.Restart
                };

                _logger.LogWarning(
                    "Supervisor decision: Actor={Actor}, Exception={ExceptionType}, Directive={Directive}",
                    actorName, ex.GetType().Name, directive);

                return directive;
            }));
    }
}
