namespace LlmFlows.Modules.Template;

using LlmFlows.Module.Sdk;
using LlmFlows.Modules.Template.Tools;

/// <summary>
/// Builds the template module service. <c>Program.cs</c> and the in-process test harness both use it.
/// <paramref name="configure"/> runs before the services are registered (configuration, test handlers).
/// </summary>
public static class TemplateHost
{
    public const string ModuleId = "template";
    public const string WorkflowId = "template-hello";

    public static WebApplication Create(WebApplicationOptions options, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(options);
        configure?.Invoke(builder);

        builder.AddServiceDefaults();
        builder.AddLlmFlowsModule();
        builder.Services.AddModuleTool<WordCountTool>(WordCountTool.Name);

        var app = builder.Build();

        app.MapLlmFlowsModule();
        app.MapDefaultEndpoints();

        return app;
    }
}
