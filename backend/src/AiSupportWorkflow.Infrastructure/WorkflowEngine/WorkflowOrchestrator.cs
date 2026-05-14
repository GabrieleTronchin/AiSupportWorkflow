namespace AiSupportWorkflow.Infrastructure.WorkflowEngine;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

internal sealed class WorkflowOrchestrator(
    Workflow workflow,
    IEmailProcessor emailProcessor,
    IWorkflowStateTracker stateTracker,
    ILogger<WorkflowOrchestrator> logger) : IOrchestrator
{
    public async Task<WorkflowResult> ProcessIssueAsync(IncomingEmail email, CancellationToken ct = default)
    {
        var issueResult = emailProcessor.Process(email);
        if (!issueResult.IsSuccess)
            return WorkflowResult.Failed(Guid.Empty, issueResult.Error!);

        var issue = issueResult.Value!;

        try
        {
            var sessionId = $"issue-{issue.Id}";
            var run = await InProcessExecution.Default.RunStreamingAsync(workflow, issue, sessionId, ct);

            WorkflowResult? result = null;

            await foreach (var evt in run.WatchStreamAsync(ct))
            {
                if (evt is WorkflowOutputEvent outputEvent && outputEvent.Data is WorkflowResult workflowResult)
                {
                    result = workflowResult;
                }
            }

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
