namespace AiSupportWorkflow.UnitTests;

using AiSupportWorkflow.Infrastructure.AgentFramework;
using AiSupportWorkflow.Infrastructure.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class ChatClientSetupTests
{
    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddLogging().AddChatClient(configuration).BuildServiceProvider();
    }

    [Fact]
    public void ResolveSettings_OpenAiWithoutEndpoint_UsesSdkDefaultEndpoint()
    {
        var settings = ChatClientSetup.ResolveSettings(new LlmProviderConfiguration
        {
            Provider = "OpenAI",
            ModelName = "gpt-4o-mini",
        });

        Assert.Null(settings.Endpoint);
        Assert.Equal("gpt-4o-mini", settings.ModelName);
    }

    [Fact]
    public void ResolveSettings_OpenAiWithEndpoint_UsesCustomEndpoint()
    {
        var settings = ChatClientSetup.ResolveSettings(new LlmProviderConfiguration
        {
            Provider = "openai",
            ModelName = "openai/gpt-oss-20b",
            Endpoint = "https://api.groq.com/openai/v1",
        });

        Assert.Equal(new Uri("https://api.groq.com/openai/v1"), settings.Endpoint);
        Assert.Equal("openai/gpt-oss-20b", settings.ModelName);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://example.com")]
    public void ResolveSettings_InvalidEndpoint_Throws(string endpoint)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ChatClientSetup.ResolveSettings(new LlmProviderConfiguration
        {
            Provider = "OpenAI",
            Endpoint = endpoint,
        }));

        Assert.Contains("Invalid LLM endpoint", ex.Message);
    }

    [Fact]
    public void AddChatClient_DemoProvider_DoesNotRequireApiKeyAndUsesDemoClient()
    {
        using var provider = BuildProvider(new() { ["LlmProvider:Provider"] = "Demo" });

        var chatClient = provider.GetRequiredService<IChatClient>();

        Assert.NotNull(chatClient.GetService<DemoChatClient>());
    }

    [Fact]
    public void AddChatClient_OpenAiWithoutApiKey_ThrowsWithDemoHint()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => BuildProvider(new() { ["LlmProvider:Provider"] = "OpenAI" }));

        Assert.Contains("Demo", ex.Message);
    }

    [Fact]
    public async Task AddChatClient_RecordsTelemetryForEveryCall()
    {
        using var provider = BuildProvider(new()
        {
            ["LlmProvider:Provider"] = "Demo",
            ["LlmProvider:DemoLatencyMs"] = "0",
        });
        var chatClient = provider.GetRequiredService<IChatClient>();

        await chatClient.GetResponseAsync("Subject: hello\n\nBody: world", cancellationToken: TestContext.Current.CancellationToken);

        var summary = provider.GetRequiredService<LlmTelemetryStore>().GetGlobalSummary();
        Assert.Equal(1, summary.TotalCalls);
        Assert.True(summary.TotalTokens > 0);
    }
}
