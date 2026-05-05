namespace AiSupportWorkflow.Application.Services;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Interfaces;
using Microsoft.Extensions.Options;

public sealed class AgentStatusService(
    IAgentStatusProvider agentStatusProvider,
    IWorkflowEventRepository eventRepository,
    IOptions<WorkflowConfiguration> config)
{
    public async Task<IReadOnlyList<AgentStatusDto>> GetAllAgentStatusesAsync(CancellationToken ct = default)
    {
        var activeResponse = await agentStatusProvider.GetAgentStatusesAsync(ct);
        var activeAgents = activeResponse.Statuses.ToDictionary(s => s.AgentId, s => s);

        var agentAssignments = await eventRepository.GetAgentAssignmentsForNonTerminalIssuesAsync(ct);

        var agentToIssue = agentAssignments
            .GroupBy(a => a.AgentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(a => a.Timestamp).First());

        var allAgents = config.Value.Teams
            .SelectMany(team => team.Agents.Select(agent =>
            {
                var agentId = $"{team.TeamName}_{agent.Role}";
                var isActive = activeAgents.TryGetValue(agentId, out var status);
                var hasAssignedIssue = agentToIssue.TryGetValue(agentId, out var assignment);

                var agentStatus = hasAssignedIssue ? "Working"
                    : isActive ? status!.Status
                    : "Idle";

                return new AgentStatusDto(
                    AgentId: agentId,
                    Team: team.TeamName,
                    Role: agent.Role.ToString(),
                    Status: agentStatus,
                    LastAction: isActive ? status!.LastAction : null,
                    CurrentIssueId: hasAssignedIssue ? assignment!.IssueId.ToString() : null,
                    CurrentSubject: hasAssignedIssue ? assignment!.Detail : null,
                    CurrentStage: hasAssignedIssue ? assignment!.CurrentStage.ToString() : null);
            }))
            .ToList();

        return allAgents;
    }
}

public record AgentStatusDto(
    string AgentId,
    string Team,
    string Role,
    string Status,
    string? LastAction,
    string? CurrentIssueId,
    string? CurrentSubject,
    string? CurrentStage);
