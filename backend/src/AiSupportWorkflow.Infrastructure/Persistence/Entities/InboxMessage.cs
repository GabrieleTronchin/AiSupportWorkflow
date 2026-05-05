namespace AiSupportWorkflow.Infrastructure.Persistence.Entities;

public class InboxMessage
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = "SupportEmail";
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? Error { get; set; }
}
