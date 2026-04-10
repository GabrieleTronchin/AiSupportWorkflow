namespace AiSupportWorkflow.Infrastructure.SemanticKernel;

using AiSupportWorkflow.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.SemanticKernel;

public static class SemanticKernelSetup
{
    public static IServiceCollection AddSemanticKernel(this IServiceCollection services, IConfiguration configuration)
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
                services.AddOpenAIChatCompletion(config.ModelName, config.ApiKey);
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
