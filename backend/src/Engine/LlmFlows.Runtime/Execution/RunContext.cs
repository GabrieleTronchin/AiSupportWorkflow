namespace LlmFlows.Runtime.Execution;

using System.Text.Json.Nodes;
using LlmFlows.Definitions;
using LlmFlows.Definitions.Expressions;
using LlmFlows.Definitions.Model;

/// <summary>
/// The state of one run: input, outputs of completed steps, assigned agent and the definition version it runs.
/// Each run has its own context (and its own executors), so concurrent runs never share state.
/// </summary>
public sealed class RunContext
{
    private readonly Lock _lock = new();
    private readonly JsonObject _stepOutputs;

    public RunContext(Guid runId, ModuleDefinition module, WorkflowDefinition workflow, JsonObject input, string? title, CancellationToken runToken)
    {
        RunId = runId;
        Module = module;
        Workflow = workflow;
        Input = input;
        Title = title;
        RunToken = runToken;
        _stepOutputs = [];
    }

    public Guid RunId { get; }

    public ModuleDefinition Module { get; }

    public WorkflowDefinition Workflow { get; }

    public JsonObject Input { get; }

    public string? Title { get; }

    /// <summary>Cancelled on abort or host shutdown.</summary>
    public CancellationToken RunToken { get; }

    public string? AssignedAgentId { get; private set; }

    public string? CurrentStepId { get; private set; }

    public int ExecutedSteps { get; private set; }

    public TemplateEngine Templates => Module.Templates;

    /// <summary>Moves the run to a step; returns false when the step budget is exhausted.</summary>
    public bool EnterStep(string stepId)
    {
        lock (_lock)
        {
            CurrentStepId = stepId;
            ExecutedSteps++;
            return ExecutedSteps <= Workflow.EffectiveMaxSteps;
        }
    }

    public void SetOutput(string stepId, JsonNode? output)
    {
        lock (_lock)
        {
            _stepOutputs[stepId] = output?.DeepClone();
        }
    }

    public JsonNode? GetOutput(string stepId)
    {
        lock (_lock)
        {
            return _stepOutputs[stepId]?.DeepClone();
        }
    }

    public JsonObject SnapshotOutputs()
    {
        lock (_lock)
        {
            return _stepOutputs.DeepClone().AsObject();
        }
    }

    public void AssignAgent(string agentId)
    {
        lock (_lock)
        {
            AssignedAgentId = agentId;
        }
    }

    /// <summary>Data visible to templates: input, steps, config and run metadata.</summary>
    public EvaluationScope CreateScope()
    {
        lock (_lock)
        {
            var agent = AssignedAgentId is null ? null : Module.FindAgent(AssignedAgentId);
            return new EvaluationScope(new JsonObject
            {
                ["input"] = Input.DeepClone(),
                ["steps"] = _stepOutputs.DeepClone(),
                ["config"] = Module.Config.DeepClone(),
                ["run"] = new JsonObject
                {
                    ["id"] = RunId.ToString(),
                    ["moduleId"] = Module.Id,
                    ["workflowId"] = Workflow.Id,
                    ["title"] = Title,
                    ["agentId"] = AssignedAgentId,
                    ["agent"] = agent is null ? null : new JsonObject
                    {
                        ["id"] = agent.Id,
                        ["name"] = agent.Name,
                        ["group"] = agent.Group,
                        ["role"] = agent.Role,
                        ["persona"] = agent.Persona,
                    },
                },
            });
        }
    }

    /// <summary>Scope used before a run exists (intake: run title).</summary>
    public static EvaluationScope CreateIntakeScope(ModuleDefinition module, WorkflowDefinition workflow, JsonObject input, Guid runId) =>
        new(new JsonObject
        {
            ["input"] = input.DeepClone(),
            ["steps"] = new JsonObject(),
            ["config"] = module.Config.DeepClone(),
            ["run"] = new JsonObject { ["id"] = runId.ToString(), ["moduleId"] = module.Id, ["workflowId"] = workflow.Id },
        });
}

/// <summary>The only message exchanged by step executors: which step runs next.</summary>
public sealed record StepMessage(string NextStepId);

/// <summary>Output yielded by terminal steps.</summary>
public sealed record RunOutcome(string StepId, TerminalOutcome Outcome, string? Message, JsonNode? Output);

/// <summary>A step failed for an expected reason (validation, rule, tool or LLM error); the run ends as Failed.</summary>
public sealed class StepFailedException(string stepId, string message, Exception? inner = null) : Exception(message, inner)
{
    public string StepId { get; } = stepId;
}
