namespace Flowgent.Definitions.Validation;

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using Flowgent.Definitions.Expressions;
using Flowgent.Definitions.Model;
using Flowgent.Definitions.Schemas;
using Flowgent.Definitions.Yaml;

/// <summary>Semantic checks that the format schemas cannot express (references, graph, templates, schemas).</summary>
internal static class DefinitionValidator
{
    private static readonly TemplateEngine Templates = TemplateEngine.Shared;

    public static void Validate(
        ModuleManifest manifest,
        IReadOnlyList<WorkflowDocument> workflows,
        IReadOnlyList<SampleDocument> samples,
        IReadOnlyDictionary<string, string> files,
        Dictionary<string, (JsonObject, JsonSchema)> schemas,
        DefinitionErrors errors)
    {
        var context = new ModuleContext(manifest, files, schemas, errors);

        ValidateManifest(context);

        var workflowIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var workflow in workflows)
        {
            if (!workflowIds.Add(workflow.Definition.Id))
            {
                errors.Add(workflow.Definition.File, null, workflow.Lines.Find("/id"), $"Workflow id '{workflow.Definition.Id}' is declared more than once.");
            }

            new WorkflowValidator(context, workflow).Validate();
        }

        ValidateSamples(context, workflows, samples);
    }

    private static void ValidateManifest(ModuleContext context)
    {
        var manifest = context.Manifest;
        const string file = ModuleManifest.FileName;

        foreach (var duplicate in manifest.Agents.GroupBy(a => a.Id).Where(g => g.Count() > 1))
        {
            context.Errors.Add(file, null, null, $"Agent '{duplicate.Key}' is declared more than once.");
        }

        foreach (var duplicate in manifest.Tools.GroupBy(t => t.Name).Where(g => g.Count() > 1))
        {
            context.Errors.Add(file, null, null, $"Tool '{duplicate.Key}' is declared more than once.");
        }

        foreach (var tool in manifest.Tools)
        {
            if (!tool.Name.StartsWith(manifest.Id + ".", StringComparison.Ordinal))
            {
                context.Errors.Add(file, null, null, $"Tool '{tool.Name}' must be prefixed with the module id ('{manifest.Id}.').");
            }

            if (tool.Input is not null)
            {
                context.RegisterSchema(tool.Input.Schema, file, null, $"input schema of tool '{tool.Name}'");
            }

            if (tool.Output is not null)
            {
                context.RegisterSchema(tool.Output.Schema, file, null, $"output schema of tool '{tool.Name}'");
            }
        }
    }

    private static void ValidateSamples(ModuleContext context, IReadOnlyList<WorkflowDocument> workflows, IReadOnlyList<SampleDocument> samples)
    {
        foreach (var duplicate in samples.GroupBy(s => s.Sample.Id).Where(g => g.Count() > 1))
        {
            context.Errors.Add(duplicate.First().File, null, null, $"Sample '{duplicate.Key}' is declared more than once.");
        }

        foreach (var doc in samples)
        {
            var sample = doc.Sample;
            var line = doc.Lines.Find($"/samples/{doc.Index}");
            var workflow = workflows.FirstOrDefault(w => w.Definition.Id == sample.Workflow)?.Definition;
            if (workflow is null)
            {
                context.Errors.Add(doc.File, null, line, $"Sample '{sample.Id}' references unknown workflow '{sample.Workflow}'.");
                continue;
            }

            if (!context.TryGetSchema(workflow.Input.Schema, out var schema))
            {
                continue;
            }

            var schemaErrors = JsonSchemaValidator.Validate(schema, sample.Input ?? []);
            if (schemaErrors.Count > 0 && !sample.ExpectInvalid)
            {
                context.Errors.Add(doc.File, null, line,
                    $"Sample '{sample.Id}' does not match the input schema of '{workflow.Id}': {string.Join("; ", schemaErrors)}");
            }
            else if (schemaErrors.Count == 0 && sample.ExpectInvalid)
            {
                context.Errors.Warn(doc.File, null, line, $"Sample '{sample.Id}' is marked expectInvalid but matches the input schema.");
            }
        }
    }

    internal sealed class ModuleContext(
        ModuleManifest manifest,
        IReadOnlyDictionary<string, string> files,
        Dictionary<string, (JsonObject, JsonSchema)> schemas,
        DefinitionErrors errors)
    {
        public ModuleManifest Manifest { get; } = manifest;

        public IReadOnlyDictionary<string, string> Files { get; } = files;

        public DefinitionErrors Errors { get; } = errors;

        public bool TryGetSchema(SchemaSource source, out JsonSchema schema)
        {
            if (schemas.TryGetValue(source.Key, out var entry))
            {
                schema = entry.Item2;
                return true;
            }

            schema = null!;
            return false;
        }

        public JsonObject? RegisterSchema(SchemaSource source, string file, string? stepId, string what, int? line = null)
        {
            if (schemas.TryGetValue(source.Key, out var existing))
            {
                return existing.Item1;
            }

            JsonObject? node = source.Inline;
            if (source.File is { } path)
            {
                if (!DefinitionPaths.IsSafe(path) || !Files.TryGetValue(DefinitionPaths.Normalize(path), out var text))
                {
                    Errors.Add(file, stepId, line, $"Schema file '{path}' for the {what} does not exist.");
                    return null;
                }

                try
                {
                    node = JsonNode.Parse(text) as JsonObject;
                }
                catch (System.Text.Json.JsonException ex)
                {
                    Errors.Add(DefinitionPaths.Normalize(path), stepId, null, $"Invalid JSON in schema file '{path}': {ex.Message}");
                    return null;
                }

                if (node is null)
                {
                    Errors.Add(DefinitionPaths.Normalize(path), stepId, null, $"Schema file '{path}' must contain a JSON object.");
                    return null;
                }
            }

            try
            {
                var schema = JsonSchemaValidator.Parse(node!);
                schemas[source.Key] = (node!, schema);
                return node;
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or ArgumentException or NotSupportedException)
            {
                Errors.Add(source.File ?? file, stepId, line, $"Invalid JSON Schema for the {what}: {ex.Message}");
                return null;
            }
        }

        public string? ResolveText(TextSource source, string file, string? stepId, string what, int? line)
        {
            if (source.Inline is not null)
            {
                return source.Inline;
            }

            if (!DefinitionPaths.IsSafe(source.File!) || !Files.TryGetValue(DefinitionPaths.Normalize(source.File!), out var text))
            {
                Errors.Add(file, stepId, line, $"File '{source.File}' for the {what} does not exist.");
                return null;
            }

            return text;
        }
    }

    private sealed class WorkflowValidator(ModuleContext context, WorkflowDocument document)
    {
        private readonly WorkflowDefinition _workflow = document.Definition;
        private readonly LineMap _lines = document.Lines;
        private readonly HashSet<string> _stepIds = new(StringComparer.Ordinal);

        private string File => _workflow.File;

        public void Validate()
        {
            for (var i = 0; i < _workflow.Steps.Count; i++)
            {
                var step = _workflow.Steps[i];
                if (!_stepIds.Add(step.Id))
                {
                    Error(step, i, "/id", $"Step id '{step.Id}' is declared more than once.");
                }
            }

            if (!_stepIds.Contains(_workflow.Start))
            {
                context.Errors.Add(File, null, _lines.Find("/start"), $"Start step '{_workflow.Start}' does not exist.");
            }

            context.RegisterSchema(_workflow.Input.Schema, File, null, "workflow input", _lines.Find("/input/schema"));
            CheckTemplate(_workflow.Title, null, -1, "/title", "title");

            for (var i = 0; i < _workflow.Steps.Count; i++)
            {
                ValidateStep(_workflow.Steps[i], i);
            }

            ValidateGraph();
        }

        private void ValidateStep(StepDefinition step, int index)
        {
            if (step is TerminalStep)
            {
                if (step.Next is not null || step.Transitions is { Count: > 0 })
                {
                    Error(step, index, "", "Terminal steps cannot declare 'next' or 'transitions'.");
                }
            }
            else if (step.Next is not null && step.Transitions is { Count: > 0 })
            {
                Error(step, index, "/next", "Declare either 'next' or 'transitions', not both.");
            }
            else if (step.EffectiveTransitions.Count == 0)
            {
                Error(step, index, "", "Non-terminal steps must declare 'next' or 'transitions'.");
            }

            var transitions = step.EffectiveTransitions;
            for (var t = 0; t < transitions.Count; t++)
            {
                var transition = transitions[t];
                var pointer = step.Next is not null ? "/next" : $"/transitions/{t}";
                if (!_stepIds.Contains(transition.To))
                {
                    Error(step, index, pointer, $"Transition target '{transition.To}' does not exist.");
                }

                if (transition.When is not null)
                {
                    CheckExpression(transition.When, step, index, $"{pointer}/when", "transition condition");
                }
                else if (t < transitions.Count - 1)
                {
                    Warn(step, index, pointer, "A transition without condition should be the last one; the following transitions are never used.");
                }
            }

            CheckTemplate(step.Detail, step, index, "/detail", "detail");
            CheckTemplate(step.AssignAgent, step, index, "/assignAgent", "assignAgent");
            CheckAgentLiteral(step.AssignAgent, step, index, "/assignAgent");

            switch (step)
            {
                case LlmStep llm:
                    ValidateLlm(llm, index);
                    break;
                case RuleStep rule:
                    ValidateRules(rule.Value, rule.Rules, rule.IgnoreCase, step, index, "");
                    CheckTemplate(rule.OnMultipleMatches?.Fail, step, index, "/onMultipleMatches/fail", "failure message");
                    CheckTemplate(rule.OnNoMatch?.Fail, step, index, "/onNoMatch/fail", "failure message");
                    CheckValue(rule.OnNoMatch?.Output, step, index, "/onNoMatch/output");
                    break;
                case MapStep map:
                    ValidateMap(map, index);
                    break;
                case HumanInputStep human:
                    CheckTemplate(human.Review.Title, step, index, "/review/title", "review title");
                    for (var f = 0; f < human.Review.Fields.Count; f++)
                    {
                        CheckTemplate(human.Review.Fields[f].Value, step, index, $"/review/fields/{f}/value", "review field");
                    }

                    break;
                case ToolStep tool:
                    if (context.Manifest.Tools.All(t => t.Name != tool.Tool))
                    {
                        Error(step, index, "/tool", $"Tool '{tool.Tool}' is not declared in {ModuleManifest.FileName}.");
                    }

                    CheckValue(tool.Input, step, index, "/input");
                    break;
                case HttpStep http:
                    CheckTemplate(http.Url, step, index, "/url", "url");
                    CheckValue(http.Body, step, index, "/body");
                    foreach (var (name, header) in http.Headers ?? [])
                    {
                        CheckTemplate(header.Literal, step, index, $"/headers/{name}", "header");
                    }

                    if (http.Output is not null)
                    {
                        context.RegisterSchema(http.Output.Schema, File, step.Id, "http output", Line(index, "/output"));
                    }

                    break;
                case TerminalStep terminal:
                    CheckTemplate(terminal.Message, step, index, "/message", "message");
                    CheckValue(terminal.Output, step, index, "/output");
                    break;
            }
        }

        private void ValidateLlm(LlmStep llm, int index)
        {
            if (llm.SystemPrompt is not null && context.ResolveText(llm.SystemPrompt, File, llm.Id, "system prompt", Line(index, "/systemPrompt")) is { } system)
            {
                CheckTemplate(system, llm, index, "/systemPrompt", "system prompt");
            }

            if (context.ResolveText(llm.Prompt, File, llm.Id, "prompt", Line(index, "/prompt")) is { } prompt)
            {
                CheckTemplate(prompt, llm, index, "/prompt", "prompt");
            }

            var schema = context.RegisterSchema(llm.Output.Schema, File, llm.Id, "llm output", Line(index, "/output"));
            if (schema is not null && (schema["type"]?.GetValue<string>() != "object" || schema["additionalProperties"] is not JsonValue ap || ap.GetValue<bool>()))
            {
                Error(llm, index, "/output", "The LLM output schema must be an object schema with 'additionalProperties: false' (structured output).");
            }

            CheckTemplate(llm.Agent, llm, index, "/agent", "agent");
            CheckAgentLiteral(llm.Agent, llm, index, "/agent");

            if (llm.Demo is null)
            {
                Warn(llm, index, "", "No demo response: the step fails in Demo mode.");
                return;
            }

            CheckValue(llm.Demo.Output, llm, index, "/demo/output");
            CheckValue(llm.Demo.Default, llm, index, "/demo/default");
            if (llm.Demo.Rules is not null)
            {
                ValidateRules(llm.Demo.Value ?? "", llm.Demo.Rules, ignoreCase: true, llm, index, "/demo");
            }

            if (llm.Demo.Tool is not null && context.Manifest.Tools.All(t => t.Name != llm.Demo.Tool))
            {
                Error(llm, index, "/demo/tool", $"Tool '{llm.Demo.Tool}' is not declared in {ModuleManifest.FileName}.");
            }
        }

        private void ValidateRules(string value, IReadOnlyList<RuleDefinition> rules, bool ignoreCase, StepDefinition step, int index, string prefix)
        {
            CheckTemplate(value, step, index, $"{prefix}/value", "rule value");
            for (var r = 0; r < rules.Count; r++)
            {
                var rule = rules[r];
                var pointer = $"{prefix}/rules/{r}";
                if (rule.Match is not null)
                {
                    try
                    {
                        _ = new Regex(rule.Match, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None, TimeSpan.FromSeconds(1));
                    }
                    catch (ArgumentException ex)
                    {
                        Error(step, index, $"{pointer}/match", $"Invalid regular expression '{rule.Match}': {ex.Message}");
                    }
                }

                if (rule.When is not null)
                {
                    CheckExpression(rule.When, step, index, $"{pointer}/when", "rule condition");
                }

                CheckValue(rule.Output, step, index, $"{pointer}/output");
            }
        }

        private void ValidateMap(MapStep map, int index)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var o = 0; o < map.Outputs.Count; o++)
            {
                var output = map.Outputs[o];
                var pointer = $"/outputs/{o}";
                if (!names.Add(output.Name))
                {
                    Error(map, index, pointer, $"Output '{output.Name}' is declared more than once.");
                }

                if (output.Lookup is not null && map.Lookups?.ContainsKey(output.Lookup) != true)
                {
                    Error(map, index, $"{pointer}/lookup", $"Lookup table '{output.Lookup}' is not declared in 'lookups'.");
                }

                CheckValue(output.Value, map, index, $"{pointer}/value");
                CheckTemplate(output.Key, map, index, $"{pointer}/key", "lookup key");
                CheckTemplate(output.OnMissing, map, index, $"{pointer}/onMissing", "failure message");
            }
        }

        private void ValidateGraph()
        {
            var steps = _workflow.Steps.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
            if (!steps.ContainsKey(_workflow.Start))
            {
                return;
            }

            var reachable = new HashSet<string>(StringComparer.Ordinal) { _workflow.Start };
            var queue = new Queue<string>([_workflow.Start]);
            while (queue.TryDequeue(out var id))
            {
                foreach (var target in steps[id].EffectiveTransitions.Select(t => t.To))
                {
                    if (steps.ContainsKey(target) && reachable.Add(target))
                    {
                        queue.Enqueue(target);
                    }
                }
            }

            for (var i = 0; i < _workflow.Steps.Count; i++)
            {
                if (!reachable.Contains(_workflow.Steps[i].Id))
                {
                    Error(_workflow.Steps[i], i, "", $"Step '{_workflow.Steps[i].Id}' is not reachable from the start step.");
                }
            }

            if (!reachable.Any(id => steps[id] is TerminalStep))
            {
                context.Errors.Add(File, null, _lines.Find("/steps"), "No terminal step is reachable from the start step.");
            }
        }

        private void CheckAgentLiteral(string? agent, StepDefinition step, int index, string pointer)
        {
            if (agent is not null && !TemplateEngine.IsTemplate(agent) && context.Manifest.Agents.All(a => a.Id != agent))
            {
                Error(step, index, pointer, $"Agent '{agent}' is not declared in {ModuleManifest.FileName}.");
            }
        }

        private void CheckValue(JsonNode? value, StepDefinition step, int index, string pointer)
        {
            switch (value)
            {
                case JsonObject obj:
                    foreach (var (key, item) in obj)
                    {
                        CheckValue(item, step, index, $"{pointer}/{key}");
                    }

                    break;
                case JsonArray array:
                    for (var i = 0; i < array.Count; i++)
                    {
                        CheckValue(array[i], step, index, $"{pointer}/{i}");
                    }

                    break;
                case JsonValue v when v.TryGetValue<string>(out var text):
                    CheckTemplate(text, step, index, pointer, "value");
                    break;
            }
        }

        private void CheckTemplate(string? text, StepDefinition? step, int index, string pointer, string what)
        {
            if (text is null)
            {
                return;
            }

            if (!Templates.TryCompileTemplate(text, out var template, out var error))
            {
                Report(step, index, pointer, $"Invalid {what} template: {error}");
                return;
            }

            CheckReferences(template!.ReferencedSteps, step, index, pointer);
        }

        private void CheckExpression(string text, StepDefinition step, int index, string pointer, string what)
        {
            if (!Templates.TryCompileExpression(text, out var expression, out var error))
            {
                Error(step, index, pointer, $"Invalid {what}: {error}");
                return;
            }

            CheckReferences(expression!.ReferencedSteps, step, index, pointer);
        }

        private void CheckReferences(IReadOnlyCollection<string> referenced, StepDefinition? step, int index, string pointer)
        {
            foreach (var id in referenced.Where(id => !_stepIds.Contains(id)))
            {
                Report(step, index, pointer, $"Unknown step 'steps.{id}' referenced.");
            }
        }

        private void Report(StepDefinition? step, int index, string pointer, string message)
        {
            if (step is null)
            {
                context.Errors.Add(File, null, _lines.Find(pointer), message);
            }
            else
            {
                Error(step, index, pointer, message);
            }
        }

        private int? Line(int index, string pointer) => _lines.Find($"/steps/{index}{pointer}");

        private void Error(StepDefinition step, int index, string pointer, string message) =>
            context.Errors.Add(File, step.Id, Line(index, pointer), message);

        private void Warn(StepDefinition step, int index, string pointer, string message) =>
            context.Errors.Warn(File, step.Id, Line(index, pointer), message);
    }
}
