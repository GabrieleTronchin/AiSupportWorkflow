namespace LlmFlows.Definitions.Schemas;

using System.Text.Json.Nodes;
using Json.Schema;

/// <summary>JSON Schemas of the YAML formats (<c>backend/schemas</c>, embedded in this assembly).</summary>
public static class FormatSchemas
{
    public const string ModuleName = "module";
    public const string WorkflowName = "workflow";
    public const string SamplesName = "samples";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Texts = new(LoadTexts);
    private static readonly Lazy<JsonSchema> ModuleSchema = new(() => Build(ModuleName));
    private static readonly Lazy<JsonSchema> WorkflowSchema = new(() => Build(WorkflowName));
    private static readonly Lazy<JsonSchema> SamplesSchema = new(() => Build(SamplesName));

    public static JsonSchema Module => ModuleSchema.Value;

    public static JsonSchema Workflow => WorkflowSchema.Value;

    public static JsonSchema Samples => SamplesSchema.Value;

    public static IReadOnlyCollection<string> Names => [ModuleName, WorkflowName, SamplesName];

    /// <summary>Raw schema text by name (<c>module</c>, <c>workflow</c>, <c>samples</c>), for publishing.</summary>
    public static string? GetText(string name) => Texts.Value.GetValueOrDefault(name);

    private static JsonSchema Build(string name)
    {
        var text = GetText(name) ?? throw new InvalidOperationException($"Embedded schema '{name}' not found.");
        var node = JsonNode.Parse(text)!;
        return JsonSchemaValidator.Parse(node);
    }

    private static IReadOnlyDictionary<string, string> LoadTexts()
    {
        var assembly = typeof(FormatSchemas).Assembly;
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in new[] { ModuleName, WorkflowName, SamplesName })
        {
            using var stream = assembly.GetManifestResourceStream($"LlmFlows.Schemas.{name}.schema.json")
                ?? throw new InvalidOperationException($"Embedded schema '{name}' not found.");
            using var reader = new StreamReader(stream);
            texts[name] = reader.ReadToEnd();
        }

        return texts;
    }
}
