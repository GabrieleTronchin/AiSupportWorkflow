namespace AiSupportWorkflow.Application.Services;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class Orchestrator(
    IEmailProcessor emailProcessor,
    IIssueClassifier issueClassifier,
    ITeamRouter teamRouter,
    IAgentSelector agentSelector,
    ICodeChangeGenerator codeChangeGenerator,
    IWorkflowStateTracker stateTracker,
    ISupervisorActorBridge supervisorBridge,
    ILogger<Orchestrator> logger,
    IOptions<WorkflowConfiguration> workflowConfig) : IOrchestrator
{
    public async Task<WorkflowResult> ProcessIssueAsync(IncomingEmail email, CancellationToken ct = default)
    {
        var issueResult = emailProcessor.Process(email);
        if (!issueResult.IsSuccess)
            return WorkflowResult.Failed(Guid.Empty, issueResult.Error!);

        var issue = issueResult.Value!;
        await stateTracker.TransitionAsync(issue.Id, WorkflowStage.Received);

        try
        {
            var classification = await issueClassifier.ClassifyAsync(issue, ct);
            logger.LogInformation(
                "Classification for issue {IssueId}: Category={Category}, Confidence={Confidence:F2}, IsCodeRelated={IsCodeRelated}",
                issue.Id, classification.Category, classification.ConfidenceScore, classification.IsCodeRelated);

            if (!classification.IsCodeRelated)
            {
                await stateTracker.TransitionAsync(issue.Id, WorkflowStage.ClassifiedOutOfScope, classification.Reasoning);
                return WorkflowResult.OutOfScope(issue.Id);
            }

            var classifiedDetail = $"{classification.Category} ({classification.ConfidenceScore:P0})";
            await stateTracker.TransitionAsync(issue.Id, WorkflowStage.Classified, classifiedDetail);

            var teamResult = teamRouter.Route(issue, classification);
            if (!teamResult.IsSuccess)
            {
                await stateTracker.TransitionAsync(issue.Id, WorkflowStage.Failed, teamResult.Error);
                return WorkflowResult.Failed(issue.Id, teamResult.Error!);
            }

            var team = teamResult.Value!;
            await stateTracker.TransitionAsync(issue.Id, WorkflowStage.TeamAssigned, team.TeamName);
            logger.LogInformation("Team assigned for issue {IssueId}: {TeamName}", issue.Id, team.TeamName);

            var agent = agentSelector.Select(team, classification.Category);
            await stateTracker.TransitionAsync(issue.Id, WorkflowStage.AgentAssigned, agent.AgentId);
            logger.LogInformation("Agent assigned for issue {IssueId}: {AgentId}", issue.Id, agent.AgentId);

            await stateTracker.TransitionAsync(issue.Id, WorkflowStage.Resolving);
            var resolution = await supervisorBridge.AssignIssueAsync(
                agent.AgentId, issue, classification.Category, GetActorAskTimeout(), ct);
            await stateTracker.TransitionAsync(issue.Id, WorkflowStage.Resolved, resolution.ProposedFixSummary);

            var pullRequest = await codeChangeGenerator.GenerateAsync(resolution, ct);
            await stateTracker.TransitionAsync(issue.Id, WorkflowStage.CodeChangeGenerated, pullRequest.Title);

            return WorkflowResult.Completed(issue.Id, pullRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow failed for issue {IssueId}", issue.Id);
            await stateTracker.TransitionAsync(issue.Id, WorkflowStage.Failed, ex.Message);
            return WorkflowResult.Failed(issue.Id, ex.Message);
        }
    }

    private TimeSpan GetActorAskTimeout()
    {
        var seconds = workflowConfig.Value.ActorAskTimeoutSeconds;
        return TimeSpan.FromSeconds(seconds > 0 ? seconds : 120);
    }
}
