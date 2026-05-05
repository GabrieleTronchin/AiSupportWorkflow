namespace AiSupportWorkflow.Domain.Messages;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.ValueObjects;

public record AssignIssueMessage(string TargetAgentId, IssueRecord Issue, IssueCategory Category);

public record ResolutionCompleteMessage(Guid IssueId, ResolutionReport Report);

public record AgentStatusQuery(string? TargetAgentId);

public record AgentStatusResponse(string AgentId, string Status, string? LastAction);

public record AggregatedAgentStatusResponse(List<AgentStatusResponse> Statuses);

public record AgentNotFoundMessage(string AgentId);
