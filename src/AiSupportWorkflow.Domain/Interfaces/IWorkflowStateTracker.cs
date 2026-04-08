namespace AiSupportWorkflow.Domain.Interfaces;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;

public interface IWorkflowStateTracker
{
    void Transition(Guid issueId, WorkflowStage stage, string? detail = null);
    WorkflowState GetState(Guid issueId);
    IReadOnlyList<WorkflowState> GetAllStates();
}
