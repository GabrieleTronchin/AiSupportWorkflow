namespace AiSupportWorkflow.Infrastructure.Persistence;

using AiSupportWorkflow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

internal sealed class WorkflowCheckpointStore(IDbContextFactory<WorkflowDbContext> dbContextFactory)
{
    public async Task SaveCheckpointAsync(Guid issueId, string executorId, string serializedState)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var existing = await dbContext.WorkflowCheckpoints
            .FirstOrDefaultAsync(c => c.IssueId == issueId && c.IsActive);

        if (existing is not null)
        {
            existing.ExecutorId = executorId;
            existing.SerializedState = serializedState;
            existing.PausedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            dbContext.WorkflowCheckpoints.Add(new WorkflowCheckpoint
            {
                Id = Guid.NewGuid(),
                IssueId = issueId,
                ExecutorId = executorId,
                SerializedState = serializedState,
                PausedAt = DateTimeOffset.UtcNow,
                IsActive = true,
            });
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<WorkflowCheckpoint>> GetActiveCheckpointsAsync()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.WorkflowCheckpoints
            .Where(c => c.IsActive)
            .OrderBy(c => c.PausedAt)
            .ToListAsync();
    }

    public async Task MarkResumedAsync(Guid issueId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var checkpoint = await dbContext.WorkflowCheckpoints
            .FirstOrDefaultAsync(c => c.IssueId == issueId && c.IsActive);

        if (checkpoint is not null)
        {
            checkpoint.ResumedAt = DateTimeOffset.UtcNow;
            checkpoint.IsActive = false;
            await dbContext.SaveChangesAsync();
        }
    }
}
