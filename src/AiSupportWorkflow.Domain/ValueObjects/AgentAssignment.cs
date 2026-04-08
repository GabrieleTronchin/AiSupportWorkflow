namespace AiSupportWorkflow.Domain.ValueObjects;

using AiSupportWorkflow.Domain.Enums;

public record AgentAssignment(string AgentId, string TeamName, AgentRole Role);
