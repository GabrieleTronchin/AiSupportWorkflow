namespace AiSupportWorkflow.Infrastructure.WorkflowEngine;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

internal sealed class WorkflowOrchestrator(
    SupportWorkflowFactory workflowFactory,
    IEmailProcessor emailProcessor,
    IWorkflowStateTracker stateTracker,
    WorkflowRunRegistry runRegistry,
    ILogger<WorkflowOrchestrator> logger) : IOrchestrator
{
    public async Task<WorkflowResult> ProcessIssueAsync(IncomingEmail email, CancellationToken ct = default)
    {
        var issueResult = emailProcessor.Process(email);
        if (!issueResult.IsSuccess)
        {
            logger.LogWarning("Email validation failed: {Error}", issueResult.Error);
            return WorkflowResult.Failed(Guid.Empty, issueResult.Error!);
        }

        var issue = issueResult.Value!;
        logger.LogInformation("Processing issue {IssueId} - Subject: {Subject}", issue.Id, issue.Subject);

        // Per-run cancellation: host shutdown (ct) or user abort (WorkflowRunRegistry.TryAbort).
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        runRegistry.Register(issue.Id, runCts);

        try
        {
            return await RunWorkflowAsync(issue, runCts.Token);
        }
        catch (OperationCanceledException) when (runCts.IsCancellationRequested)
        {
            return await CancelledAsync(issue.Id, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow failed for issue {IssueId}", issue.Id);
            return await FailAsync(issue.Id, ex.Message);
        }
        finally
        {
            runRegistry.Unregister(issue.Id);
        }

        async Task<WorkflowResult> RunWorkflowAsync(IssueRecord issueRecord, CancellationToken runToken)
        {
            // Build a fresh workflow instance per execution (framework requires exclusive ownership)
            var workflow = workflowFactory.Build();
            var sessionId = $"issue-{issueRecord.Id}";
            logger.LogInformation("Starting workflow for issue {IssueId}, session {SessionId}", issueRecord.Id, sessionId);
            await using var run = await InProcessExecution.RunStreamingAsync(workflow, issueRecord, sessionId, runToken);

            // Executors receive the framework's own token: propagate abort/shutdown to the run explicitly,
            // so running handlers (LLM calls, the approval gate) are cancelled and clean up.
            await using var cancelRegistration = runToken.Register(() => _ = run.CancelRunAsync());

            await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

            WorkflowResult? result = null;
            string? failureReason = null;

            await foreach (var evt in run.WatchStreamAsync(runToken))
            {
                logger.LogDebug("Workflow event: {EventType} for issue {IssueId}", evt.GetType().Name, issueRecord.Id);
                switch (evt)
                {
                    case WorkflowOutputEvent { Data: WorkflowResult workflowResult }:
                        result = workflowResult;
                        break;
                    case ExecutorFailedEvent failed:
                        failureReason ??= failed.Data?.Message ?? $"Executor {failed.ExecutorId} failed.";
                        logger.LogError(failed.Data, "Executor {ExecutorId} failed for issue {IssueId}", failed.ExecutorId, issueRecord.Id);
                        break;
                    case WorkflowErrorEvent error:
                        var exception = error.Data as Exception;
                        failureReason ??= exception?.Message ?? "Workflow error.";
                        logger.LogError(exception, "Workflow error for issue {IssueId}", issueRecord.Id);
                        break;
                }
            }

            logger.LogInformation("Workflow completed for issue {IssueId}, result: {Result}", issueRecord.Id, result?.GetType().Name ?? "null");

            // A cancelled executor surfaces as a failure event: report it as cancellation/abort instead.
            if (runToken.IsCancellationRequested)
            {
                return await CancelledAsync(issueRecord.Id, ct);
            }

            if (result is not null)
            {
                return result;
            }

            // No output was produced: surface the failure instead of leaving the issue stuck in a non-terminal stage.
            return await FailAsync(issueRecord.Id, failureReason ?? "Workflow completed without producing a result.");
        }
    }

    private Task<WorkflowResult> CancelledAsync(Guid issueId, CancellationToken hostToken)
    {
        var reason = hostToken.IsCancellationRequested ? "Workflow was cancelled." : WorkflowRunRegistry.AbortReason;
        logger.LogWarning("Workflow for issue {IssueId} stopped: {Reason}", issueId, reason);
        return FailAsync(issueId, reason);
    }

    private async Task<WorkflowResult> FailAsync(Guid issueId, string reason)
    {
        await stateTracker.TransitionAsync(issueId, WorkflowStage.Failed, reason);
        return WorkflowResult.Failed(issueId, reason);
    }
}
