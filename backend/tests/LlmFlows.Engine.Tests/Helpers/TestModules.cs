namespace LlmFlows.Engine.Tests.Helpers;

using LlmFlows.Contracts.Modules;
using LlmFlows.Definitions;

/// <summary>A small valid module used by definition and runtime tests; tests patch individual files.</summary>
public static class TestModules
{
    public const string Manifest = """
        id: demo
        name: Demo
        description: Test module.
        config:
          greeting: Hello
        agents:
          - id: demo_writer
            name: Writer
            group: Writers
            role: Writer
            persona: A concise writer.
        tools:
          - name: demo.count
            input:
              schema: { type: object, required: [text], properties: { text: { type: string } } }
            output:
              schema: { type: object, required: [words], properties: { words: { type: integer } } }
        workflows:
          - workflows/hello.yaml
        samples:
          - samples/hello.yaml
        """;

    public const string Workflow = """
        id: hello
        version: 1
        name: Hello
        title: "{{ input.text }}"
        input:
          schema:
            type: object
            required: [text]
            properties:
              text: { type: string, minLength: 1 }
          errorMessage: Text is required.
        start: summarize
        steps:
          - id: summarize
            kind: llm
            name: Summary
            agent: demo_writer
            temperature: 0.2
            systemPrompt: { file: prompts/summarize.md }
            prompt: "Summarise {{ input.text }}"
            output:
              schema: { file: schemas/summary.schema.json }
            demo:
              output: { summary: "[Demo] {{ input.text }}" }
            detail: "{{ steps.summarize.summary }}"
            next: count
          - id: count
            kind: tool
            name: Count
            tool: demo.count
            input: { text: "{{ steps.summarize.summary }}" }
            transitions:
              - when: steps.count.words > 3
                to: long
              - to: short
          - id: long
            kind: terminal
            name: Long
            outcome: success
            message: "{{ steps.summarize.summary }}"
            output: "{{ steps.summarize }}"
          - id: short
            kind: terminal
            name: Short
            outcome: failure
            message: Too short
        """;

    public const string SummarySchema = """
        {"type":"object","additionalProperties":false,"required":["summary"],"properties":{"summary":{"type":"string"}}}
        """;

    public const string Samples = """
        samples:
          - id: s1
            name: Sample one
            group: Examples
            workflow: hello
            input: { text: "Agent Framework workflows" }
          - id: empty
            name: Empty
            workflow: hello
            input: { text: "" }
            expectInvalid: true
        """;

    public static Dictionary<string, string> Files() => new()
    {
        ["module.yaml"] = Manifest,
        ["workflows/hello.yaml"] = Workflow,
        ["schemas/summary.schema.json"] = SummarySchema,
        ["prompts/summarize.md"] = "You are {{ run.agent.persona }}",
        ["samples/hello.yaml"] = Samples,
    };

    public static ModulePackage Package(Action<Dictionary<string, string>>? patch = null, string moduleId = "demo")
    {
        var files = Files();
        patch?.Invoke(files);
        return new ModulePackage(moduleId, ModuleLoader.ComputeVersion(files), files);
    }

    public static LoadResult Load(Action<Dictionary<string, string>>? patch = null) => ModuleLoader.Load(Package(patch));

    public static ModuleDefinition LoadValid(Action<Dictionary<string, string>>? patch = null)
    {
        var result = Load(patch);
        return result.Module ?? throw new InvalidOperationException(
            "Module is invalid: " + string.Join(" | ", result.Errors.Select(e => $"{e.File}:{e.Line} {e.StepId} {e.Message}")));
    }

    /// <summary>Replaces text in a file (fails if the text is not found, so tests cannot silently pass).</summary>
    public static void Replace(this Dictionary<string, string> files, string file, string oldText, string newText)
    {
        if (!files[file].Contains(oldText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Text not found in {file}: {oldText}");
        }

        files[file] = files[file].Replace(oldText, newText, StringComparison.Ordinal);
    }
}
