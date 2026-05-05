namespace AiSupportWorkflow.Infrastructure.Persistence.Entities;

using AiSupportWorkflow.Domain.Enums;

public class StateTransitionEvent
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public WorkflowStage? PreviousStage { get; set; }
    public WorkflowStage NewStage { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Detail { get; set; }
}
