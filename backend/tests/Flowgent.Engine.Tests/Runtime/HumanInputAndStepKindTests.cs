namespace Flowgent.Engine.Tests.Runtime;

using System.Text.Json.Nodes;
using Flowgent.Contracts.Runs;
using Flowgent.Engine.Tests.Helpers;
using Flowgent.Runtime.HumanInput;

public class HumanInputAndStepKindTests
{
    private static Task<TestEngine> StartReviewEngineAsync() => TestEngine.StartAsync(TestModules.AddReviewWorkflow);

    private static JsonObject Subject(string subject) => new() { ["subject"] = subject };

    [Fact]
    public async Task HumanInput_PausesRunWithReviewPayload()
    {
        await using var engine = await StartReviewEngineAsync();
        var runId = await engine.SubmitAsync(Subject("alpha docs"), "review");

        var execution = engine.Orchestrator.ExecuteAsync(runId);
        await engine.WaitForHumanInputAsync(runId);
        var run = await engine.WaitForStatusAsync(runId, RunStatus.AwaitingInput);

        Assert.Equal("approval", run.CurrentStepId);
        Assert.Equal("Waiting for review", run.Detail);
        Assert.Equal("demo_writer", run.AssignedAgentId);
        var pending = Assert.Single(engine.HumanInputs.GetPending());
        Assert.Equal("Review alpha docs", pending.Review.Title);
        Assert.Equal([("Team", "Alpha"), ("Role", "Writer")], pending.Review.Fields.Select(f => (f.Label, f.Value!)));
        Assert.Equal("alpha docs", pending.RunTitle);

        await engine.HumanInput.ApproveAsync(runId);
        await execution;
    }

    [Fact]
    public async Task Approve_ContinuesAlongTheApprovedTransition()
    {
        await using var engine = await StartReviewEngineAsync();
        var runId = await engine.SubmitAsync(Subject("alpha urgent"), "review");
        var execution = engine.Orchestrator.ExecuteAsync(runId);
        await engine.WaitForHumanInputAsync(runId);

        var (result, _) = await engine.HumanInput.ApproveAsync(runId);
        await execution;

        Assert.Equal(HumanInputResult.Completed, result);
        var run = (await engine.Store.GetAsync(runId))!;
        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal("done", run.CurrentStepId);
        Assert.Equal("URGENT Alpha", run.Detail);
        Assert.Equal("approved", run.StepOutputs["approval"]!["decision"]!.GetValue<string>());
        Assert.Empty(engine.HumanInputs.GetPending());
    }

    [Fact]
    public async Task Reject_FollowsTheDefaultTransitionWithReason()
    {
        await using var engine = await StartReviewEngineAsync();
        var runId = await engine.SubmitAsync(Subject("beta"), "review");
        var execution = engine.Orchestrator.ExecuteAsync(runId);
        await engine.WaitForHumanInputAsync(runId);

        await engine.HumanInput.RejectAsync(runId, "Needs more detail");
        await execution;

        var run = (await engine.Store.GetAsync(runId))!;
        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("declined", run.CurrentStepId);
        Assert.Equal("Needs more detail", run.Detail);
        var events = await engine.EventsAsync(runId);
        Assert.Contains(events, e => e.Kind == RunEventKind.InputReceived && e.Detail == "rejected: Needs more detail");
    }

    [Fact]
    public async Task RejectWithoutReason_UsesTheTemplateFallback()
    {
        await using var engine = await StartReviewEngineAsync();
        var runId = await engine.SubmitAsync(Subject("beta"), "review");
        var execution = engine.Orchestrator.ExecuteAsync(runId);
        await engine.WaitForHumanInputAsync(runId);

        await engine.HumanInput.RejectAsync(runId, "  ");
        await execution;

        Assert.Equal("Rejected", (await engine.Store.GetAsync(runId))!.Detail);
    }

    [Fact]
    public async Task Approve_UnknownRun_IsNotFound()
    {
        await using var engine = await StartReviewEngineAsync();

        var (result, error) = await engine.HumanInput.ApproveAsync(Guid.NewGuid());

        Assert.Equal(HumanInputResult.NotFound, result);
        Assert.Contains("not found", error);
    }

    [Fact]
    public async Task Approve_CompletedRun_IsConflict()
    {
        await using var engine = await StartReviewEngineAsync();
        var run = await engine.RunAsync(Subject("alpha beta"), "review");

        var (result, error) = await engine.HumanInput.ApproveAsync(run.Id);

        Assert.Equal(HumanInputResult.Conflict, result);
        Assert.Contains("already completed", error);
    }

    [Fact]
    public async Task Approve_QueuedRun_IsConflict()
    {
        await using var engine = await StartReviewEngineAsync();
        var runId = await engine.SubmitAsync(Subject("alpha"), "review");

        var (result, error) = await engine.HumanInput.ApproveAsync(runId);

        Assert.Equal(HumanInputResult.Conflict, result);
        Assert.Contains("not awaiting input", error);
    }

    [Fact]
    public async Task Abort_ReleasesPendingInputAndRecordsSingleFailedTransition()
    {
        await using var engine = await StartReviewEngineAsync();
        var runId = await engine.SubmitAsync(Subject("alpha"), "review");
        var execution = engine.Orchestrator.ExecuteAsync(runId);
        await engine.WaitForHumanInputAsync(runId);

        Assert.True(engine.Registry.TryAbort(runId));
        await execution;

        var run = (await engine.Store.GetAsync(runId))!;
        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("Aborted by user", run.Detail);
        Assert.Equal("approval", run.CurrentStepId);
        var events = await engine.EventsAsync(runId);
        Assert.Single(events, e => e.Kind == RunEventKind.Failed);
        Assert.DoesNotContain(events, e => e.Kind == RunEventKind.InputReceived);
        Assert.Empty(engine.HumanInputs.GetPending());
        Assert.False(engine.Registry.IsRunning(runId));
    }

    [Fact]
    public async Task HostShutdown_CancelsTheRun()
    {
        await using var engine = await StartReviewEngineAsync();
        var runId = await engine.SubmitAsync(Subject("alpha"), "review");
        using var host = new CancellationTokenSource();
        var execution = engine.Orchestrator.ExecuteAsync(runId, host.Token);
        await engine.WaitForHumanInputAsync(runId);

        await host.CancelAsync();
        var result = await execution;

        Assert.Equal(RunStatus.Cancelled, result.Status);
        var run = (await engine.Store.GetAsync(runId))!;
        Assert.Equal(RunStatus.Cancelled, run.Status);
        Assert.Equal("Workflow was cancelled.", run.Detail);
    }

    [Theory]
    [InlineData("alpha and beta", "Ambiguous: both alpha and beta.")]
    [InlineData("gamma", "No team for 'gamma'.")]
    public async Task RuleStep_FailsWithDeclaredMessages(string subject, string expected)
    {
        await using var engine = await StartReviewEngineAsync();

        var run = await engine.RunAsync(Subject(subject), "review");

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("route", run.CurrentStepId);
        Assert.Equal(expected, run.Detail);
    }

    [Theory]
    [InlineData("ALPHA", "Alpha")]
    [InlineData("about Beta", "Beta")]
    public async Task RuleStep_MatchesCaseInsensitivelyByDefault(string subject, string team)
    {
        await using var engine = await StartReviewEngineAsync();
        var runId = await engine.SubmitAsync(Subject(subject), "review");
        var execution = engine.Orchestrator.ExecuteAsync(runId);
        await engine.WaitForHumanInputAsync(runId);

        var run = (await engine.Store.GetAsync(runId))!;
        Assert.Equal(team, run.StepOutputs["route"]!["team"]!.GetValue<string>());
        engine.Registry.TryAbort(runId);
        await execution;
    }

    [Fact]
    public async Task RuleStep_FirstMatchWinsWhenMultipleMatchesAreAllowed()
    {
        await using var engine = await TestEngine.StartAsync(f =>
        {
            TestModules.AddReviewWorkflow(f);
            f.Replace("workflows/review.yaml", "onMultipleMatches: { fail: \"Ambiguous: both alpha and beta.\" }", "onMultipleMatches: first");
        });
        var runId = await engine.SubmitAsync(Subject("beta alpha"), "review");
        var execution = engine.Orchestrator.ExecuteAsync(runId);
        await engine.WaitForHumanInputAsync(runId);

        Assert.Equal("Alpha", (await engine.Store.GetAsync(runId))!.StepOutputs["route"]!["team"]!.GetValue<string>());
        engine.Registry.TryAbort(runId);
        await execution;
    }

    [Fact]
    public async Task RuleStep_NoMatchCanOutputAFallback()
    {
        await using var engine = await TestEngine.StartAsync(f =>
        {
            TestModules.AddReviewWorkflow(f);
            f.Replace("workflows/review.yaml", "onNoMatch: { fail: \"No team for '{{ input.subject }}'.\" }", "onNoMatch: { output: { team: Beta } }");
        });
        var runId = await engine.SubmitAsync(Subject("gamma"), "review");
        var execution = engine.Orchestrator.ExecuteAsync(runId);
        await engine.WaitForHumanInputAsync(runId);

        Assert.Equal("Beta", (await engine.Store.GetAsync(runId))!.StepOutputs["route"]!["team"]!.GetValue<string>());
        engine.Registry.TryAbort(runId);
        await execution;
    }

    [Fact]
    public async Task MapStep_ComputesOutputsInOrderAndFailsOnMissingLookup()
    {
        await using var engine = await TestEngine.StartAsync(f =>
        {
            TestModules.AddReviewWorkflow(f);
            f.Replace("workflows/review.yaml", "roleByTeam: { Alpha: Writer, Beta: Reviewer }", "roleByTeam: { Alpha: Writer }");
        });

        var beta = await engine.RunAsync(Subject("beta"), "review");
        Assert.Equal(RunStatus.Failed, beta.Status);
        Assert.Equal("assign", beta.CurrentStepId);
        Assert.Equal("No role for team Beta.", beta.Detail);

        var runId = await engine.SubmitAsync(Subject("alpha"), "review");
        var execution = engine.Orchestrator.ExecuteAsync(runId);
        await engine.WaitForHumanInputAsync(runId);
        var alpha = (await engine.Store.GetAsync(runId))!;
        Assert.Equal("Alpha-Writer", alpha.StepOutputs["assign"]!["label"]!.GetValue<string>());
        engine.Registry.TryAbort(runId);
        await execution;
    }

    [Fact]
    public async Task ConcurrentRuns_AreIsolated()
    {
        await using var engine = await StartReviewEngineAsync();
        var subjects = Enumerable.Range(0, 20).Select(i => i % 2 == 0 ? $"alpha {i}" : $"beta urgent {i}").ToList();

        var runIds = new List<Guid>();
        foreach (var subject in subjects)
        {
            runIds.Add(await engine.SubmitAsync(Subject(subject), "review"));
        }

        var executions = runIds.Select(id => engine.Orchestrator.ExecuteAsync(id)).ToList();
        foreach (var id in runIds)
        {
            await engine.WaitForHumanInputAsync(id);
        }

        Assert.Equal(20, engine.HumanInputs.GetPending().Count);
        foreach (var id in runIds)
        {
            await engine.HumanInput.ApproveAsync(id);
        }

        await Task.WhenAll(executions);

        for (var i = 0; i < subjects.Count; i++)
        {
            var run = (await engine.Store.GetAsync(runIds[i]))!;
            Assert.Equal(RunStatus.Completed, run.Status);
            Assert.Equal(subjects[i], run.Title);
            Assert.Equal(i % 2 == 0 ? "normal Alpha" : "URGENT Beta", run.Detail);
        }
    }

    [Fact]
    public async Task DemoMode_StepWithoutDemo_FailsWithDescriptiveError()
    {
        await using var engine = await TestEngine.StartAsync(f =>
            f.Replace("workflows/hello.yaml", "    demo:\n      output: { summary: \"[Demo] {{ input.text }}\" }\n", ""));

        var run = await engine.RunAsync(new JsonObject { ["text"] = "hi" });

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("summarize", run.CurrentStepId);
        Assert.Equal("Step 'summarize' has no demo response; Demo mode requires a demo: block.", run.Detail);
    }

    [Fact]
    public async Task Loop_IsStoppedByMaxSteps()
    {
        await using var engine = await TestEngine.StartAsync(f => f["workflows/hello.yaml"] = """
            id: hello
            version: 1
            name: Loop
            input: { schema: { type: object } }
            maxSteps: 5
            start: a
            steps:
              - id: a
                kind: map
                name: A
                outputs: [{ name: x, value: 1 }]
                transitions:
                  - when: "true"
                    to: a
                  - to: end
              - id: end
                kind: terminal
                name: End
                outcome: success
            """);

        var run = await engine.RunAsync([]);

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("Run exceeded the maximum of 5 steps.", run.Detail);
    }

    [Fact]
    public async Task UndeclaredAssignedAgent_FailsTheStep()
    {
        await using var engine = await TestEngine.StartAsync(f =>
        {
            TestModules.AddReviewWorkflow(f);
            f.Replace("workflows/review.yaml", "assignAgent: demo_writer", "assignAgent: \"{{ steps.route.team }}_agent\"");
        });

        var run = await engine.RunAsync(Subject("alpha"), "review");

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("Agent 'Alpha_agent' is not declared in module 'demo'.", run.Detail);
    }
}
