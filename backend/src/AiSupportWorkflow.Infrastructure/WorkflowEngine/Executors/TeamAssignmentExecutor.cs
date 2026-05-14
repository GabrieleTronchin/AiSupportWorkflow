namespace AiSupportWorkflow.Infrastructure.WorkflowEngine.Executors;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Agents.AI.Workflows;

internal sealed partial class TeamAssignmentExecutor(
    ITeamRouter teamRouter,
    IWorkflowStateTracker stateTracker) : Executor("TeamAssignmentExecutor")
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) =>
        protocolBuilder.AddClassAttributeTypes(GetType());

    [MessageHandler]
    private async ValueTask<TeamAssignment> HandleAsync(
        ClassificationResult classification, IWorkflowContext context, CancellationToken ct)
    {
        var issueId = await context.ReadStateAsync<Guid>("CurrentIssueId", scopeName: "Workflow", ct);

        var issue = await context.ReadStateAsync<IssueRecord>(issueId.ToString(), scopeName: "Issues", ct)
            ?? throw new InvalidOperationException("Issue not found in workflow state");

        var teamResult = teamRouter.Route(issue, classification);

        if (!teamResult.IsSuccess)
            throw new InvalidOperationException(teamResult.Error!);

        await stateTracker.TransitionAsync(issueId, WorkflowStage.TeamAssigned, teamResult.Value!.TeamName);

        return teamResult.Value!;
    }
}
