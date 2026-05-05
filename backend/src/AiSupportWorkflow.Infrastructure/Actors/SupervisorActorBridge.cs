namespace AiSupportWorkflow.Infrastructure.Actors;

using Akka.Actor;
using Akka.Hosting;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.Messages;
using AiSupportWorkflow.Domain.ValueObjects;

public class SupervisorActorBridge(IRequiredActor<SupervisorActor> supervisorActor)
    : ISupervisorActorBridge
{
    private readonly IActorRef _supervisor = supervisorActor.ActorRef;

    public async Task<ResolutionReport> AssignIssueAsync(
        string agentId, IssueRecord issue, IssueCategory category,
        TimeSpan timeout, CancellationToken ct)
    {
        var message = new AssignIssueMessage(agentId, issue, category);
        var response = await _supervisor.Ask<ResolutionCompleteMessage>(message, timeout, ct);
        return response.Report;
    }
}
