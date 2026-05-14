namespace AiSupportWorkflow.Infrastructure.Persistence.Entities;

public class LlmCallRecord
{
    public Guid Id { get; set; }
    public string AgentId { get; set; } = "";
    public string ModelName { get; set; } = "";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public long LatencyMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
