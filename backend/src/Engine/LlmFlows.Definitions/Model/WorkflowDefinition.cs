namespace LlmFlows.Definitions.Model;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

/// <summary>A workflow file (<c>workflows/*.yaml</c>).</summary>
public sealed class WorkflowDefinition
{
    public const int DefaultMaxSteps = 100;

    public required string Id { get; init; }

    public required int Version { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>Template for the run title.</summary>
    public string? Title { get; init; }

    public required WorkflowInput Input { get; init; }

    public required string Start { get; init; }

    public int? MaxSteps { get; init; }

    public List<StepDefinition> Steps { get; init; } = [];

    /// <summary>Path of the source file, relative to the definition folder (set by the loader).</summary>
    [JsonIgnore]
    public string File { get; internal set; } = "";

    [JsonIgnore]
    public int EffectiveMaxSteps => MaxSteps ?? DefaultMaxSteps;

    public StepDefinition? FindStep(string id) => Steps.FirstOrDefault(s => s.Id == id);
}

public sealed class WorkflowInput
{
    public required SchemaSource Schema { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed class TransitionDefinition
{
    /// <summary>Scriban expression; no condition means default transition.</summary>
    public string? When { get; init; }

    public required string To { get; init; }
}

/// <summary>Base class of all steps. The YAML <c>kind</c> selects the concrete type.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(LlmStep), StepKinds.Llm)]
[JsonDerivedType(typeof(RuleStep), StepKinds.Rule)]
[JsonDerivedType(typeof(MapStep), StepKinds.Map)]
[JsonDerivedType(typeof(HumanInputStep), StepKinds.HumanInput)]
[JsonDerivedType(typeof(ToolStep), StepKinds.Tool)]
[JsonDerivedType(typeof(HttpStep), StepKinds.Http)]
[JsonDerivedType(typeof(TerminalStep), StepKinds.Terminal)]
public abstract class StepDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>Template for the step-completed event detail and the run detail.</summary>
    public string? Detail { get; init; }

    /// <summary>Template resolving to the agent assigned to the run after this step.</summary>
    public string? AssignAgent { get; init; }

    public string? Next { get; init; }

    public List<TransitionDefinition>? Transitions { get; init; }

    [JsonIgnore]
    public string Kind => this switch
    {
        LlmStep => StepKinds.Llm,
        RuleStep => StepKinds.Rule,
        MapStep => StepKinds.Map,
        HumanInputStep => StepKinds.HumanInput,
        ToolStep => StepKinds.Tool,
        HttpStep => StepKinds.Http,
        TerminalStep => StepKinds.Terminal,
        _ => "unknown",
    };

    /// <summary>Transitions in evaluation order (<c>next</c> is a single default transition).</summary>
    [JsonIgnore]
    public IReadOnlyList<TransitionDefinition> EffectiveTransitions =>
        Transitions is { Count: > 0 } ? Transitions
        : Next is not null ? [new TransitionDefinition { To = Next }]
        : [];
}

public static class StepKinds
{
    public const string Llm = "llm";
    public const string Rule = "rule";
    public const string Map = "map";
    public const string HumanInput = "human-input";
    public const string Tool = "tool";
    public const string Http = "http";
    public const string Terminal = "terminal";
}

public sealed class LlmStep : StepDefinition
{
    public TextSource? SystemPrompt { get; init; }

    public required TextSource Prompt { get; init; }

    public required SchemaRef Output { get; init; }

    public float? Temperature { get; init; }

    /// <summary>Template: agent the LLM call is attributed to (default: the run's assigned agent).</summary>
    public string? Agent { get; init; }

    public DemoDefinition? Demo { get; init; }
}

public sealed class DemoDefinition
{
    public JsonNode? Output { get; init; }

    public string? Value { get; init; }

    public List<RuleDefinition>? Rules { get; init; }

    public JsonNode? Default { get; init; }

    public string? Tool { get; init; }
}

public sealed class RuleDefinition
{
    /// <summary>Regular expression matched against the rule value.</summary>
    public string? Match { get; init; }

    /// <summary>Scriban expression (alternative to <see cref="Match"/>).</summary>
    public string? When { get; init; }

    public JsonNode? Output { get; init; }
}

public sealed class RuleStep : StepDefinition
{
    public required string Value { get; init; }

    public List<RuleDefinition> Rules { get; init; } = [];

    public bool IgnoreCase { get; init; } = true;

    /// <summary><c>first</c> (default) or <c>{ fail: message }</c>.</summary>
    [JsonConverter(typeof(MultipleMatchPolicyConverter))]
    public MultipleMatchPolicy? OnMultipleMatches { get; init; }

    public NoMatchPolicy? OnNoMatch { get; init; }
}

/// <summary>Behaviour when several rules match: take the first (<see cref="Fail"/> null) or fail.</summary>
public sealed record MultipleMatchPolicy(string? Fail);

/// <summary>Behaviour when no rule matches: fail with a message or output a fallback.</summary>
public sealed class NoMatchPolicy
{
    public string? Fail { get; init; }

    /// <summary>Fallback output, used when <see cref="Fail"/> is not set.</summary>
    public JsonNode? Output { get; init; }
}

public sealed class MapStep : StepDefinition
{
    public Dictionary<string, JsonObject>? Lookups { get; init; }

    public List<MapOutputDefinition> Outputs { get; init; } = [];
}

public sealed class MapOutputDefinition
{
    public required string Name { get; init; }

    public JsonNode? Value { get; init; }

    public string? Lookup { get; init; }

    public string? Key { get; init; }

    public string? OnMissing { get; init; }
}

public sealed class HumanInputStep : StepDefinition
{
    public required ReviewDefinition Review { get; init; }
}

public sealed class ReviewDefinition
{
    public required string Title { get; init; }

    public List<ReviewFieldDefinition> Fields { get; init; } = [];
}

public sealed class ReviewFieldDefinition
{
    public required string Label { get; init; }

    public required string Value { get; init; }
}

public sealed class ToolStep : StepDefinition
{
    public required string Tool { get; init; }

    public JsonObject? Input { get; init; }

    public int? TimeoutSeconds { get; init; }
}

public sealed class HttpStep : StepDefinition
{
    public const int DefaultTimeoutSeconds = 30;

    public required string Method { get; init; }

    public required string Url { get; init; }

    public Dictionary<string, HeaderValue>? Headers { get; init; }

    public JsonNode? Body { get; init; }

    public int? TimeoutSeconds { get; init; }

    public SchemaRef? Output { get; init; }
}

/// <summary>HTTP header value: a literal or a secret read from engine configuration.</summary>
[JsonConverter(typeof(HeaderValueConverter))]
public sealed record HeaderValue(string? Literal, string? Secret);

public enum TerminalOutcome
{
    Success,
    Failure,
}

public sealed class TerminalStep : StepDefinition
{
    public required TerminalOutcome Outcome { get; init; }

    public string? Message { get; init; }

    public JsonNode? Output { get; init; }
}

internal sealed class MultipleMatchPolicyConverter : JsonConverter<MultipleMatchPolicy>
{
    public override MultipleMatchPolicy? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString() == "first" ? new MultipleMatchPolicy(null) : throw new JsonException("Expected 'first' or { fail: message }.");
        }

        var node = JsonNode.Parse(ref reader) as JsonObject;
        return new MultipleMatchPolicy(node?["fail"]?.GetValue<string>() ?? throw new JsonException("Expected 'first' or { fail: message }."));
    }

    public override void Write(Utf8JsonWriter writer, MultipleMatchPolicy value, JsonSerializerOptions options)
    {
        if (value.Fail is null)
        {
            writer.WriteStringValue("first");
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("fail", value.Fail);
        writer.WriteEndObject();
    }
}

internal sealed class HeaderValueConverter : JsonConverter<HeaderValue>
{
    public override HeaderValue? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new HeaderValue(reader.GetString(), null);
        }

        var node = JsonNode.Parse(ref reader) as JsonObject;
        return new HeaderValue(null, node?["secret"]?.GetValue<string>() ?? throw new JsonException("Expected a string or { secret: key }."));
    }

    public override void Write(Utf8JsonWriter writer, HeaderValue value, JsonSerializerOptions options)
    {
        if (value.Secret is null)
        {
            writer.WriteStringValue(value.Literal);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("secret", value.Secret);
        writer.WriteEndObject();
    }
}
