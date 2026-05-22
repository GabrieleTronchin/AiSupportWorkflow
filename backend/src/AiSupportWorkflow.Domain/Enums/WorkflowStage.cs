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
    AwaitingApproval,
    CodeChangeGenerated,
    Failed,
    ManualReviewRequired
}
