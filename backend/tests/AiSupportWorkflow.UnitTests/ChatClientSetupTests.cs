namespace AiSupportWorkflow.UnitTests;

using AiSupportWorkflow.Infrastructure.AgentFramework;
using AiSupportWorkflow.Infrastructure.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class ChatClientSetupTests
{
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
            ModelName = "qwen2.5:7b",
            Endpoint = "http://localhost:11434/v1",
        });

        Assert.Equal(new Uri("http://localhost:11434/v1"), settings.Endpoint);
        Assert.Equal("qwen2.5:7b", settings.ModelName);
    }

    [Theory]
    [InlineData("gpt-4o-mini", "openai/gpt-4o-mini")]
    [InlineData("openai/gpt-4.1-mini", "openai/gpt-4.1-mini")]
    public void ResolveSettings_GitHubModels_DefaultsEndpointAndQualifiesModel(string configured, string expected)
    {
        var settings = ChatClientSetup.ResolveSettings(new LlmProviderConfiguration
        {
            Provider = "GitHubModels",
            ModelName = configured,
        });

        Assert.Equal(new Uri(ChatClientSetup.GitHubModelsEndpoint), settings.Endpoint);
        Assert.Equal(expected, settings.ModelName);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://example.com")]
    public void ResolveSettings_InvalidEndpoint_Throws(string endpoint)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ChatClientSetup.ResolveSettings(new LlmProviderConfiguration
        {
            Provider = "GitHubModels",
            Endpoint = endpoint,
        }));

        Assert.Contains("Invalid LLM endpoint", ex.Message);
    }

    [Fact]
    public void AddChatClient_GitHubModels_RegistersChatClient()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LlmProvider:Provider"] = "GitHubModels",
                ["LlmProvider:ApiKey"] = "test-token",
                ["LlmProvider:ModelName"] = "gpt-4o-mini",
            })
            .Build();

        using var provider = new ServiceCollection().AddChatClient(configuration).BuildServiceProvider();

        Assert.NotNull(provider.GetService<IChatClient>());
    }
}
