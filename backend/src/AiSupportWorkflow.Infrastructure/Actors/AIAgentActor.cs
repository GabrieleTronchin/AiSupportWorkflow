namespace AiSupportWorkflow.Infrastructure.Actors;

using Akka.Actor;
using AiSupportWorkflow.Domain.Interfaces;

public class AIAgentActor : ReceiveActor
{
    private readonly IAIAgent _agent;
    private string _status = "Idle";
    private string? _lastAction;

    public AIAgentActor(IAIAgent agent)
    {
        _agent = agent;

        ReceiveAsync<AssignIssueMessage>(HandleAssignIssue);
        Receive<AgentStatusQuery>(_ => HandleStatusQuery());
    }

    private async Task HandleAssignIssue(AssignIssueMessage message)
    {
        _status = "Resolving";
        _lastAction = $"Analyzing issue {message.Issue.Id}";

        var sender = Sender;
        var report = await _agent.AnalyzeAndResolveAsync(message.Issue);

        _status = "Idle";
        _lastAction = $"Resolved issue {message.Issue.Id}";

        sender.Tell(new ResolutionCompleteMessage(message.Issue.Id, report));
    }

    private void HandleStatusQuery()
    {
        Sender.Tell(new AgentStatusResponse(_agent.AgentId, _status, _lastAction));
    }
}
