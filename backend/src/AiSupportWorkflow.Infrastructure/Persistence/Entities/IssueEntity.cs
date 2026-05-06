namespace AiSupportWorkflow.Infrastructure.Persistence.Entities;

using AiSupportWorkflow.Domain.Enums;

public class IssueEntity
{
    public Guid Id { get; set; }
    public WorkflowStage CurrentStage { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
    public string? Detail { get; set; }
    public string? Subject { get; set; }
}
