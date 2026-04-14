namespace AiSupportWorkflow.Application.Configuration;

using AiSupportWorkflow.Domain.Enums;

public class WorkflowConfiguration
{
    public bool EnableVisualization { get; set; }
    public int ActorAskTimeoutSeconds { get; set; } = 120;
    public List<TeamConfiguration> Teams { get; set; } = [];
}

public class TeamConfiguration
{
    public string TeamName { get; set; } = "";
    public string ApplicationName { get; set; } = "";
    public List<AgentRoleConfiguration> Agents { get; set; } = [];
}

public class AgentRoleConfiguration
{
    public AgentRole Role { get; set; }
    public string Persona { get; set; } = "";
}
