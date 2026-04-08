namespace AiSupportWorkflow.UnitTests.Helpers;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

internal sealed class FakeChatCompletionService : IChatCompletionService
{
    private readonly Func<ChatHistory, Task<ChatMessageContent>> _handler;

    public FakeChatCompletionService(string responseContent)
    {
        _handler = _ => Task.FromResult(new ChatMessageContent(AuthorRole.Assistant, responseContent));
    }

    public FakeChatCompletionService(Exception exception)
    {
        _handler = _ => throw exception;
    }

    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _handler(chatHistory);
        return [result];
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var result = await _handler(chatHistory);
        yield return new StreamingChatMessageContent(result.Role, result.Content);
    }
}
