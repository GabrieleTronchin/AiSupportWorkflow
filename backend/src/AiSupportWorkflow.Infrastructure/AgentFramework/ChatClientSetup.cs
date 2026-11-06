namespace AiSupportWorkflow.Infrastructure.AgentFramework;

using System.ClientModel;
using AiSupportWorkflow.Infrastructure.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using OpenAI;

public static class ChatClientSetup
{
    public static IServiceCollection AddChatClient(this IServiceCollection services, IConfiguration configuration)
    {
        var config = configuration.GetSection("LlmProvider").Get<LlmProviderConfiguration>()
            ?? new LlmProviderConfiguration();

        var innerClient = config.IsDemo ? CreateDemoClient(config) : CreateOpenAiCompatibleClient(config);

        // Every LLM call goes through the telemetry middleware (Agents page: tokens, calls, latency).
        services.TryAddSingleton<LlmTelemetryStore>();
        services.AddSingleton<IChatClient>(sp => new LlmTelemetryMiddleware(
            innerClient,
            sp.GetRequiredService<ILogger<LlmTelemetryMiddleware>>(),
            sp.GetRequiredService<LlmTelemetryStore>()));

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

    /// <summary>
    /// Maps the configured provider to the endpoint and model name used by the OpenAI-compatible client.
    /// </summary>
    internal static ChatClientSettings ResolveSettings(LlmProviderConfiguration config) =>
        config.Provider.ToLowerInvariant() switch
        {
            // Endpoint is optional: when set, any OpenAI-compatible server (Groq, Ollama, ...) can be used.
            "openai" => new(ParseEndpoint(config.Endpoint), config.ModelName),
            _ => throw new InvalidOperationException($"Unsupported LLM provider: {config.Provider}")
        };

    private static IChatClient CreateDemoClient(LlmProviderConfiguration config) =>
        new DemoChatClient(TimeSpan.FromMilliseconds(Math.Max(0, config.DemoLatencyMs)));

    private static IChatClient CreateOpenAiCompatibleClient(LlmProviderConfiguration config)
    {
        var settings = ResolveSettings(config);

        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new InvalidOperationException(
                "LLM API key is not configured. Set 'LlmProvider:ApiKey' in appsettings.Development.json, " +
                "or set 'LlmProvider:Provider' to 'Demo' to use simulated responses.");
        }

        var options = new OpenAIClientOptions();
        if (settings.Endpoint is not null)
        {
            options.Endpoint = settings.Endpoint;
        }

        return new OpenAIClient(new ApiKeyCredential(config.ApiKey), options)
            .GetChatClient(settings.ModelName)
            .AsIChatClient();
    }

    private static Uri? ParseEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri
            : throw new InvalidOperationException($"Invalid LLM endpoint: '{endpoint}'. Expected an absolute http(s) URL.");
    }
}

internal sealed record ChatClientSettings(Uri? Endpoint, string ModelName);
