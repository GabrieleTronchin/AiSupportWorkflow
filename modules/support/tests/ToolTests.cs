namespace Flowgent.Modules.Support.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using Flowgent.Module.Sdk;
using Flowgent.Modules.Support.Tools;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary><c>support.locateComponent</c> against the real dummy apps.</summary>
public class ToolTests
{
    private static readonly ModuleDefinitionProvider Provider = new(
        Options.Create(new ModuleSdkOptions { DefinitionPath = SupportPlatform.DefinitionFolder }),
        new TestEnvironment());

    [Theory]
    [InlineData("scenario-a1", "application-a", "OrderController.cs", "dummy-apps/application-a/src/Controllers/OrderController.cs", "csharp")]
    [InlineData("scenario-a2", "application-a", "OrderSummary.razor", "dummy-apps/application-a/src/Components/OrderSummary.razor", "razor")]
    [InlineData("scenario-a3", "application-a", "OrderServiceTests.cs", "dummy-apps/application-a/tests/OrderServiceTests.cs", "csharp")]
    [InlineData("scenario-b1", "application-b", "UserController.cs", "dummy-apps/application-b/src/Controllers/UserController.cs", "csharp")]
    [InlineData("scenario-b2", "application-b", "UserProfile.razor", "dummy-apps/application-b/src/Components/UserProfile.razor", "razor")]
    [InlineData("scenario-b3", "application-b", "UserServiceTests.cs", "dummy-apps/application-b/tests/UserServiceTests.cs", "csharp")]
    public void Scenario_LocatesTheAffectedFile(string sampleId, string folder, string component, string path, string language)
    {
        var input = SupportPlatform.Sample(sampleId).Input!;

        var result = Locate(folder, $"{input["subject"]} {input["body"]}");

        Assert.True(result.Found);
        Assert.Equal(component, result.Component);
        Assert.Equal(path, result.Path);
        Assert.Equal(language, result.Language);
        Assert.Equal(File.ReadAllText(Path.Combine(SupportPlatform.ModuleFolder, path)), result.Snippet);
    }

    [Fact]
    public void EarliestMention_Wins()
    {
        var result = Locate("application-a", "OrderSummary looks wrong, maybe because of OrderController");

        Assert.Equal("OrderSummary.razor", result.Component);
    }

    [Fact]
    public void NoMention_ReturnsUnknownComponentUnderTheApplication()
    {
        var result = Locate("application-b", "Something is slow");

        Assert.False(result.Found);
        Assert.Equal("UnknownComponent", result.Component);
        Assert.Equal("dummy-apps/application-b/src/UnknownComponent", result.Path);
        Assert.Null(result.Snippet);
    }

    [Theory]
    [InlineData("../application-a")]
    [InlineData("..")]
    [InlineData("application-a/../../definition")]
    [InlineData("C:")]
    [InlineData("")]
    [InlineData("Application-A")]
    public void InvalidFolders_AreRejected(string folder)
    {
        var ex = Assert.Throws<ToolException>(() => Locate(folder, "OrderController"));

        Assert.Contains("application folder", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownFolder_IsRejected()
    {
        var ex = Assert.Throws<ToolException>(() => Locate("application-z", "OrderController"));

        Assert.Equal("Unknown application folder 'application-z'.", ex.Message);
    }

    [Fact]
    public async Task Output_MatchesTheDeclaredToolSchema()
    {
        var tool = new LocateComponentTool();
        var output = await tool.InvokeAsync(Invocation("application-a", "OrderController"), TestContext.Current.CancellationToken);
        var schema = SupportPlatform.Definition.GetSchema(SupportPlatform.Definition.FindTool(LocateComponentTool.Name)!.Output!.Schema);

        Assert.Empty(Flowgent.Definitions.Schemas.JsonSchemaValidator.Validate(schema, output));
    }

    internal static LocateComponentOutput Locate(string folder, string text)
    {
        try
        {
            var output = new LocateComponentTool().InvokeAsync(Invocation(folder, text), CancellationToken.None).GetAwaiter().GetResult();
            return output.Deserialize<LocateComponentOutput>(ModuleTool<object, object>.JsonOptions)!;
        }
        catch (AggregateException ex) when (ex.InnerException is ToolException inner)
        {
            throw inner;
        }
    }

    private static ToolInvocation Invocation(string folder, string text) =>
        new(Guid.NewGuid(), "support", "support-email", "locate_component", new JsonObject { ["applicationFolder"] = folder, ["text"] = text }, Provider);

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "tests";

        public string ContentRootPath { get; set; } = SupportPlatform.DefinitionFolder;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
