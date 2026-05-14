namespace AiSupportWorkflow.Infrastructure.WorkflowEngine;

using AiSupportWorkflow.Domain.ValueObjects;
using AiSupportWorkflow.Infrastructure.WorkflowEngine.Executors;
using Microsoft.Agents.AI.Workflows;

internal sealed class SupportWorkflowFactory(
    ClassificationExecutor classificationExecutor,
    TeamAssignmentExecutor teamAssignmentExecutor,
    AgentAssignmentExecutor agentAssignmentExecutor,
    ResolutionExecutor resolutionExecutor,
    HumanApprovalGateExecutor humanApprovalGateExecutor,
    CodeGenerationExecutor codeGenerationExecutor)
{
    public Workflow Build()
    {
        WorkflowBuilder builder = new(classificationExecutor);

        builder.AddEdge<ClassificationResult>(classificationExecutor, teamAssignmentExecutor,
            msg => msg is { IsCodeRelated: true });
        builder.AddEdge(teamAssignmentExecutor, agentAssignmentExecutor);
        builder.AddEdge(agentAssignmentExecutor, resolutionExecutor);
        builder.AddEdge(resolutionExecutor, humanApprovalGateExecutor);
        builder.AddEdge<ApprovalDecision>(humanApprovalGateExecutor, codeGenerationExecutor,
            msg => msg is { Approved: true });

        return builder.Build();
    }
}
