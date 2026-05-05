namespace AiSupportWorkflow.Infrastructure.Agents;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;

public class AiAgent(
    string agentId,
    string teamName,
    AgentRole role,
    IBugResolver bugResolver) : IAIAgent
{
    public string AgentId => agentId;
    public string TeamName => teamName;
    public AgentRole Role => role;

    public Task<ResolutionReport> AnalyzeAndResolveAsync(IssueRecord issue, CancellationToken ct = default)
    {
        var assignment = new AgentAssignment(agentId, teamName, role);
        return bugResolver.ResolveAsync(issue, assignment, ct);
    }
}
