namespace AiSupportWorkflow.UnitTests.Helpers;

using AiSupportWorkflow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// Test factory that creates fresh DbContext instances sharing the same InMemory database.
/// Safe for use with code that disposes contexts (await using).
/// </summary>
internal sealed class TestDbContextFactory : IDbContextFactory<WorkflowDbContext>
{
    private readonly DbContextOptions<WorkflowDbContext> _options;

    public TestDbContextFactory()
    {
        var root = new InMemoryDatabaseRoot();
        _options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), root)
            .Options;
    }

    public WorkflowDbContext CreateDbContext() => new(_options);
}
