namespace Flowgent.Engine.Tests.Definitions;

using System.Text.Json.Nodes;
using Flowgent.Definitions.Expressions;

public class TemplateEngineTests
{
    private static readonly TemplateEngine Engine = new();

    private static EvaluationScope Scope() => new(JsonNode.Parse("""
        {
          "input": { "subject": "Crash in Application A", "body": "NullReferenceException" },
          "steps": {
            "classify": { "isCodeRelated": true, "category": "BackendBug", "confidenceScore": 0.92 },
            "approval": { "decision": "rejected", "reason": null }
          },
          "config": { "dummyAppsRoot": "dummy-apps" },
          "run": { "id": "r1", "agentId": "TeamA_BackendDeveloper" }
        }
        """)!.AsObject());

    [Fact]
    public void Literal_IsReturnedUnchanged()
    {
        Assert.Equal("plain text", Engine.Render("plain text", Scope()));
        Assert.True(Engine.GetTemplate("plain text").IsLiteral);
    }

    [Fact]
    public void Template_RendersValues()
    {
        var text = Engine.Render("{{ steps.classify.category }} ({{ steps.classify.confidenceScore * 100 | math.round }}%)", Scope());

        Assert.Equal("BackendBug (92%)", text);
    }

    [Fact]
    public void SingleExpression_ReturnsTypedValues()
    {
        var scope = Scope();

        Assert.True(Engine.GetTemplate("{{ steps.classify.isCodeRelated }}").Evaluate(scope)!.GetValue<bool>());
        Assert.Equal(0.92, Engine.GetTemplate("{{ steps.classify.confidenceScore }}").Evaluate(scope)!.GetValue<double>());
        var obj = Assert.IsType<JsonObject>(Engine.GetTemplate("{{ steps.classify }}").Evaluate(scope));
        Assert.Equal("BackendBug", obj["category"]!.GetValue<string>());
        Assert.Null(Engine.GetTemplate("{{ steps.missing }}").Evaluate(scope));
    }

    [Fact]
    public void MixedTemplate_ReturnsString()
    {
        var value = Engine.GetTemplate("Category: {{ steps.classify.category }}").Evaluate(Scope());

        Assert.Equal("Category: BackendBug", value!.GetValue<string>());
    }

    [Theory]
    [InlineData("steps.classify.isCodeRelated", true)]
    [InlineData("steps.classify.isCodeRelated == false", false)]
    [InlineData("steps.approval.decision == \"approved\"", false)]
    [InlineData("steps.approval.decision == \"rejected\"", true)]
    [InlineData("steps.classify.confidenceScore > 0.9", true)]
    [InlineData("steps.missing.value == null", true)]
    [InlineData("input.subject | string.downcase | string.contains 'application a'", true)]
    [InlineData("(regex.match input.subject `Application\\s?A` | array.size) > 0", true)]
    [InlineData("steps.approval.reason", false)]
    public void Conditions_AreEvaluated(string expression, bool expected)
    {
        Assert.Equal(expected, Engine.EvaluateCondition(expression, Scope()));
    }

    [Fact]
    public void ObjectDefault_ProvidesFallbacks()
    {
        Assert.Equal("Rejected", Engine.Render("{{ steps.approval.reason | object.default 'Rejected' }}", Scope()));
    }

    [Fact]
    public void EvaluateValue_EvaluatesNestedTemplates()
    {
        var value = Engine.EvaluateValue(JsonNode.Parse("""
            { "path": "{{ config.dummyAppsRoot }}/a", "flag": "{{ steps.classify.isCodeRelated }}", "list": ["{{ run.id }}", 3] }
            """), Scope())!;

        Assert.Equal("dummy-apps/a", value["path"]!.GetValue<string>());
        Assert.True(value["flag"]!.GetValue<bool>());
        Assert.Equal("r1", value["list"]![0]!.GetValue<string>());
        Assert.Equal(3, value["list"]![1]!.GetValue<long>());
    }

    [Theory]
    [InlineData("{{ steps. }}")]
    [InlineData("{{ if x }}")]
    public void InvalidTemplates_AreRejected(string text)
    {
        Assert.False(Engine.TryCompileTemplate(text, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Theory]
    [InlineData("steps.x ==")]
    [InlineData("")]
    public void InvalidExpressions_AreRejected(string text)
    {
        Assert.False(Engine.TryCompileExpression(text, out _, out _));
    }

    [Fact]
    public void ReferencedSteps_AreCollected()
    {
        var template = Engine.GetTemplate("{{ steps.classify.category }} {{ if steps.approval.reason }}x{{ end }} {{ input.subject }}");

        Assert.Equal(["approval", "classify"], template.ReferencedSteps.Order());
    }

    [Fact]
    public void Include_FailsBecauseNoLoaderIsRegistered()
    {
        var ex = Assert.Throws<ExpressionException>(() => Engine.Render("{{ include 'secrets.txt' }}", Scope()));

        Assert.Contains("TemplateLoader", ex.Message);
    }

    [Fact]
    public void LoopLimit_StopsRunawayScripts()
    {
        Assert.Throws<ExpressionException>(() => Engine.Render("{{ for i in 1..100000 }}{{ end }}", Scope()));
    }

    [Fact]
    public void Booleans_RenderLowercase()
    {
        Assert.Equal("true", Engine.Render("{{ steps.classify.isCodeRelated }}", Scope()));
    }

    [Fact]
    public void WithAddsVariablesWithoutChangingTheOriginalScope()
    {
        var scope = Scope();
        var extended = scope.With("output", new JsonObject { ["role"] = "QAEngineer" });

        Assert.Equal("QAEngineer", Engine.Render("{{ output.role }}", extended));
        Assert.Equal("", Engine.Render("{{ output.role }}", scope));
    }
}
