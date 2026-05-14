namespace AiSupportWorkflow.Infrastructure.Persistence.Entities;

public class WorkflowCheckpoint
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public string ExecutorId { get; set; } = "";
    public string SerializedState { get; set; } = "";
    public DateTimeOffset PausedAt { get; set; }
    public DateTimeOffset? ResumedAt { get; set; }
    public bool IsActive { get; set; }
}
