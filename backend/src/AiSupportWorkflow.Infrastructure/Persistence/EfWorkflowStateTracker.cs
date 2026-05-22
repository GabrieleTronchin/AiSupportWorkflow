namespace AiSupportWorkflow.Infrastructure.Persistence;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Infrastructure.Persistence.Entities;
using AiSupportWorkflow.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

internal sealed class EfWorkflowStateTracker(IDbContextFactory<WorkflowDbContext> dbContextFactory, WorkflowUpdateChannel? updateChannel = null) : IWorkflowStateTracker
{
    public async Task TransitionAsync(Guid issueId, WorkflowStage stage, string? detail = null, string? subject = null)
    {
        var now = DateTimeOffset.UtcNow;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var existing = await dbContext.Issues.FindAsync(issueId);
        WorkflowStage? previousStage = existing?.CurrentStage;

        if (existing is null)
        {
            dbContext.Issues.Add(new IssueEntity
            {
                Id = issueId,
                CurrentStage = stage,
                LastUpdated = now,
                Detail = detail,
                Subject = subject,
            });
        }
        else
        {
            existing.CurrentStage = stage;
            existing.LastUpdated = now;
            existing.Detail = detail;
            if (subject is not null)
                existing.Subject = subject;
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

        await dbContext.SaveChangesAsync();

        // Notify subscribers (gRPC stream)
        var state = new WorkflowState(issueId, stage, now, detail);
        updateChannel?.Writer.TryWrite(state);
    }

    public WorkflowState GetState(Guid issueId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        var entity = dbContext.Issues.Find(issueId);
        return entity is null
            ? new WorkflowState(issueId, WorkflowStage.Received, DateTimeOffset.MinValue, null)
            : new WorkflowState(entity.Id, entity.CurrentStage, entity.LastUpdated, entity.Detail, entity.Subject);
    }

    public IReadOnlyList<WorkflowState> GetAllStates()
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        return dbContext.Issues
            .AsNoTracking()
            .Select(e => new WorkflowState(e.Id, e.CurrentStage, e.LastUpdated, e.Detail, e.Subject))
            .ToList();
    }
}
