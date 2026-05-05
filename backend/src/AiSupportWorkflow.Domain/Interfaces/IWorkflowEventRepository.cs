namespace AiSupportWorkflow.Domain.Interfaces;

using AiSupportWorkflow.Domain.Enums;

public interface IWorkflowEventRepository
{
    Task<IReadOnlyList<WorkflowEventDto>> GetEventsAsync(int limit, CancellationToken ct = default);

    Task<IReadOnlyList<AgentAssignmentInfo>> GetAgentAssignmentsForNonTerminalIssuesAsync(CancellationToken ct = default);
}

public record WorkflowEventDto(
    Guid Id,
    Guid IssueId,
    string? PreviousStage,
    string NewStage,
    DateTimeOffset Timestamp,
    string? Detail);

public record AgentAssignmentInfo(
    string AgentId,
    Guid IssueId,
    WorkflowStage CurrentStage,
    string? Detail,
    DateTimeOffset Timestamp);
