namespace AiSupportWorkflow.Infrastructure.AgentFramework;

using System.ClientModel;
using AiSupportWorkflow.Infrastructure.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using OpenAI;

public static class ChatClientSetup
{
    public static IServiceCollection AddChatClient(this IServiceCollection services, IConfiguration configuration)
    {
        var config = configuration.GetSection("LlmProvider").Get<LlmProviderConfiguration>()
            ?? new LlmProviderConfiguration();

        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new InvalidOperationException(
                "LLM API key is not configured. Set 'LlmProvider:ApiKey' in appsettings.Development.json.");
        }

        switch (config.Provider.ToLowerInvariant())
        {
            case "openai":
                var openAiClient = new OpenAIClient(new ApiKeyCredential(config.ApiKey));
                IChatClient chatClient = openAiClient
                    .GetChatClient(config.ModelName)
                    .AsIChatClient();
                services.AddSingleton(chatClient);
                break;
            default:
                throw new InvalidOperationException($"Unsupported LLM provider: {config.Provider}");
        }

        services.ConfigureHttpClientDefaults(builder =>
        {
            builder.AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.UseJitter = true;
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            });
        });

        return services;
    }
}
