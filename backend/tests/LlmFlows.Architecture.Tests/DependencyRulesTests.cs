namespace LlmFlows.Architecture.Tests;

using System.Reflection;
using System.Text.RegularExpressions;
using LlmFlows.Fixtures.Module;
using LlmFlows.Modules.Support;
using LlmFlows.Modules.Template;
using NetArchTest.Rules;

/// <summary>
/// Dependency rules of the platform: the engine never depends on modules, modules depend only on the Module SDK
/// and the contracts, and the definition/runtime libraries stay free of ASP.NET Core.
/// Project files are inspected for references; compiled assemblies are inspected with NetArchTest.
/// </summary>
public class DependencyRulesTests
{
    private static readonly string[] EngineNamespaces = ["LlmFlows.Runtime", "LlmFlows.Persistence", "LlmFlows.Engine.Service", "LlmFlows.Gateway", "LlmFlows.Testing"];

    private static readonly string[] SupportDomainTerms = ["IncomingEmail", "IssueCategory", "TeamRouter", "/emails"];

    public static TheoryData<string> EngineAndGatewayProjects =>
        [.. ProjectGraph.EngineProjects.Append(ProjectGraph.Service("LlmFlows.Gateway")).Select(ProjectGraph.Relative)];

    public static TheoryData<string> ModuleServiceProjects => [.. ProjectGraph.ModuleServiceProjects.Select(ProjectGraph.Relative)];

    public static TheoryData<string> EngineAndGatewayAssemblies =>
    [
        "LlmFlows.Contracts", "LlmFlows.Definitions", "LlmFlows.Runtime", "LlmFlows.Persistence", "LlmFlows.Engine.Service", "LlmFlows.Gateway",
    ];

    public static TheoryData<string> ModuleAssemblies => [.. ModuleTypes.Select(t => t.Assembly.GetName().Name!)];

    private static readonly Type[] ModuleTypes = [typeof(SupportHost), typeof(TemplateHost), typeof(FixtureHost)];

    // ── Project references ─────────────────────────────────────────────

    [Fact]
    public void Contracts_HasNoProjectReferences() =>
        Assert.Empty(ProjectGraph.DirectReferences(ProjectGraph.Engine("LlmFlows.Contracts")));

    [Fact]
    public void Definitions_ReferencesOnlyContracts() =>
        Assert.Equal(
            [ProjectGraph.Relative(ProjectGraph.Engine("LlmFlows.Contracts"))],
            ProjectGraph.DirectReferences(ProjectGraph.Engine("LlmFlows.Definitions")).Select(ProjectGraph.Relative));

    [Theory]
    [MemberData(nameof(EngineAndGatewayProjects))]
    public void EngineAndGateway_NeverReferenceModulesOrFixtures(string project)
    {
        var references = ProjectGraph.TransitiveReferences(Path.Combine(ProjectGraph.Root, project)).Select(ProjectGraph.Relative);

        Assert.DoesNotContain(references, r => r.StartsWith("modules/", StringComparison.Ordinal) || r.Contains("/Fixtures/", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryModuleService_IsCovered() =>
        Assert.Superset(
            new HashSet<string>(["modules/support/service/LlmFlows.Modules.Support.csproj", "modules/_template/service/LlmFlows.Modules.Template.csproj"]),
            ProjectGraph.ModuleServiceProjects.Select(ProjectGraph.Relative).ToHashSet());

    [Theory]
    [MemberData(nameof(ModuleServiceProjects))]
    public void ModuleServices_ReferenceOnlyTheSdkContractsDefinitionsAndServiceDefaults(string project)
    {
        string[] allowed =
        [
            ProjectGraph.Sdk("LlmFlows.Module.Sdk"),
            ProjectGraph.Engine("LlmFlows.Contracts"),
            ProjectGraph.Engine("LlmFlows.Definitions"),
            ProjectGraph.ServiceDefaults,
        ];
        string[] forbidden =
        [
            ProjectGraph.Engine("LlmFlows.Runtime"),
            ProjectGraph.Engine("LlmFlows.Persistence"),
            ProjectGraph.Service("LlmFlows.Engine.Service"),
            ProjectGraph.Service("LlmFlows.Gateway"),
            ProjectGraph.Sdk("LlmFlows.Testing"),
        ];
        var path = Path.Combine(ProjectGraph.Root, project);

        Assert.All(ProjectGraph.DirectReferences(path), r => Assert.Contains(r, allowed, StringComparer.OrdinalIgnoreCase));
        Assert.Empty(ProjectGraph.TransitiveReferences(path).Intersect(forbidden, StringComparer.OrdinalIgnoreCase));
    }

    // ── Compiled assemblies (NetArchTest) ──────────────────────────────

    [Theory]
    [MemberData(nameof(EngineAndGatewayAssemblies))]
    public void EngineAndGateway_HaveNoModuleTypesOrDependencies(string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);

        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny("LlmFlows.Modules", "LlmFlows.Fixtures").GetResult();
        var moduleTypes = assembly.GetTypes().Where(t => t.Namespace is { } ns && (ns == "LlmFlows.Modules" || ns.StartsWith("LlmFlows.Modules.", StringComparison.Ordinal)));

        Assert.True(result.IsSuccessful, $"Types depending on modules: {string.Join(", ", result.FailingTypeNames ?? [])}");
        Assert.Empty(moduleTypes);
    }

    [Theory]
    [MemberData(nameof(ModuleAssemblies))]
    public void Modules_DoNotDependOnEngineImplementation(string assemblyName)
    {
        var result = Types.InAssembly(Assembly.Load(assemblyName)).ShouldNot().HaveDependencyOnAny(EngineNamespaces).GetResult();

        Assert.True(result.IsSuccessful, $"Types depending on engine implementation: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Theory]
    [InlineData("LlmFlows.Definitions")]
    [InlineData("LlmFlows.Runtime")]
    public void DefinitionsAndRuntime_DoNotDependOnAspNetCore(string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);

        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOn("Microsoft.AspNetCore").GetResult();

        Assert.True(result.IsSuccessful, $"Types depending on ASP.NET Core: {string.Join(", ", result.FailingTypeNames ?? [])}");
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), r => r.Name!.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    // ── Source files ───────────────────────────────────────────────────

    [Fact]
    public void EngineSources_DoNotMentionSupportDomainTerms()
    {
        var pattern = new Regex(string.Join('|', SupportDomainTerms.Select(Regex.Escape)), RegexOptions.CultureInvariant);
        var offending = ProjectGraph.EngineProjects
            .Select(Path.GetDirectoryName)
            .Append(Path.Combine(ProjectGraph.Root, "backend", "protos"))
            .SelectMany(folder => Directory.EnumerateFiles(folder!, "*.*", SearchOption.AllDirectories))
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".proto", StringComparison.Ordinal))
            .Where(file => !Regex.IsMatch(file, @"[\\/](bin|obj)[\\/]"))
            .Where(file => pattern.IsMatch(File.ReadAllText(file)))
            .Select(ProjectGraph.Relative)
            .ToList();

        Assert.Empty(offending);
    }
}
