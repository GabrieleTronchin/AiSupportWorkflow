namespace AiSupportWorkflow.Application.Services;

using Akka.Actor;
using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using AiSupportWorkflow.Domain.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class Orchestrator(
    IEmailProcessor emailProcessor,
    IIssueClassifier issueClassifier,
    ITeamRouter teamRouter,
    IAgentSelector agentSelector,
    ICodeChangeGenerator codeChangeGenerator,
    IWorkflowStateTracker stateTracker,
    ActorSystem actorSystem,
    ILogger<Orchestrator> logger,
    IOptions<WorkflowConfiguration> workflowConfig) : IOrchestrator
{
    private bool IsVisualizationEnabled => workflowConfig.Value.EnableVisualization;

    public async Task<WorkflowResult> ProcessIssueAsync(IncomingEmail email, CancellationToken ct = default)
    {
        var issueResult = emailProcessor.Process(email);
        if (!issueResult.IsSuccess)
            return WorkflowResult.Failed(Guid.Empty, issueResult.Error!);

        var issue = issueResult.Value!;
        stateTracker.Transition(issue.Id, WorkflowStage.Received);

        try
        {
            var classification = await ClassifyIssueAsync(issue, ct);
            LogClassificationDecision(issue.Id, classification);

            if (!classification.IsCodeRelated)
            {
                stateTracker.Transition(issue.Id, WorkflowStage.ClassifiedOutOfScope, classification.Reasoning);
                return WorkflowResult.OutOfScope(issue.Id);
            }

            stateTracker.Transition(issue.Id, WorkflowStage.Classified, $"{classification.Category} ({classification.ConfidenceScore:P0})");

            var team = AssignTeam(issue, classification);
            stateTracker.Transition(issue.Id, WorkflowStage.TeamAssigned, team.TeamName);
            LogTeamAssignmentDecision(issue.Id, team);

            var agent = SelectAgent(team, classification.Category);
            stateTracker.Transition(issue.Id, WorkflowStage.AgentAssigned, agent.AgentId);
            LogAgentSelectionDecision(issue.Id, agent);

            stateTracker.Transition(issue.Id, WorkflowStage.Resolving);
            var resolution = await ResolveWithActorAsync(issue, classification.Category, agent, ct);
            stateTracker.Transition(issue.Id, WorkflowStage.Resolved, resolution.ProposedFixSummary);

            var pullRequest = await codeChangeGenerator.GenerateAsync(resolution, ct);
            stateTracker.Transition(issue.Id, WorkflowStage.CodeChangeGenerated, pullRequest.Title);

            return WorkflowResult.Completed(issue.Id, pullRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow failed for issue {IssueId}", issue.Id);
            stateTracker.Transition(issue.Id, WorkflowStage.Failed, ex.Message);
            return WorkflowResult.Failed(issue.Id, ex.Message);
        }
    }

    private Task<ClassificationResult> ClassifyIssueAsync(IssueRecord issue, CancellationToken ct) =>
        issueClassifier.ClassifyAsync(issue, ct);

    private TeamAssignment AssignTeam(IssueRecord issue, ClassificationResult classification)
    {
        var result = teamRouter.Route(issue, classification);
        return result.IsSuccess
            ? result.Value!
            : throw new InvalidOperationException($"Team routing failed: {result.Error}");
    }

    private AgentAssignment SelectAgent(TeamAssignment team, IssueCategory category) =>
        agentSelector.Select(team, category);

    private async Task<ResolutionReport> ResolveWithActorAsync(
        IssueRecord issue,
        IssueCategory category,
        AgentAssignment agent,
        CancellationToken ct)
    {
        var agentPath = $"/user/supervisor/{agent.AgentId}";
        var agentActor = await actorSystem.ActorSelection(agentPath)
            .ResolveOne(TimeSpan.FromSeconds(5), ct);

        var response = await agentActor.Ask<ResolutionCompleteMessage>(
            new AssignIssueMessage(issue, category),
            TimeSpan.FromMinutes(2),
            ct);

        return response.Report;
    }

    private void LogClassificationDecision(Guid issueId, ClassificationResult classification)
    {
        if (!IsVisualizationEnabled) return;

        logger.LogInformation(
            "[Visualization] Classification decision for issue {IssueId}: Category={Category}, Confidence={Confidence:F2}, IsCodeRelated={IsCodeRelated}, Reasoning={Reasoning}",
            issueId, classification.Category, classification.ConfidenceScore, classification.IsCodeRelated, classification.Reasoning);
    }

    private void LogTeamAssignmentDecision(Guid issueId, TeamAssignment team)
    {
        if (!IsVisualizationEnabled) return;

        logger.LogInformation(
            "[Visualization] Team assignment decision for issue {IssueId}: TeamName={TeamName}, ApplicationName={ApplicationName}",
            issueId, team.TeamName, team.ApplicationName);
    }

    private void LogAgentSelectionDecision(Guid issueId, AgentAssignment agent)
    {
        if (!IsVisualizationEnabled) return;

        logger.LogInformation(
            "[Visualization] Agent selection decision for issue {IssueId}: AgentId={AgentId}, Role={Role}",
            issueId, agent.AgentId, agent.Role);
    }
}
