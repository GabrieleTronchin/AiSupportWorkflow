namespace AiSupportWorkflow.Infrastructure.WorkflowEngine.Executors;

using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Agents.AI.Workflows;

internal sealed class AgentAssignmentExecutor(
    IAgentSelector agentSelector,
    IWorkflowStateTracker stateTracker) : Executor<TeamAssignment, AgentAssignment>("AgentAssignmentExecutor")
{
    public override async ValueTask<AgentAssignment> HandleAsync(
        TeamAssignment team, IWorkflowContext context, CancellationToken ct)
    {
        var classification = await context.ReadStateAsync<ClassificationResult>(
            "LatestClassification", scopeName: "Workflow", ct)
            ?? throw new InvalidOperationException("Classification not found in workflow state");

        var agent = agentSelector.Select(team, classification.Category);

        var issueId = await context.ReadStateAsync<Guid>("CurrentIssueId", scopeName: "Workflow", ct);

        await stateTracker.TransitionAsync(issueId, WorkflowStage.AgentAssigned, agent.AgentId);

        return agent;
    }
}
