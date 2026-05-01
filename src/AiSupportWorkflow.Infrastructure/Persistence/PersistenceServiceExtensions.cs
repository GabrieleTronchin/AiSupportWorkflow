namespace AiSupportWorkflow.Infrastructure.Persistence;

using AiSupportWorkflow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static class PersistenceServiceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddDbContext<WorkflowDbContext>(options =>
            options.UseInMemoryDatabase("WorkflowDb"));

        services.AddScoped<IWorkflowStateTracker, EfWorkflowStateTracker>();

        return services;
    }
}
