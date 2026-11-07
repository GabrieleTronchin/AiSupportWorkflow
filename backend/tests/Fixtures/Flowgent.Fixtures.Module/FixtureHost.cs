namespace Flowgent.Fixtures.Module;

using System.Text.Json.Nodes;
using Flowgent.Module.Sdk;

/// <summary>
/// Test-only module service: the <c>fixture.reverse</c> tool and <c>POST /api/modules/fixture/echo</c>, the
/// "external API" called by the <c>http</c> step of <c>fixture-flow</c>.
/// </summary>
public static class FixtureHost
{
    public const string ModuleId = "fixture";
    public const string FlowWorkflowId = "fixture-flow";
    public const string RulesWorkflowId = "fixture-rules";
    public const string RunHeader = "X-Fixture-Run";

    public static WebApplication Create(WebApplicationOptions options, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(options);
        configure?.Invoke(builder);

        builder.AddServiceDefaults();
        builder.AddFlowgentModule();
        builder.Services.AddModuleTool<ReverseTool>(ReverseTool.Name);

        var app = builder.Build();

        app.MapFlowgentModule();
        app.MapPost($"/api/modules/{ModuleId}/echo", (JsonObject body, HttpRequest request) => Results.Ok(new JsonObject
        {
            ["echoed"] = body["value"]?.DeepClone(),
            ["queue"] = body["queue"]?.DeepClone(),
            ["runHeader"] = request.Headers[RunHeader].ToString(),
        }));
        app.MapDefaultEndpoints();

        return app;
    }
}

public sealed record ReverseInput(string Text);

public sealed record ReverseOutput(string Reversed);

/// <summary><c>fixture.reverse</c>: returns the text reversed.</summary>
public sealed class ReverseTool : ModuleTool<ReverseInput, ReverseOutput>
{
    public const string Name = "fixture.reverse";

    protected override Task<ReverseOutput> InvokeAsync(ReverseInput input, ToolInvocation invocation, CancellationToken ct) =>
        Task.FromResult(new ReverseOutput(new string([.. input.Text.Reverse()])));
}
