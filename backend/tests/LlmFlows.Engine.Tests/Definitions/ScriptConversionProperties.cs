namespace LlmFlows.Engine.Tests.Definitions;

using System.Text.Json.Nodes;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LlmFlows.Definitions.Expressions;

public class ScriptConversionProperties
{
    // JSON → Scriban → JSON keeps every value (Run_Context data must not be altered by templates).
    [Property(MaxTest = 200)]
    public Property JsonRoundTrip_PreservesValues(string[] keys, int number, bool flag, NonNull<string> text)
    {
        var obj = new JsonObject();
        foreach (var key in keys.Where(k => k is not null).Distinct())
        {
            obj[key] = new JsonObject { ["n"] = number, ["b"] = flag, ["s"] = text.Get, ["a"] = new JsonArray(1, "x", null) };
        }

        var roundTrip = ScriptConversion.ToJson(ScriptConversion.ToScriptObject(obj));

        return JsonNode.DeepEquals(Normalize(obj), Normalize(roundTrip)).ToProperty();
    }

    // A single {{ path }} returns exactly the stored value.
    [Property(MaxTest = 200)]
    public Property SingleExpression_ReturnsStoredValue(int number, bool flag, NonNull<string> text)
    {
        var scope = new EvaluationScope(new JsonObject
        {
            ["steps"] = new JsonObject { ["a"] = new JsonObject { ["n"] = number, ["b"] = flag, ["s"] = text.Get } },
        });
        var engine = TemplateEngine.Shared;

        return (engine.GetTemplate("{{ steps.a.n }}").Evaluate(scope)!.GetValue<long>() == number
            && engine.GetTemplate("{{ steps.a.b }}").Evaluate(scope)!.GetValue<bool>() == flag
            && engine.GetTemplate("{{ steps.a.s }}").Evaluate(scope)!.GetValue<string>() == text.Get).ToProperty();
    }

    // Integers become long values, compare through their JSON text.
    private static JsonNode? Normalize(JsonNode? node) => node is null ? null : JsonNode.Parse(node.ToJsonString());
}
