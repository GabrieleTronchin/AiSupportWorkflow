namespace AiSupportWorkflow.Infrastructure.WorkflowEngine;

using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Infrastructure.AgentFramework;
using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.Infrastructure.Services;
using AiSupportWorkflow.Infrastructure.WorkflowEngine.Executors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class WorkflowEngineServiceExtensions
{
    public static IServiceCollection AddWorkflowEngine(this IServiceCollection services, IConfiguration configuration)
    {
        // LLM Telemetry (fed by LlmTelemetryMiddleware, registered in ChatClientSetup)
        services.TryAddSingleton<LlmTelemetryStore>();

        // Executors (singletons since they hold state for approval gate)
        services.AddSingleton<ClassificationExecutor>();
        services.AddSingleton<TeamAssignmentExecutor>();
        services.AddSingleton<AgentAssignmentExecutor>();
        services.AddSingleton<ResolutionExecutor>();
        services.AddSingleton<HumanApprovalGateExecutor>();
        services.AddSingleton<CodeGenerationExecutor>();

        // Workflow Factory
        services.AddSingleton<SupportWorkflowFactory>();

        // Approval Service
        services.AddScoped<WorkflowApprovalService>();

        // Workflow State Persistence
        services.AddSingleton<WorkflowCheckpointStore>();

        // Orchestrator (+ registry of running workflows, used by the abort endpoint)
        services.AddSingleton<WorkflowRunRegistry>();
        services.AddScoped<IOrchestrator, WorkflowOrchestrator>();

        // Agent status provider (replaces Akka-based provider)
        services.AddSingleton<IAgentStatusProvider, Services.TelemetryAgentStatusProvider>();

        return services;
    }
}
