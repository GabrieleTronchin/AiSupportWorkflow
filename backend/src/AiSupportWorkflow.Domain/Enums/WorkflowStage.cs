namespace AiSupportWorkflow.Domain.Enums;

public enum WorkflowStage
{
    Received,
    Classified,
    ClassifiedOutOfScope,
    TeamAssigned,
    AgentAssigned,
    Resolving,
    Resolved,
    CodeChangeGenerated,
    Failed,
    ManualReviewRequired
}
