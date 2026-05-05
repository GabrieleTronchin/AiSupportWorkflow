namespace AiSupportWorkflow.Domain.Interfaces;

using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.ValueObjects;

public interface IAgentSelector
{
    AgentAssignment Select(TeamAssignment team, IssueCategory category);
}
