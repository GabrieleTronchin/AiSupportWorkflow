namespace AiSupportWorkflow.UnitTests;

using AiSupportWorkflow.Infrastructure.AgentFramework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

public class LlmTelemetryMiddlewareTests
{
    /// <summary>Records the options it receives, to verify what reaches the provider.</summary>
    private sealed class CapturingChatClient : IChatClient
    {
        public ChatOptions? LastOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
            });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
            // Nothing to dispose
        }
    }

    private readonly CapturingChatClient _inner = new();
    private readonly LlmTelemetryStore _store = new();

    private LlmTelemetryMiddleware CreateSut() => new(_inner, NullLogger<LlmTelemetryMiddleware>.Instance, _store);

    [Fact]
    public async Task CallForAgent_IsAttributedToAgent_AndKeyIsNotForwarded()
    {
        var options = LlmTelemetryMiddleware.ForAgent(new ChatOptions { Temperature = 0.2f }, "TeamA_BackendDeveloper");

        await CreateSut().GetResponseAsync([new(ChatRole.User, "hi")], options, TestContext.Current.CancellationToken);

        var telemetry = _store.GetAgentTelemetry("TeamA_BackendDeveloper");
        Assert.Equal(1, telemetry.TotalCalls);
        Assert.Equal(10, telemetry.TotalPromptTokens);
        Assert.Equal(5, telemetry.TotalCompletionTokens);

        Assert.NotNull(_inner.LastOptions);
        Assert.Equal(0.2f, _inner.LastOptions!.Temperature);
        Assert.False(_inner.LastOptions.AdditionalProperties?.ContainsKey(LlmTelemetryMiddleware.AgentIdKey) ?? false);
    }

    [Fact]
    public void ForAgent_DoesNotMutateSharedOptions()
    {
        var shared = new ChatOptions { Temperature = 0.1f };

        _ = LlmTelemetryMiddleware.ForAgent(shared, "TeamB_QAEngineer");

        Assert.Null(shared.AdditionalProperties);
    }

    [Fact]
    public async Task CallWithoutAgent_IsCountedGloballyOnly()
    {
        await CreateSut().GetResponseAsync([new(ChatRole.User, "hi")], new ChatOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(1, _store.GetGlobalSummary().TotalCalls);
        Assert.Equal(0, _store.GetAgentTelemetry("TeamA_BackendDeveloper").TotalCalls);
    }
}
