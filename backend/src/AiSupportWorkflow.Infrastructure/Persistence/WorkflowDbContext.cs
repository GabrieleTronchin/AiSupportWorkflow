namespace AiSupportWorkflow.Infrastructure.Persistence;

using AiSupportWorkflow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

public class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    public DbSet<IssueEntity> Issues => Set<IssueEntity>();
    public DbSet<StateTransitionEvent> Events => Set<StateTransitionEvent>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkflowDbContext).Assembly);
    }
}
