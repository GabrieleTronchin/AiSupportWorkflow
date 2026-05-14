namespace AiSupportWorkflow.Infrastructure.AgentFramework;

using System.Collections.Concurrent;

public record LlmCallEntry(
    string ModelName,
    int PromptTokens,
    int CompletionTokens,
    long LatencyMs,
    bool Success,
    DateTimeOffset Timestamp,
    string? ErrorMessage = null,
    string? AgentId = null);

public record AgentTelemetry(
    string AgentId,
    int TotalPromptTokens,
    int TotalCompletionTokens,
    int TotalCalls,
    double AverageLatencyMs,
    LlmCallEntry? LastCall);

public record TelemetrySummary(
    int TotalTokens,
    int TotalCalls,
    double AverageLatencyMs,
    double ErrorRate);

internal sealed class LlmTelemetryStore
{
    private readonly ConcurrentBag<LlmCallEntry> _entries = [];

    public void Record(LlmCallEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
    }

    public AgentTelemetry GetAgentTelemetry(string agentId)
    {
        var agentEntries = _entries.Where(e => e.AgentId == agentId).ToList();

        if (agentEntries.Count == 0)
        {
            return new AgentTelemetry(agentId, 0, 0, 0, 0, null);
        }

        var totalPromptTokens = agentEntries.Sum(e => e.PromptTokens);
        var totalCompletionTokens = agentEntries.Sum(e => e.CompletionTokens);
        var averageLatency = agentEntries.Average(e => e.LatencyMs);
        var lastCall = agentEntries.OrderByDescending(e => e.Timestamp).First();

        return new AgentTelemetry(
            agentId,
            totalPromptTokens,
            totalCompletionTokens,
            agentEntries.Count,
            averageLatency,
            lastCall);
    }

    public TelemetrySummary GetGlobalSummary()
    {
        var allEntries = _entries.ToList();

        if (allEntries.Count == 0)
        {
            return new TelemetrySummary(0, 0, 0, 0);
        }

        var totalTokens = allEntries.Sum(e => e.PromptTokens + e.CompletionTokens);
        var averageLatency = allEntries.Average(e => e.LatencyMs);
        var errorRate = (double)allEntries.Count(e => !e.Success) / allEntries.Count;

        return new TelemetrySummary(
            totalTokens,
            allEntries.Count,
            averageLatency,
            errorRate);
    }
}
