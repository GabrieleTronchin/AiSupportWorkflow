namespace AiSupportWorkflow.Infrastructure;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Infrastructure.Configuration;
using AiSupportWorkflow.Infrastructure.AgentFramework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmProviderConfiguration>(configuration.GetSection("LlmProvider"));
        services.Configure<WorkflowConfiguration>(configuration.GetSection("Workflow"));

        services.AddChatClient(configuration);

        services.AddSingleton<IBugResolver, BugResolverService>();

        return services;
    }
}
