namespace AiSupportWorkflow.Domain.Messages;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.ValueObjects;

public record AssignIssueMessage(IssueRecord Issue, IssueCategory Category);

public record ResolutionCompleteMessage(Guid IssueId, ResolutionReport Report);

public record AgentStatusQuery();

public record AgentStatusResponse(string AgentId, string Status, string? LastAction);
