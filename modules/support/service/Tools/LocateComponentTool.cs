namespace Flowgent.Modules.Support.Tools;

using System.Text.RegularExpressions;
using Flowgent.Module.Sdk;

public sealed record LocateComponentInput(string ApplicationFolder, string Text);

/// <param name="Path">Relative to the support module folder, e.g. <c>dummy-apps/application-a/src/Controllers/OrderController.cs</c>.</param>
public sealed record LocateComponentOutput(bool Found, string Component, string Path, string Language, string? Snippet);

/// <summary>
/// <c>support.locateComponent</c>: finds the dummy-app source file mentioned in a support email (by file name or
/// type name, earliest mention wins) and returns its module-relative path and content for the LLM prompts.
/// </summary>
public sealed partial class LocateComponentTool : ModuleTool<LocateComponentInput, LocateComponentOutput>
{
    public const string Name = "support.locateComponent";
    public const string UnknownComponent = "UnknownComponent";
    public const string DefaultDummyAppsRoot = "dummy-apps";
    public const int MaxSnippetLength = 4000;

    private static readonly Dictionary<string, string> Languages = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "csharp",
        [".razor"] = "razor",
        [".cshtml"] = "razor",
        [".ts"] = "typescript",
        [".tsx"] = "tsx",
        [".js"] = "javascript",
        [".css"] = "css",
    };

    protected override async Task<LocateComponentOutput> InvokeAsync(LocateComponentInput input, ToolInvocation invocation, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.ApplicationFolder) || !FolderPattern().IsMatch(input.ApplicationFolder))
        {
            throw new ToolException($"Invalid application folder '{input.ApplicationFolder}'.");
        }

        var rootSetting = invocation.Module.Config["dummyAppsRoot"]?.GetValue<string>() ?? DefaultDummyAppsRoot;
        var dummyAppsRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(invocation.Module.ModuleRoot, rootSetting));
        var applicationRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(dummyAppsRoot, input.ApplicationFolder));

        if (!applicationRoot.StartsWith(dummyAppsRoot + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(applicationRoot))
        {
            throw new ToolException($"Unknown application folder '{input.ApplicationFolder}'.");
        }

        var relativeRoot = $"{rootSetting.Trim('/', '\\').Replace('\\', '/')}/{input.ApplicationFolder}";
        var match = FindFirstMentioned(applicationRoot, input.Text ?? "");
        if (match is null)
        {
            return new LocateComponentOutput(false, UnknownComponent, $"{relativeRoot}/src/{UnknownComponent}", "", null);
        }

        var content = await File.ReadAllTextAsync(match, ct);
        var relative = System.IO.Path.GetRelativePath(applicationRoot, match).Replace('\\', '/');
        return new LocateComponentOutput(
            true,
            System.IO.Path.GetFileName(match),
            $"{relativeRoot}/{relative}",
            Languages.GetValueOrDefault(System.IO.Path.GetExtension(match), ""),
            content.Length <= MaxSnippetLength ? content : content[..MaxSnippetLength] + "\n// ... (truncated)");
    }

    /// <summary>The source file whose name (with or without extension) appears first in the text.</summary>
    internal static string? FindFirstMentioned(string applicationRoot, string text) =>
        Directory.EnumerateFiles(applicationRoot, "*", SearchOption.AllDirectories)
            .Where(f => Languages.ContainsKey(System.IO.Path.GetExtension(f)))
            .Select(f => (File: f, Index: FirstMention(text, f)))
            .Where(x => x.Index >= 0)
            .OrderBy(x => x.Index)
            .ThenByDescending(x => System.IO.Path.GetFileName(x.File).Length)
            .ThenBy(x => x.File, StringComparer.Ordinal)
            .Select(x => x.File)
            .FirstOrDefault();

    private static int FirstMention(string text, string file)
    {
        var name = System.IO.Path.GetFileName(file);
        var stem = System.IO.Path.GetFileNameWithoutExtension(file);
        var pattern = $@"\b(?:{Regex.Escape(name)}|{Regex.Escape(stem)})\b";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return match.Success ? match.Index : -1;
    }

    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex FolderPattern();
}
