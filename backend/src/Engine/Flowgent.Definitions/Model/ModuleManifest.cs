namespace Flowgent.Definitions.Model;

using System.Text.Json.Nodes;

/// <summary><c>module.yaml</c>.</summary>
public sealed class ModuleManifest
{
    public const string FileName = "module.yaml";

    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public JsonObject? Config { get; init; }

    public List<AgentDefinition> Agents { get; init; } = [];

    public List<ToolDefinition> Tools { get; init; } = [];

    public List<string> Workflows { get; init; } = [];

    public List<string> Samples { get; init; } = [];
}

public sealed class AgentDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Group { get; init; }

    public string? Role { get; init; }

    public string? Persona { get; init; }
}

public sealed class ToolDefinition
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public SchemaRef? Input { get; init; }

    public SchemaRef? Output { get; init; }

    public int? TimeoutSeconds { get; init; }
}

/// <summary>A samples file (<c>samples/*.yaml</c>).</summary>
public sealed class SamplesFile
{
    public List<SampleDefinition> Samples { get; init; } = [];
}

public sealed class SampleDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Group { get; init; }

    public string? Description { get; init; }

    public required string Workflow { get; init; }

    public JsonObject? Input { get; init; }

    public bool ExpectInvalid { get; init; }
}
