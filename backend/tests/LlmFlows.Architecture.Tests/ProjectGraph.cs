namespace LlmFlows.Architecture.Tests;

using System.Xml.Linq;

/// <summary>Project references read from the <c>.csproj</c> files of the repository (no build needed).</summary>
public static class ProjectGraph
{
    public static readonly string Root = FindRepositoryRoot();

    public static string Engine(string name) => Path.Combine(Root, "backend", "src", "Engine", name, $"{name}.csproj");

    public static string Service(string name) => Path.Combine(Root, "backend", "src", "Services", name, $"{name}.csproj");

    public static string Sdk(string name) => Path.Combine(Root, "backend", "src", "Sdk", name, $"{name}.csproj");

    public static string ServiceDefaults => Path.Combine(Root, "backend", "src", "AppHost", "LlmFlows.ServiceDefaults", "LlmFlows.ServiceDefaults.csproj");

    public static string FixtureModule => Path.Combine(Root, "backend", "tests", "Fixtures", "LlmFlows.Fixtures.Module", "LlmFlows.Fixtures.Module.csproj");

    /// <summary>Engine projects: libraries and the engine service.</summary>
    public static IReadOnlyList<string> EngineProjects =>
    [
        Engine("LlmFlows.Contracts"),
        Engine("LlmFlows.Definitions"),
        Engine("LlmFlows.Runtime"),
        Engine("LlmFlows.Persistence"),
        Service("LlmFlows.Engine.Service"),
    ];

    /// <summary>Every module service (<c>modules/*/service/*.csproj</c>) plus the test fixture module.</summary>
    public static IReadOnlyList<string> ModuleServiceProjects =>
    [
        .. Directory.GetDirectories(Path.Combine(Root, "modules"))
            .Select(module => Path.Combine(module, "service"))
            .Where(Directory.Exists)
            .SelectMany(service => Directory.GetFiles(service, "*.csproj"))
            .Order(StringComparer.Ordinal),
        FixtureModule,
    ];

    /// <summary>Full paths of the projects referenced directly by <paramref name="project"/>.</summary>
    public static IReadOnlyList<string> DirectReferences(string project)
    {
        var directory = Path.GetDirectoryName(project)!;
        return XDocument.Load(project).Descendants("ProjectReference")
            .Select(r => Path.GetFullPath(Path.Combine(directory, r.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar))))
            .ToList();
    }

    /// <summary>Full paths of all projects reachable from <paramref name="project"/> (excluding itself).</summary>
    public static IReadOnlySet<string> TransitiveReferences(string project)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>(DirectReferences(project));
        while (pending.TryPop(out var next))
        {
            if (seen.Add(next))
            {
                foreach (var reference in DirectReferences(next))
                {
                    pending.Push(reference);
                }
            }
        }

        return seen;
    }

    public static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "backend", "LlmFlows.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root (folder containing backend/LlmFlows.slnx) not found.");
    }
}
