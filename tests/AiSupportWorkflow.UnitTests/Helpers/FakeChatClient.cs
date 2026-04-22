namespace AiSupportWorkflow.UnitTests.Helpers;

using Microsoft.Extensions.AI;

internal sealed class FakeChatClient : IChatClient
{
    private readonly Func<IEnumerable<ChatMessage>, Task<ChatResponse>> _handler;

    public FakeChatClient(string responseContent)
    {
        _handler = _ => Task.FromResult(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, responseContent)));
    }

    public FakeChatClient(Exception exception)
    {
        _handler = _ => throw exception;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return await _handler(messages);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await _handler(messages);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
        // No resources to dispose
    }
}
