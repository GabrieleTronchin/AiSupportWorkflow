namespace AiSupportWorkflow.Infrastructure.Persistence;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Infrastructure.Persistence.Entities;
using AiSupportWorkflow.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

internal sealed class EfWorkflowStateTracker(WorkflowDbContext dbContext, WorkflowUpdateChannel? updateChannel = null) : IWorkflowStateTracker
{
    public void Transition(Guid issueId, WorkflowStage stage, string? detail = null)
    {
        var now = DateTimeOffset.UtcNow;

        var existing = dbContext.Issues.Find(issueId);
        WorkflowStage? previousStage = existing?.CurrentStage;

        if (existing is null)
        {
            dbContext.Issues.Add(new IssueEntity
            {
                Id = issueId,
                CurrentStage = stage,
                LastUpdated = now,
                Detail = detail,
            });
        }
        else
        {
            existing.CurrentStage = stage;
            existing.LastUpdated = now;
            existing.Detail = detail;
        }

        dbContext.Events.Add(new StateTransitionEvent
        {
            Id = Guid.NewGuid(),
            IssueId = issueId,
            PreviousStage = previousStage,
            NewStage = stage,
            Timestamp = now,
            Detail = detail,
        });

        dbContext.SaveChanges();

        // Notify subscribers (gRPC stream)
        var state = new WorkflowState(issueId, stage, now, detail);
        updateChannel?.Writer.TryWrite(state);
    }

    public WorkflowState GetState(Guid issueId)
    {
        var entity = dbContext.Issues.Find(issueId);
        return entity is null
            ? new WorkflowState(issueId, WorkflowStage.Received, DateTimeOffset.MinValue, null)
            : new WorkflowState(entity.Id, entity.CurrentStage, entity.LastUpdated, entity.Detail);
    }

    public IReadOnlyList<WorkflowState> GetAllStates()
    {
        return dbContext.Issues
            .AsNoTracking()
            .Select(e => new WorkflowState(e.Id, e.CurrentStage, e.LastUpdated, e.Detail))
            .ToList();
    }

    public IReadOnlyList<StateTransitionEvent> GetEvents(int limit = 200)
    {
        return dbContext.Events
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToList();
    }
}
