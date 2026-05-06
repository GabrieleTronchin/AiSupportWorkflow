namespace AiSupportWorkflow.Domain.Interfaces;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;

public interface IWorkflowStateTracker
{
    Task TransitionAsync(Guid issueId, WorkflowStage stage, string? detail = null, string? subject = null);
    WorkflowState GetState(Guid issueId);
    IReadOnlyList<WorkflowState> GetAllStates();
}
