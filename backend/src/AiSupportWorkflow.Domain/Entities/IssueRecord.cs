namespace AiSupportWorkflow.Domain.Entities;

public record IssueRecord(Guid Id, string Sender, string Subject, string Body, DateTimeOffset CreatedAt);
