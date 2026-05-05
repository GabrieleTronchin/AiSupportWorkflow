namespace AiSupportWorkflow.Domain.Entities;

using AiSupportWorkflow.Domain.Enums;

public record WorkflowState(Guid IssueId, WorkflowStage Stage, DateTimeOffset LastUpdated, string? Detail)
{
    public bool IsTerminal => Stage is WorkflowStage.Failed
        or WorkflowStage.CodeChangeGenerated
        or WorkflowStage.ClassifiedOutOfScope;
}
