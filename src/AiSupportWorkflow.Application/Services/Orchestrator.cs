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
        logger.LogDebug(
            "Workflow stage transition for issue {IssueId}: {SourceStage} → {TargetStage}, Detail={Detail}, Sender={Sender}, Subject={Subject}",
            issue.Id, "None", WorkflowStage.Received, (string?)null, email.Sender, email.Subject);
        stateTracker.Transition(issue.Id, WorkflowStage.Received);

        try
        {
            var classification = await ClassifyIssueAsync(issue, ct);
            LogClassificationDecision(issue.Id, classification);

            if (!classification.IsCodeRelated)
            {
                logger.LogDebug(
                    "Workflow stage transition for issue {IssueId}: {SourceStage} → {TargetStage}, Detail={Detail}",
                    issue.Id, WorkflowStage.Received, WorkflowStage.ClassifiedOutOfScope, classification.Reasoning);
                stateTracker.Transition(issue.Id, WorkflowStage.ClassifiedOutOfScope, classification.Reasoning);
                return WorkflowResult.OutOfScope(issue.Id);
            }

            var classifiedDetail = $"{classification.Category} ({classification.ConfidenceScore:P0})";
            logger.LogDebug(
                "Workflow stage transition for issue {IssueId}: {SourceStage} → {TargetStage}, Detail={Detail}",
                issue.Id, WorkflowStage.Received, WorkflowStage.Classified, classifiedDetail);
            stateTracker.Transition(issue.Id, WorkflowStage.Classified, classifiedDetail);

            var team = AssignTeam(issue, classification);
            logger.LogDebug(
                "Workflow stage transition for issue {IssueId}: {SourceStage} → {TargetStage}, Detail={Detail}",
                issue.Id, WorkflowStage.Classified, WorkflowStage.TeamAssigned, team.TeamName);
            stateTracker.Transition(issue.Id, WorkflowStage.TeamAssigned, team.TeamName);
            LogTeamAssignmentDecision(issue.Id, team);

            var agent = SelectAgent(team, classification.Category);
            logger.LogDebug(
                "Workflow stage transition for issue {IssueId}: {SourceStage} → {TargetStage}, Detail={Detail}",
                issue.Id, WorkflowStage.TeamAssigned, WorkflowStage.AgentAssigned, agent.AgentId);
            stateTracker.Transition(issue.Id, WorkflowStage.AgentAssigned, agent.AgentId);
            LogAgentSelectionDecision(issue.Id, agent);

            logger.LogDebug(
                "Workflow stage transition for issue {IssueId}: {SourceStage} → {TargetStage}, Detail={Detail}",
                issue.Id, WorkflowStage.AgentAssigned, WorkflowStage.Resolving, (string?)null);
            stateTracker.Transition(issue.Id, WorkflowStage.Resolving);
            var resolution = await ResolveWithActorAsync(issue, classification.Category, agent, ct);
            logger.LogDebug(
                "Workflow stage transition for issue {IssueId}: {SourceStage} → {TargetStage}, Detail={Detail}",
                issue.Id, WorkflowStage.Resolving, WorkflowStage.Resolved, resolution.ProposedFixSummary);
            stateTracker.Transition(issue.Id, WorkflowStage.Resolved, resolution.ProposedFixSummary);

            var pullRequest = await codeChangeGenerator.GenerateAsync(resolution, ct);
            logger.LogDebug(
                "Workflow stage transition for issue {IssueId}: {SourceStage} → {TargetStage}, Detail={Detail}",
                issue.Id, WorkflowStage.Resolved, WorkflowStage.CodeChangeGenerated, pullRequest.Title);
            stateTracker.Transition(issue.Id, WorkflowStage.CodeChangeGenerated, pullRequest.Title);

            return WorkflowResult.Completed(issue.Id, pullRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow failed for issue {IssueId}", issue.Id);
            logger.LogWarning(
                "Workflow stage transition FAILED for issue {IssueId}: {SourceStage} → {TargetStage}, FailureReason={FailureReason}",
                issue.Id, WorkflowStage.Received, WorkflowStage.Failed, ex.Message);
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

    private Task<ResolutionReport> ResolveWithActorAsync(
        IssueRecord issue,
        IssueCategory category,
        AgentAssignment agent,
        CancellationToken ct)
    {
        return supervisorBridge.AssignIssueAsync(
            agent.AgentId, issue, category,
            GetActorAskTimeout(), ct);
    }

    private TimeSpan GetActorAskTimeout()
    {
        var seconds = workflowConfig.Value.ActorAskTimeoutSeconds;
        return TimeSpan.FromSeconds(seconds > 0 ? seconds : 120);
    }

    private void LogClassificationDecision(Guid issueId, ClassificationResult classification)
    {
        logger.LogInformation(
            "[Visualization] Classification decision for issue {IssueId}: Category={Category}, ConfidenceScore={ConfidenceScore:F2}, IsCodeRelated={IsCodeRelated}, Reasoning={Reasoning}",
            issueId, classification.Category, classification.ConfidenceScore, classification.IsCodeRelated, classification.Reasoning);
    }

    private void LogTeamAssignmentDecision(Guid issueId, TeamAssignment team)
    {
        logger.LogInformation(
            "[Visualization] Team assignment decision for issue {IssueId}: TeamName={TeamName}, ApplicationName={ApplicationName}",
            issueId, team.TeamName, team.ApplicationName);
    }

    private void LogAgentSelectionDecision(Guid issueId, AgentAssignment agent)
    {
        logger.LogInformation(
            "[Visualization] Agent selection decision for issue {IssueId}: AgentId={AgentId}, Role={Role}",
            issueId, agent.AgentId, agent.Role);
    }
}
