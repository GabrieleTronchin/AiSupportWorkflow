namespace AiSupportWorkflow.Infrastructure.AgentFramework;

using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

internal sealed class LlmTelemetryMiddleware(
    IChatClient innerClient,
    ILogger<LlmTelemetryMiddleware> logger,
    LlmTelemetryStore telemetryStore) : DelegatingChatClient(innerClient)
{
    /// <summary>ChatOptions.AdditionalProperties key carrying the agent the call is attributed to.</summary>
    internal const string AgentIdKey = "aisupport.telemetry.agentId";

    /// <summary>Returns a copy of <paramref name="options"/> that attributes the LLM call to <paramref name="agentId"/>.</summary>
    public static ChatOptions ForAgent(ChatOptions options, string agentId)
    {
        var clone = options.Clone();
        clone.AdditionalProperties ??= [];
        clone.AdditionalProperties[AgentIdKey] = agentId;
        return clone;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // The attribution key is internal: never forward it to the LLM provider.
        var (agentId, forwardedOptions) = ExtractAgentId(options);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await base.GetResponseAsync(messages, forwardedOptions, cancellationToken);
            stopwatch.Stop();

            var entry = new LlmCallEntry(
                ModelName: response.ModelId ?? forwardedOptions?.ModelId ?? "unknown",
                PromptTokens: (int)(response.Usage?.InputTokenCount ?? 0),
                CompletionTokens: (int)(response.Usage?.OutputTokenCount ?? 0),
                LatencyMs: stopwatch.ElapsedMilliseconds,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow,
                AgentId: agentId);

            telemetryStore.Record(entry);
            logger.LogInformation(
                "LLM call: Agent={Agent}, Model={Model}, PromptTokens={Prompt}, CompletionTokens={Completion}, Latency={Latency}ms",
                agentId ?? "-", entry.ModelName, entry.PromptTokens, entry.CompletionTokens, entry.LatencyMs);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var entry = new LlmCallEntry(
                ModelName: forwardedOptions?.ModelId ?? "unknown",
                PromptTokens: 0,
                CompletionTokens: 0,
                LatencyMs: stopwatch.ElapsedMilliseconds,
                Success: false,
                Timestamp: DateTimeOffset.UtcNow,
                ErrorMessage: ex.Message,
                AgentId: agentId);

            telemetryStore.Record(entry);
            logger.LogError(ex, "LLM call failed: Agent={Agent}, Latency={Latency}ms", agentId ?? "-", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private static (string? AgentId, ChatOptions? Options) ExtractAgentId(ChatOptions? options)
    {
        if (options?.AdditionalProperties is not { } props || !props.TryGetValue(AgentIdKey, out var value))
        {
            return (null, options);
        }

        var forwarded = options.Clone();
        forwarded.AdditionalProperties!.Remove(AgentIdKey);
        if (forwarded.AdditionalProperties.Count == 0)
        {
            forwarded.AdditionalProperties = null;
        }

        return (value?.ToString(), forwarded);
    }
}
