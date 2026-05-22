namespace AiSupportWorkflow.Infrastructure.AgentFramework;

using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

internal sealed class LlmTelemetryMiddleware(
    IChatClient innerClient,
    ILogger<LlmTelemetryMiddleware> logger,
    LlmTelemetryStore telemetryStore) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken);
            stopwatch.Stop();

            var entry = new LlmCallEntry(
                ModelName: response.ModelId ?? options?.ModelId ?? "unknown",
                PromptTokens: (int)(response.Usage?.InputTokenCount ?? 0),
                CompletionTokens: (int)(response.Usage?.OutputTokenCount ?? 0),
                LatencyMs: stopwatch.ElapsedMilliseconds,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow);

            telemetryStore.Record(entry);
            logger.LogInformation(
                "LLM call: Model={Model}, PromptTokens={Prompt}, CompletionTokens={Completion}, Latency={Latency}ms",
                entry.ModelName, entry.PromptTokens, entry.CompletionTokens, entry.LatencyMs);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var entry = new LlmCallEntry(
                ModelName: options?.ModelId ?? "unknown",
                PromptTokens: 0,
                CompletionTokens: 0,
                LatencyMs: stopwatch.ElapsedMilliseconds,
                Success: false,
                Timestamp: DateTimeOffset.UtcNow,
                ErrorMessage: ex.Message);

            telemetryStore.Record(entry);
            logger.LogError(ex, "LLM call failed: Latency={Latency}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
