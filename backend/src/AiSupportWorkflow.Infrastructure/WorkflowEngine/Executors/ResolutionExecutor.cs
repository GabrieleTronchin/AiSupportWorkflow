namespace AiSupportWorkflow.Infrastructure.WorkflowEngine.Executors;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

internal sealed partial class ResolutionExecutor(
    IChatClient chatClient,
    IWorkflowStateTracker stateTracker) : Executor("ResolutionExecutor")
{
    private static readonly ChatOptions ChatOpts = new() { Temperature = 0.2f };

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) =>
        protocolBuilder.AddClassAttributeTypes(GetType());

    [MessageHandler]
    private async ValueTask<ResolutionReport> HandleAsync(
        AgentAssignment agent, IWorkflowContext context, CancellationToken ct)
    {
        var issueId = await context.ReadStateAsync<Guid>("CurrentIssueId", scopeName: "Workflow", ct);

        var issue = await context.ReadStateAsync<IssueRecord>(issueId.ToString(), scopeName: "Issues", ct)
            ?? throw new InvalidOperationException("Issue not found in workflow state");

        await stateTracker.TransitionAsync(issueId, WorkflowStage.Resolving);

        var response = await chatClient.GetResponseAsync<ResolutionReport>(
            [new(ChatRole.User, $"Agent: {agent.AgentId} ({agent.Role})\nSubject: {issue.Subject}\n\nBody: {issue.Body}")],
            ChatOpts, cancellationToken: ct);
        var report = response.Result;

        await stateTracker.TransitionAsync(issueId, WorkflowStage.Resolved, report.ProposedFixSummary);

        return report;
    }
}
