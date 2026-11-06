// RemoveAllResilienceHandlers is experimental: it removes the service-wide standard handler (ServiceDefaults),
// whose 10 s attempt timeout would cut long tool and HTTP step calls. Timeouts are applied per call instead.
#pragma warning disable EXTEXP0001

namespace LlmFlows.Runtime;

using LlmFlows.Runtime.Agents;
using LlmFlows.Runtime.Configuration;
using LlmFlows.Runtime.Execution;
using LlmFlows.Runtime.HumanInput;
using LlmFlows.Runtime.Intake;
using LlmFlows.Runtime.Llm;
using LlmFlows.Runtime.Modules;
using LlmFlows.Runtime.Runs;
using LlmFlows.Runtime.Steps;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

public static class RuntimeServiceExtensions
{
    /// <summary>
    /// Registers the engine runtime: module catalog, compiler, step kinds, orchestrator, human input, intake,
    /// inbox processor, LLM client and telemetry. Persistence (<see cref="IRunStore"/>, <see cref="IInboxStore"/>) is registered separately.
    /// </summary>
    public static IServiceCollection AddLlmFlowsRuntime(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(EngineOptions.SectionName);
        services.Configure<EngineOptions>(section);
        var engineOptions = section.Get<EngineOptions>() ?? new EngineOptions();

        services.AddLlmFlowsChatClient(configuration);

        services.AddSingleton<ModuleCatalog>();
        services.AddSingleton<IModuleCatalog>(sp => sp.GetRequiredService<ModuleCatalog>());
        services.AddSingleton<IModuleClient, ModuleClient>();
        services.AddSingleton<ModuleCatalogService>();
        services.AddHostedService(sp => sp.GetRequiredService<ModuleCatalogService>());

        services.AddSingleton<RunUpdateChannel>();
        services.AddSingleton<RunRecorder>();
        services.AddSingleton<RunRegistry>();
        services.AddSingleton<HumanInputRegistry>();
        services.AddSingleton<HumanInputService>();

        services.AddSingleton<ToolStepHandler>();
        services.AddSingleton<IStepHandler>(sp => sp.GetRequiredService<ToolStepHandler>());
        services.AddSingleton<IStepHandler, LlmStepHandler>();
        services.AddSingleton<IStepHandler, RuleStepHandler>();
        services.AddSingleton<IStepHandler, MapStepHandler>();
        services.AddSingleton<IStepHandler, HumanInputStepHandler>();
        services.AddSingleton<IStepHandler, HttpStepHandler>();
        services.AddSingleton<WorkflowCompiler>();

        services.AddSingleton<RunOrchestrator>();
        services.AddSingleton<IRunExecutor>(sp => sp.GetRequiredService<RunOrchestrator>());
        services.AddSingleton<IntakeService>();
        services.AddSingleton<InboxProcessor>();
        services.AddHostedService(sp => sp.GetRequiredService<InboxProcessor>());
        services.AddSingleton<AgentStatusService>();

        foreach (var module in engineOptions.Modules.Where(m => m.Enabled).GroupBy(m => m.Id).Select(g => g.First()))
        {
            AddModuleHttpClient(services, module);
        }

        services.AddHttpClient(HttpStepHandler.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .RemoveAllResilienceHandlers();

        return services;
    }

    /// <summary>
    /// One client per module with its own retry policy. Timeouts are applied per call (package fetch, tool),
    /// so the client itself has no timeout.
    /// </summary>
    private static void AddModuleHttpClient(IServiceCollection services, ModuleEndpointOptions module)
    {
        var builder = services.AddHttpClient(ModuleEndpointOptions.HttpClientName(module.Id), client => client.Timeout = Timeout.InfiniteTimeSpan)
            .RemoveAllResilienceHandlers();

        if (module.Retries > 0)
        {
            builder.AddResilienceHandler("llmflows-module", pipeline => pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = module.Retries,
                Delay = TimeSpan.FromMilliseconds(Math.Max(1, module.RetryDelayMilliseconds)),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            }));
        }
    }
}
