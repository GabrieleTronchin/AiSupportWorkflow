namespace AiSupportWorkflow.Presentation.Endpoints;

using Akka.Actor;
using Akka.Hosting;
using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Messages;
using AiSupportWorkflow.Infrastructure.Actors;
using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.Presentation.Endpoints.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public class AgentsEndpoints : IEndpoint
{
    private static readonly WorkflowStage[] TerminalStages =
    [
        WorkflowStage.Failed,
        WorkflowStage.CodeChangeGenerated,
        WorkflowStage.ClassifiedOutOfScope,
    ];

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support").WithTags("Agents");

        group.MapGet("/agents", async (
            IRequiredActor<SupervisorActor> supervisorActor,
            IOptions<WorkflowConfiguration> config,
            WorkflowDbContext dbContext,
            CancellationToken ct) =>
        {
            if (!config.Value.EnableVisualization)
                return Results.NotFound(new { Error = "Visualization is disabled." });

            // Get active agent statuses from Akka
            var supervisor = supervisorActor.ActorRef;
            var response = await supervisor.Ask<AggregatedAgentStatusResponse>(
                new AgentStatusQuery(null),
                TimeSpan.FromSeconds(10),
                ct);

            var activeAgents = response.Statuses.ToDictionary(s => s.AgentId, s => s);

            // Find non-terminal issues and their agent assignments via StateTransitionEvent
            var nonTerminalIssues = await dbContext.Issues
                .AsNoTracking()
                .Where(i => !TerminalStages.Contains(i.CurrentStage))
                .ToListAsync(ct);

            // Get agent assignment events for non-terminal issues
            var nonTerminalIssueIds = nonTerminalIssues.Select(i => i.Id).ToList();
            var agentAssignments = await dbContext.Events
                .AsNoTracking()
                .Where(e => nonTerminalIssueIds.Contains(e.IssueId)
                    && e.NewStage == WorkflowStage.AgentAssigned
                    && e.Detail != null)
                .ToListAsync(ct);

            // Build a lookup: agentId -> (issueId, currentStage)
            var agentToIssue = agentAssignments
                .GroupBy(e => e.Detail!)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var latestAssignment = g.OrderByDescending(e => e.Timestamp).First();
                        var issue = nonTerminalIssues.FirstOrDefault(i => i.Id == latestAssignment.IssueId);
                        return issue;
                    });

            // Build full list from configuration, merging with active status and current issue info
            var allAgents = config.Value.Teams
                .SelectMany(team => team.Agents.Select(agent =>
                {
                    var agentId = $"{team.TeamName}_{agent.Role}";
                    var isActive = activeAgents.TryGetValue(agentId, out var status);
                    var hasAssignedIssue = agentToIssue.TryGetValue(agentId, out var assignedIssue)
                        && assignedIssue is not null;

                    var agentStatus = hasAssignedIssue ? "Working"
                        : isActive ? status!.Status
                        : "Idle";

                    return new AgentStatusResponse(
                        AgentId: agentId,
                        Team: team.TeamName,
                        Role: agent.Role.ToString(),
                        Status: agentStatus,
                        LastAction: isActive ? status!.LastAction : null,
                        CurrentIssueId: hasAssignedIssue ? assignedIssue!.Id.ToString() : null,
                        CurrentSubject: hasAssignedIssue ? assignedIssue!.Detail : null,
                        CurrentStage: hasAssignedIssue ? assignedIssue!.CurrentStage.ToString() : null
                    );
                }))
                .ToList();

            return Results.Ok(allAgents);
        }).WithSummary("Get all configured agents with current status (Idle/Working)");
    }
}

public record AgentStatusResponse(
    string AgentId,
    string Team,
    string Role,
    string Status,
    string? LastAction,
    string? CurrentIssueId,
    string? CurrentSubject,
    string? CurrentStage
);
