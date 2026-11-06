namespace LlmFlows.Modules.Support;

using LlmFlows.Module.Sdk;
using LlmFlows.Modules.Support.Endpoints;
using LlmFlows.Modules.Support.Tools;

/// <summary>
/// Builds the support module service. <c>Program.cs</c> and the in-process test harness both use it.
/// <paramref name="configure"/> runs before the services are registered (configuration, test handlers).
/// </summary>
public static class SupportHost
{
    public const string ModuleId = "support";
    public const string WorkflowId = "support-email";

    public static WebApplication Create(WebApplicationOptions options, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(options);
        configure?.Invoke(builder);

        builder.AddServiceDefaults();
        builder.AddLlmFlowsModule();
        builder.Services.AddModuleTool<LocateComponentTool>(LocateComponentTool.Name);

        var app = builder.Build();

        app.MapLlmFlowsModule();
        app.MapSupportEmailEndpoints();
        app.MapDefaultEndpoints();

        return app;
    }
}
