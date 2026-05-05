namespace AiSupportWorkflow.Infrastructure.Persistence;

using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static class PersistenceServiceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddDbContext<WorkflowDbContext>(options =>
            options.UseInMemoryDatabase("WorkflowDb"));

        services.AddSingleton<WorkflowUpdateChannel>();
        services.AddScoped<IWorkflowStateTracker, EfWorkflowStateTracker>();
        services.AddScoped<IInboxRepository, EfInboxRepository>();
        services.AddScoped<IInboxQueryService, EfInboxRepository>();
        services.AddScoped<IWorkflowEventRepository, EfWorkflowEventRepository>();

        return services;
    }
}
