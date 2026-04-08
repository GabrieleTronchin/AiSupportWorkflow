namespace AiSupportWorkflow.Infrastructure.Services;

using System.Collections.Concurrent;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;

public class WorkflowStateTracker : IWorkflowStateTracker
{
    private readonly ConcurrentDictionary<Guid, WorkflowState> _states = new();

    public void Transition(Guid issueId, WorkflowStage stage, string? detail = null)
    {
        var state = new WorkflowState(issueId, stage, DateTimeOffset.UtcNow, detail);
        _states.AddOrUpdate(issueId, state, (_, _) => state);
    }

    public WorkflowState GetState(Guid issueId) =>
        _states.TryGetValue(issueId, out var state)
            ? state
            : new WorkflowState(issueId, WorkflowStage.Received, DateTimeOffset.UtcNow, null);

    public IReadOnlyList<WorkflowState> GetAllStates() =>
        _states.Values.ToList();
}
