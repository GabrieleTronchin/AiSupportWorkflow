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
            condition: msg => msg is { IsCodeRelated: true });
        builder.AddEdge(teamAssignmentExecutor, agentAssignmentExecutor);
        builder.AddEdge(agentAssignmentExecutor, resolutionExecutor);
        builder.AddEdge(resolutionExecutor, humanApprovalGateExecutor);
        builder.AddEdge<ApprovalDecision>(humanApprovalGateExecutor, codeGenerationExecutor,
            condition: msg => msg is { Approved: true });

        // Executors that terminate the run by yielding a WorkflowResult
        // (out-of-scope, rejected approval, generated code change).
        // Since Agent Framework 1.x outputs are only surfaced from registered output executors.
        builder.WithOutputFrom(classificationExecutor, humanApprovalGateExecutor, codeGenerationExecutor);

        return builder.Build();
    }
}
