namespace AiSupportWorkflow.Application.Services;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Extensions.Options;

public class AgentSelector(IOptions<WorkflowConfiguration> config) : IAgentSelector
{
    public AgentAssignment Select(TeamAssignment team, IssueCategory category)
    {
        var role = MapCategoryToRole(category);
        ValidateAgentExists(team.TeamName, role);

        return new AgentAssignment($"{team.TeamName}_{role}", team.TeamName, role);
    }

    private void ValidateAgentExists(string teamName, AgentRole role)
    {
        var teamConfig = config.Value.Teams
            .FirstOrDefault(t => t.TeamName.Equals(teamName, StringComparison.OrdinalIgnoreCase));

        if (teamConfig is null)
            throw new InvalidOperationException($"Team '{teamName}' is not configured.");

        if (!teamConfig.Agents.Exists(a => a.Role == role))
            throw new InvalidOperationException($"No agent with role '{role}' configured for team '{teamName}'.");
    }

    private static AgentRole MapCategoryToRole(IssueCategory category) => category switch
    {
        IssueCategory.BackendBug => AgentRole.BackendDeveloper,
        IssueCategory.FrontendBug => AgentRole.FrontendDeveloper,
        IssueCategory.QualityTestIssue => AgentRole.QAEngineer,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category,
            $"No agent role mapping for category '{category}'.")
    };
}
