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

        try
        {
            // Build a fresh workflow instance per execution (framework requires exclusive ownership)
            var workflow = workflowFactory.Build();
            var sessionId = $"issue-{issue.Id}";
            logger.LogInformation("Starting workflow for issue {IssueId}, session {SessionId}", issue.Id, sessionId);
            var run = await InProcessExecution.RunStreamingAsync(workflow, issue, sessionId, ct);
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

            WorkflowResult? result = null;

            await foreach (var evt in run.WatchStreamAsync(ct))
            {
                logger.LogDebug("Workflow event: {EventType} for issue {IssueId}", evt.GetType().Name, issue.Id);
                if (evt is WorkflowOutputEvent outputEvent && outputEvent.Data is WorkflowResult workflowResult)
                {
                    result = workflowResult;
                }
            }

            logger.LogInformation("Workflow completed for issue {IssueId}, result: {Result}", issue.Id, result?.GetType().Name ?? "null");
            return result ?? WorkflowResult.Failed(issue.Id, "Workflow completed without producing a result.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogWarning("Workflow cancelled for issue {IssueId}", issue.Id);
            await stateTracker.TransitionAsync(issue.Id, WorkflowStage.Failed, "Workflow was cancelled.");
            return WorkflowResult.Failed(issue.Id, "Workflow was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow failed for issue {IssueId}", issue.Id);
            await stateTracker.TransitionAsync(issue.Id, WorkflowStage.Failed, ex.Message);
            return WorkflowResult.Failed(issue.Id, ex.Message);
        }
    }
}
