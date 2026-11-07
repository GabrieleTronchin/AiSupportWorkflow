namespace Flowgent.Engine.Tests.Runtime;

using System.Text.Json.Nodes;
using Flowgent.Contracts.Api;
using Flowgent.Contracts.Runs;
using Flowgent.Engine.Tests.Helpers;
using Flowgent.Runtime.Modules;
using Microsoft.Extensions.DependencyInjection;

public class ModuleCatalogTests
{
    private static readonly Dictionary<string, string?> TwoModules = new()
    {
        ["Flowgent:Modules:1:Id"] = "other",
        ["Flowgent:Modules:1:Url"] = "http://other",
        ["Flowgent:Modules:1:Retries"] = "0",
    };

    [Fact]
    public async Task ValidPackage_MakesModuleAvailable()
    {
        await using var engine = await TestEngine.StartAsync();

        var module = Assert.Single(engine.Services.GetRequiredService<IModuleCatalog>().Modules);
        Assert.Equal(ModuleStatus.Available, module.Status);
        Assert.Equal(TestModules.Package().Version, module.Definition!.Version);
        Assert.NotNull(module.LastLoadedAt);
        Assert.Equal(["hello"], module.ToInfo().Workflows);
    }

    [Fact]
    public async Task InvalidPackage_IsReportedWithErrorsAndWorkflowsAreNotExposed()
    {
        await using var engine = await TestEngine.StartAsync(f => f.Replace("workflows/hello.yaml", "to: short", "to: ghost"));
        var catalog = engine.Services.GetRequiredService<IModuleCatalog>();

        var module = Assert.Single(catalog.Modules);
        Assert.Equal(ModuleStatus.Invalid, module.Status);
        Assert.Contains(module.Errors, e => e.StepId == "count" && e.Message.Contains("ghost"));
        Assert.Null(catalog.FindWorkflow("hello"));
    }

    [Fact]
    public async Task UnreachableModule_IsUnavailableUntilItAnswers()
    {
        await using var engine = await TestEngine.StartAsync();
        var service = engine.Services.GetRequiredService<ModuleCatalogService>();
        var catalog = engine.Services.GetRequiredService<IModuleCatalog>();

        engine.Modules.DownModules.Add("demo");
        await service.RefreshAllAsync(TestContext.Current.CancellationToken);
        var down = catalog.FindModule("demo")!;
        Assert.Equal(ModuleStatus.Unavailable, down.Status);
        Assert.Contains("No connection", down.LastError);
        Assert.NotNull(down.Definition);
        Assert.False(catalog.FindWorkflow("hello")!.IsAvailable);

        engine.Modules.DownModules.Remove("demo");
        await service.RefreshAllAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ModuleStatus.Available, catalog.FindModule("demo")!.Status);
    }

    [Fact]
    public async Task ModuleNeverReached_StaysWithoutDefinitions()
    {
        await using var engine = await TestEngine.StartAsync(configuration: TwoModules);

        var other = engine.Services.GetRequiredService<IModuleCatalog>().FindModule("other")!;
        Assert.Equal(ModuleStatus.Unavailable, other.Status);
        Assert.Null(other.Definition);
        Assert.Equal(ModuleStatus.Available, engine.Services.GetRequiredService<IModuleCatalog>().FindModule("demo")!.Status);
    }

    [Fact]
    public async Task ManifestIdNotMatchingConfiguredId_IsInvalid()
    {
        await using var engine = await TestEngine.StartAsync(configuration: TwoModules);
        engine.Modules.SetPackage("other", TestModules.Package(f =>
        {
            f.Replace("module.yaml", "id: demo\nname: Demo", "id: impostor\nname: Demo");
            f.Replace("module.yaml", "name: demo.count", "name: impostor.count");
            f.Replace("workflows/hello.yaml", "tool: demo.count", "tool: impostor.count");
        }));

        await engine.Services.GetRequiredService<ModuleCatalogService>().RefreshAllAsync(TestContext.Current.CancellationToken);

        var other = engine.Services.GetRequiredService<IModuleCatalog>().FindModule("other")!;
        Assert.Equal(ModuleStatus.Invalid, other.Status);
        Assert.Contains("does not match the configured id 'other'", other.LastError);
    }

    [Fact]
    public async Task WorkflowIdAlreadyProvidedByAnotherModule_RefusesTheConflictingModule()
    {
        await using var engine = await TestEngine.StartAsync(configuration: TwoModules);
        engine.Modules.SetPackage("other", TestModules.Package(f =>
        {
            f.Replace("module.yaml", "id: demo\nname: Demo", "id: other\nname: Demo");
            f.Replace("module.yaml", "name: demo.count", "name: other.count");
            f.Replace("workflows/hello.yaml", "tool: demo.count", "tool: other.count");
        }));

        await engine.Services.GetRequiredService<ModuleCatalogService>().RefreshAllAsync(TestContext.Current.CancellationToken);
        var catalog = engine.Services.GetRequiredService<IModuleCatalog>();

        Assert.Equal(ModuleStatus.Invalid, catalog.FindModule("other")!.Status);
        Assert.Contains("Workflow 'hello' is already provided by module 'demo'", catalog.FindModule("other")!.LastError);
        Assert.Equal("demo", catalog.FindWorkflow("hello")!.Module.Id);
    }

    [Fact]
    public async Task NewVersion_IsLoaded_AndInFlightRunKeepsItsVersion()
    {
        await using var engine = await TestEngine.StartAsync(TestModules.AddReviewWorkflow);
        var runId = await engine.SubmitAsync(new JsonObject { ["subject"] = "alpha" }, "review");
        var execution = engine.Orchestrator.ExecuteAsync(runId);
        await engine.WaitForHumanInputAsync(runId);

        // The module restarts with a new definition: the final message changes.
        engine.Modules.SetPackage("demo", TestModules.Package(f =>
        {
            TestModules.AddReviewWorkflow(f);
            f.Replace("workflows/review.yaml", "message: \"{{ steps.write.summary }}\"", "message: \"v2 {{ steps.write.summary }}\"");
        }));
        await engine.Services.GetRequiredService<ModuleCatalogService>().RefreshAllAsync(TestContext.Current.CancellationToken);
        var catalog = engine.Services.GetRequiredService<IModuleCatalog>();
        Assert.NotEqual(TestModules.Package(TestModules.AddReviewWorkflow).Version, catalog.FindModule("demo")!.Definition!.Version);

        await engine.HumanInput.ApproveAsync(runId);
        await execution;
        Assert.Equal("normal Alpha", (await engine.Store.GetAsync(runId))!.Detail);

        var newRun = await engine.SubmitAsync(new JsonObject { ["subject"] = "alpha" }, "review");
        var newExecution = engine.Orchestrator.ExecuteAsync(newRun);
        await engine.WaitForHumanInputAsync(newRun);
        await engine.HumanInput.ApproveAsync(newRun);
        await newExecution;
        var run = (await engine.Store.GetAsync(newRun))!;
        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal("v2 normal Alpha", run.Detail);
    }
}
