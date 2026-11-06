namespace LlmFlows.Engine.Service;

using System.Text.Json.Serialization;
using LlmFlows.Engine.Service.Endpoints;
using LlmFlows.Engine.Service.Grpc;
using LlmFlows.Persistence;
using LlmFlows.Runtime;

/// <summary>
/// Builds the engine application. <c>Program.cs</c> and the in-process test harness both use it.
/// <paramref name="configure"/> runs before the engine services are registered, so it can add configuration
/// and replace abstractions registered with TryAdd (e.g. the HTTP primary handler in tests).
/// </summary>
public static class EngineHost
{
    public static WebApplication Create(WebApplicationOptions options, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(options);
        configure?.Invoke(builder);

        builder.AddServiceDefaults();
        builder.Services.ConfigureHttpJsonOptions(json => json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddProblemDetails();
        builder.Services.AddGrpc();

        builder.Services.AddLlmFlowsRuntime(builder.Configuration);
        builder.Services.AddLlmFlowsPersistence();

        var app = builder.Build();

        app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });
        app.MapGrpcService<RunMonitorService>();
        app.MapEngineEndpoints();
        app.MapDefaultEndpoints();

        return app;
    }
}
