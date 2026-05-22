namespace AiSupportWorkflow.Infrastructure.Persistence;

using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

public static class PersistenceServiceExtensions
{
    private static readonly InMemoryDatabaseRoot DatabaseRoot = new();

    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        // Use shared InMemoryDatabaseRoot so all DbContext instances share the same data
        services.AddDbContextFactory<WorkflowDbContext>(options =>
            options.UseInMemoryDatabase("WorkflowDb", DatabaseRoot));
        // Register DbContext for scoped resolution (InboxProcessor creates scopes)
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContext());

        services.AddSingleton<WorkflowUpdateChannel>();
        services.AddSingleton<IWorkflowStateTracker, EfWorkflowStateTracker>();
        services.AddScoped<IInboxRepository, EfInboxRepository>();
        services.AddScoped<IInboxQueryService, EfInboxRepository>();
        services.AddScoped<IWorkflowEventRepository, EfWorkflowEventRepository>();

        return services;
    }
}
