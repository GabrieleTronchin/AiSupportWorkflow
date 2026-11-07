namespace Flowgent.Definitions.Expressions;

using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Scriban.Runtime;

/// <summary>Converts between JSON values and Scriban script objects (keys are kept as-is).</summary>
public static class ScriptConversion
{
    public static object? ToScript(JsonNode? node) => node switch
    {
        null => null,
        JsonObject obj => ToScriptObject(obj),
        JsonArray array => ToScriptArray(array),
        JsonValue value => ToScalar(value),
        _ => null,
    };

    public static ScriptObject ToScriptObject(JsonObject obj)
    {
        var script = new ScriptObject(obj.Count);
        foreach (var (key, value) in obj)
        {
            script[key] = ToScript(value);
        }

        return script;
    }

    public static JsonNode? ToJson(object? value) => value switch
    {
        null => null,
        JsonNode node => node.DeepClone(),
        string s => JsonValue.Create(s),
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        double d => double.IsFinite(d) ? JsonValue.Create(d) : JsonValue.Create(d.ToString(CultureInfo.InvariantCulture)),
        float f => float.IsFinite(f) ? JsonValue.Create(f) : JsonValue.Create(f.ToString(CultureInfo.InvariantCulture)),
        decimal m => JsonValue.Create(m),
        System.Numerics.BigInteger big => JsonValue.Create(big.ToString(CultureInfo.InvariantCulture)),
        IDictionary<string, object?> dictionary => ToJsonObject(dictionary),
        IDictionary dictionary => ToJsonObject(dictionary),
        IEnumerable enumerable => ToJsonArray(enumerable),
        _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture)),
    };

    private static ScriptArray ToScriptArray(JsonArray array)
    {
        var script = new ScriptArray(array.Count);
        foreach (var item in array)
        {
            script.Add(ToScript(item));
        }

        return script;
    }

    private static object? ToScalar(JsonValue value)
    {
        switch (value.GetValueKind())
        {
            case JsonValueKind.String:
                return value.GetValue<string>();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Number:
                if (value.TryGetValue<long>(out var l))
                {
                    return l;
                }

                if (value.TryGetValue<int>(out var i))
                {
                    return (long)i;
                }

                if (value.TryGetValue<double>(out var d))
                {
                    return d;
                }

                if (value.TryGetValue<JsonElement>(out var element))
                {
                    return element.TryGetInt64(out var el) ? el : element.GetDouble();
                }

                return Convert.ToDouble(value.GetValue<object>(), CultureInfo.InvariantCulture);
            default:
                return null;
        }
    }

    private static JsonObject ToJsonObject(IDictionary<string, object?> dictionary)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in dictionary)
        {
            obj[key] = ToJson(value);
        }

        return obj;
    }

    private static JsonObject ToJsonObject(IDictionary dictionary)
    {
        var obj = new JsonObject();
        foreach (DictionaryEntry entry in dictionary)
        {
            obj[Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? ""] = ToJson(entry.Value);
        }

        return obj;
    }

    private static JsonArray ToJsonArray(IEnumerable enumerable)
    {
        var array = new JsonArray();
        foreach (var item in enumerable)
        {
            array.Add(ToJson(item));
        }

        return array;
    }
}
