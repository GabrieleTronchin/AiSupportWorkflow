namespace LlmFlows.Runtime.Execution;

using System.Diagnostics;
using System.Text.Json.Nodes;
using LlmFlows.Definitions.Expressions;
using LlmFlows.Definitions.Model;
using LlmFlows.Runtime.Steps;
using Microsoft.Agents.AI.Workflows;

/// <summary>
/// Compiles a workflow definition into an Agent Framework workflow graph for one run: one executor per step,
/// edges conditioned on <see cref="StepMessage.NextStepId"/>, outputs from terminal steps.
/// </summary>
public sealed class WorkflowCompiler(IEnumerable<IStepHandler> handlers, RunRecorder recorder)
{
    internal static readonly ActivitySource ActivitySource = new("LlmFlows.Runtime");

    private readonly IReadOnlyDictionary<string, IStepHandler> _handlers = handlers.ToDictionary(h => h.Kind);

    public Workflow Build(RunContext run)
    {
        var definition = run.Workflow;
        var executors = definition.Steps.ToDictionary(
            s => s.Id,
            s => s is TerminalStep terminal
                ? (Executor)new TerminalStepExecutor(terminal, run)
                : new StepExecutor(s, run, _handlers[s.Kind], recorder));

        var builder = new WorkflowBuilder(executors[definition.Start]);
        foreach (var step in definition.Steps)
        {
            foreach (var target in step.EffectiveTransitions.Select(t => t.To).Distinct())
            {
                var targetId = target;
                builder.AddEdge<StepMessage>(executors[step.Id], executors[targetId], condition: message => message?.NextStepId == targetId);
            }
        }

        builder.WithOutputFrom([.. definition.Steps.OfType<TerminalStep>().Select(t => executors[t.Id])]);
        return builder.Build();
    }
}

/// <summary>Runs a non-terminal step: records progress, executes the kind handler and selects the next step.</summary>
internal sealed class StepExecutor(StepDefinition step, RunContext run, IStepHandler handler, RunRecorder recorder) : Executor(step.Id)
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
    {
        protocolBuilder.RouteBuilder.AddHandler<StepMessage, StepMessage>(HandleAsync);
        return protocolBuilder;
    }

    private async ValueTask<StepMessage> HandleAsync(StepMessage message, IWorkflowContext context, CancellationToken ct)
    {
        using var activity = WorkflowCompiler.ActivitySource.StartActivity($"step {step.Id}");
        activity?.SetTag("llmflows.run_id", run.RunId);
        activity?.SetTag("llmflows.module_id", run.Module.Id);
        activity?.SetTag("llmflows.workflow_id", run.Workflow.Id);
        activity?.SetTag("llmflows.step_id", step.Id);
        activity?.SetTag("llmflows.step_kind", step.Kind);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, run.RunToken);
        var token = linked.Token;

        if (!run.EnterStep(step.Id))
        {
            throw new StepFailedException(step.Id, $"Run exceeded the maximum of {run.Workflow.EffectiveMaxSteps} steps.");
        }

        if (handler.RecordsStart)
        {
            await recorder.StepStartedAsync(run, step.Id);
        }

        try
        {
            var output = await handler.ExecuteAsync(step, run, token);
            run.SetOutput(step.Id, output);

            var scope = run.CreateScope();
            if (step.AssignAgent is not null)
            {
                var agentId = run.Templates.Render(step.AssignAgent, scope);
                if (run.Module.FindAgent(agentId) is null)
                {
                    throw new StepFailedException(step.Id, $"Agent '{agentId}' is not declared in module '{run.Module.Id}'.");
                }

                run.AssignAgent(agentId);
                scope = run.CreateScope();
            }

            var detail = step.Detail is null ? null : run.Templates.Render(step.Detail, scope);
            await recorder.StepCompletedAsync(run, step.Id, detail);

            return new StepMessage(SelectNext(scope));
        }
        catch (ExpressionException ex)
        {
            throw new StepFailedException(step.Id, $"Template error in step '{step.Id}': {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not StepFailedException && ex is not OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private string SelectNext(EvaluationScope scope)
    {
        foreach (var transition in step.EffectiveTransitions)
        {
            if (transition.When is null || run.Templates.EvaluateCondition(transition.When, scope))
            {
                return transition.To;
            }
        }

        throw new StepFailedException(step.Id, $"No transition matched after step '{step.Id}'.");
    }
}

/// <summary>Ends the run: yields the outcome, message and final output.</summary>
internal sealed class TerminalStepExecutor(TerminalStep step, RunContext run) : Executor(step.Id)
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
    {
        protocolBuilder.RouteBuilder.AddHandler<StepMessage>((Func<StepMessage, IWorkflowContext, CancellationToken, ValueTask>)HandleAsync);
        return protocolBuilder.YieldsOutput<RunOutcome>();
    }

    private async ValueTask HandleAsync(StepMessage message, IWorkflowContext context, CancellationToken ct)
    {
        run.EnterStep(step.Id);
        try
        {
            var scope = run.CreateScope();
            var text = step.Message is null ? null : run.Templates.Render(step.Message, scope);
            JsonNode? output = step.Output is null ? null : run.Templates.EvaluateValue(step.Output, scope);
            await context.YieldOutputAsync(new RunOutcome(step.Id, step.Outcome, text, output), ct);
        }
        catch (ExpressionException ex)
        {
            throw new StepFailedException(step.Id, $"Template error in step '{step.Id}': {ex.Message}", ex);
        }
    }
}
