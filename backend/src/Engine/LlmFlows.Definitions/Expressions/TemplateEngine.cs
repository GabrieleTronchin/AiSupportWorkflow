namespace LlmFlows.Definitions.Expressions;

using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

/// <summary>An expression or template could not be parsed or evaluated.</summary>
public sealed class ExpressionException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Data visible to expressions and templates: <c>input</c>, <c>steps</c>, <c>config</c>, <c>run</c> and,
/// inside a map step, <c>output</c>. Built from JSON only, so scripts never see CLR objects.
/// </summary>
public sealed class EvaluationScope(JsonObject data)
{
    private ScriptObject? _globals;

    public JsonObject Data { get; } = data;

    internal ScriptObject Globals => _globals ??= ScriptConversion.ToScriptObject(Data);

    /// <summary>Returns a scope with an extra top-level variable (e.g. <c>output</c> in map steps).</summary>
    public EvaluationScope With(string name, JsonNode? value)
    {
        var data = Data.DeepClone().AsObject();
        data[name] = value?.DeepClone();
        return new EvaluationScope(data);
    }
}

/// <summary>
/// Parses and evaluates Scriban templates and expressions in a sandbox: no template loader (include fails),
/// loop/recursion/output limits, regex timeout and data-only globals. Parsed scripts are cached.
/// </summary>
public sealed partial class TemplateEngine
{
    public static TemplateEngine Shared { get; } = new();

    private static readonly LexerOptions ExpressionLexer = new() { Mode = ScriptMode.ScriptOnly };
    private readonly ConcurrentDictionary<string, CompiledTemplate> _templates = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CompiledExpression> _expressions = new(StringComparer.Ordinal);

    /// <summary>True when the text contains Scriban placeholders (otherwise it is a literal).</summary>
    public static bool IsTemplate(string text) => text.Contains("{{", StringComparison.Ordinal);

    public bool TryCompileTemplate(string text, out CompiledTemplate? template, out string? error)
    {
        try
        {
            template = GetTemplate(text);
            error = null;
            return true;
        }
        catch (ExpressionException ex)
        {
            template = null;
            error = ex.Message;
            return false;
        }
    }

    public bool TryCompileExpression(string text, out CompiledExpression? expression, out string? error)
    {
        try
        {
            expression = GetExpression(text);
            error = null;
            return true;
        }
        catch (ExpressionException ex)
        {
            expression = null;
            error = ex.Message;
            return false;
        }
    }

    public CompiledTemplate GetTemplate(string text) => _templates.GetOrAdd(text, CompileTemplate);

    public CompiledExpression GetExpression(string text) => _expressions.GetOrAdd(text, CompileExpression);

    /// <summary>Renders a template to text (literals are returned unchanged).</summary>
    public string Render(string text, EvaluationScope scope) => GetTemplate(text).Render(scope);

    /// <summary>
    /// Evaluates a value from a definition: strings are templates (a single <c>{{ expr }}</c> yields a typed value),
    /// objects and arrays are evaluated recursively, other values are copied.
    /// </summary>
    public JsonNode? EvaluateValue(JsonNode? value, EvaluationScope scope) => value switch
    {
        null => null,
        JsonObject obj => new JsonObject(obj.Select(p => KeyValuePair.Create(p.Key, EvaluateValue(p.Value, scope)))),
        JsonArray array => new JsonArray([.. array.Select(item => EvaluateValue(item, scope))]),
        JsonValue v when v.TryGetValue<string>(out var text) => GetTemplate(text).Evaluate(scope),
        _ => value.DeepClone(),
    };

    /// <summary>Evaluates a condition; null, false, 0 and empty strings are false.</summary>
    public bool EvaluateCondition(string expression, EvaluationScope scope) => IsTruthy(GetExpression(expression).Evaluate(scope));

    public static bool IsTruthy(JsonNode? value) => value switch
    {
        null => false,
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.TryGetValue<string>(out var s) => s.Length > 0,
        JsonValue v when v.TryGetValue<double>(out var d) => d != 0,
        JsonValue v when v.TryGetValue<long>(out var l) => l != 0,
        _ => true,
    };

    internal static TemplateContext CreateContext(EvaluationScope scope)
    {
        var context = new TemplateContext
        {
            TemplateLoader = null,
            LoopLimit = 1000,
            RecursiveLimit = 50,
            LimitToString = 100_000,
            OutputLimit = 200_000,
            ObjectRecursionLimit = 20,
            RegexTimeOut = TimeSpan.FromSeconds(1),
            StrictVariables = false,
            EnableRelaxedMemberAccess = true,
            EnableRelaxedTargetAccess = true,
            EnableRelaxedIndexerAccess = true,
            EnableNullIndexer = true,
            MemberRenamer = member => member.Name,
        };
        context.PushGlobal(scope.Globals);
        return context;
    }

    private static CompiledTemplate CompileTemplate(string text)
    {
        if (!IsTemplate(text))
        {
            return new CompiledTemplate(text, null, null, []);
        }

        var single = SingleExpressionPattern().Match(text);
        if (single.Success && !single.Groups["expr"].Value.Contains("{{", StringComparison.Ordinal))
        {
            var expression = CompileExpression(single.Groups["expr"].Value);
            return new CompiledTemplate(text, null, expression, expression.ReferencedSteps);
        }

        var template = Template.Parse(text);
        ThrowIfErrors(template, text);
        return new CompiledTemplate(text, template, null, ReferenceCollector.CollectSteps(template));
    }

    private static CompiledExpression CompileExpression(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ExpressionException("The expression is empty.");
        }

        var template = Template.Parse(text, lexerOptions: ExpressionLexer);
        ThrowIfErrors(template, text);
        return new CompiledExpression(text, template, ReferenceCollector.CollectSteps(template));
    }

    private static void ThrowIfErrors(Template template, string text)
    {
        if (template.HasErrors)
        {
            var message = string.Join("; ", template.Messages.Select(m => m.Message));
            throw new ExpressionException($"Invalid expression '{Truncate(text)}': {message}");
        }
    }

    internal static string Truncate(string text) => text.Length <= 80 ? text : text[..77] + "...";

    // The whole string is exactly one {{ expression }} (with optional whitespace around it).
    [GeneratedRegex(@"^\s*\{\{-?(?<expr>(?:(?!\}\}).)*?)-?\}\}\s*$", RegexOptions.Singleline)]
    private static partial Regex SingleExpressionPattern();
}

/// <summary>A parsed template; a single <c>{{ expr }}</c> evaluates to a typed value.</summary>
public sealed class CompiledTemplate
{
    private readonly Template? _template;
    private readonly CompiledExpression? _expression;

    internal CompiledTemplate(string source, Template? template, CompiledExpression? expression, IReadOnlyCollection<string> referencedSteps)
    {
        Source = source;
        _template = template;
        _expression = expression;
        ReferencedSteps = referencedSteps;
    }

    public string Source { get; }

    public bool IsLiteral => _template is null && _expression is null;

    /// <summary>Step ids referenced as <c>steps.&lt;id&gt;</c>.</summary>
    public IReadOnlyCollection<string> ReferencedSteps { get; }

    public JsonNode? Evaluate(EvaluationScope scope)
    {
        if (_expression is not null)
        {
            return _expression.Evaluate(scope);
        }

        return JsonValue.Create(Render(scope));
    }

    public string Render(EvaluationScope scope)
    {
        if (_expression is not null)
        {
            return ToText(_expression.Evaluate(scope));
        }

        if (_template is null)
        {
            return Source;
        }

        try
        {
            return _template.Render(TemplateEngine.CreateContext(scope));
        }
        catch (Exception ex) when (ex is ScriptRuntimeException or InvalidOperationException or RegexMatchTimeoutException)
        {
            throw new ExpressionException($"Template error in '{TemplateEngine.Truncate(Source)}': {ex.Message}", ex);
        }
    }

    /// <summary>Text form of a value, as Scriban renders it (booleans lowercase, objects as JSON).</summary>
    public static string ToText(JsonNode? value) => value switch
    {
        null => "",
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonValue v when v.TryGetValue<bool>(out var b) => b ? "true" : "false",
        JsonValue v => v.ToJsonString(),
        _ => value.ToJsonString(),
    };
}

/// <summary>A parsed expression (Scriban script mode).</summary>
public sealed class CompiledExpression
{
    private readonly Template _template;

    internal CompiledExpression(string source, Template template, IReadOnlyCollection<string> referencedSteps)
    {
        Source = source;
        _template = template;
        ReferencedSteps = referencedSteps;
    }

    public string Source { get; }

    public IReadOnlyCollection<string> ReferencedSteps { get; }

    public JsonNode? Evaluate(EvaluationScope scope)
    {
        try
        {
            return ScriptConversion.ToJson(_template.Evaluate(TemplateEngine.CreateContext(scope)));
        }
        catch (Exception ex) when (ex is ScriptRuntimeException or InvalidOperationException or RegexMatchTimeoutException)
        {
            throw new ExpressionException($"Expression error in '{TemplateEngine.Truncate(Source)}': {ex.Message}", ex);
        }
    }
}

/// <summary>Collects <c>steps.&lt;id&gt;</c> references from a parsed script.</summary>
internal static class ReferenceCollector
{
    public static IReadOnlyCollection<string> CollectSteps(Template template)
    {
        var steps = new HashSet<string>(StringComparer.Ordinal);
        Visit(template.Page, steps, depth: 0);
        return steps;
    }

    private static void Visit(ScriptNode? node, HashSet<string> steps, int depth)
    {
        if (node is null || depth > 200)
        {
            return;
        }

        if (node is ScriptMemberExpression { Target: ScriptVariable { Name: "steps" }, Member: { } member })
        {
            steps.Add(member.Name);
        }

        for (var i = 0; i < node.ChildrenCount; i++)
        {
            Visit(node.GetChildren(i), steps, depth + 1);
        }
    }
}
