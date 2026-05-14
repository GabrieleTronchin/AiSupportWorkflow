namespace AiSupportWorkflow.UnitTests.Helpers;

using AiSupportWorkflow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

internal sealed class TestDbContextFactory(WorkflowDbContext context) : IDbContextFactory<WorkflowDbContext>
{
    public WorkflowDbContext CreateDbContext() => context;
}
