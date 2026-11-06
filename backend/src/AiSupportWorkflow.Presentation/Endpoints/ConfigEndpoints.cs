namespace AiSupportWorkflow.Presentation.Endpoints;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Infrastructure.AgentFramework;
using AiSupportWorkflow.Infrastructure.Configuration;
using AiSupportWorkflow.Presentation.Endpoints.Primitives;
using Microsoft.Extensions.Options;

public class ConfigEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support").WithTags("Config");

        group.MapGet("/config", (IOptions<WorkflowConfiguration> workflow, IOptions<LlmProviderConfiguration> llm) =>
            Results.Ok(BuildResponse(workflow.Value, llm.Value)))
            .WithSummary("Get runtime configuration flags for the dashboard");
    }

    /// <summary>
    /// Dashboard-facing view of the runtime configuration. Only non-sensitive values are exposed:
    /// never the API key or the endpoint URL.
    /// </summary>
    public static RuntimeConfigResponse BuildResponse(WorkflowConfiguration workflow, LlmProviderConfiguration llm) =>
        llm.IsDemo
            ? new(workflow.SequentialProcessing, DemoMode: true, LlmProvider: LlmProviderConfiguration.DemoProvider, LlmModel: DemoChatClient.DemoModelId)
            : new(workflow.SequentialProcessing, DemoMode: false, LlmProvider: llm.Provider, LlmModel: llm.ModelName);
}

/// <param name="SequentialProcessing">Inbox processes one workflow at a time.</param>
/// <param name="DemoMode">LLM responses are simulated (LlmProvider:Provider = Demo).</param>
/// <param name="LlmProvider">Configured provider, e.g. "Demo" or "OpenAI".</param>
/// <param name="LlmModel">Model used for LLM calls, e.g. "gpt-4o-mini" ("demo-simulated" in Demo mode).</param>
public record RuntimeConfigResponse(bool SequentialProcessing, bool DemoMode, string LlmProvider, string LlmModel);
