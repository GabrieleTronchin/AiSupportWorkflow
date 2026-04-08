namespace AiSupportWorkflow.Infrastructure.SemanticKernel;

using AiSupportWorkflow.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

public static class SemanticKernelSetup
{
    public static IServiceCollection AddSemanticKernel(this IServiceCollection services, IConfiguration configuration)
    {
        var openAiConfig = configuration.GetSection("OpenAI").Get<OpenAIConfiguration>() ?? new OpenAIConfiguration();
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? openAiConfig.ApiKey;
        var modelName = openAiConfig.ModelName;

        var builder = services.AddKernel();
        builder.AddOpenAIChatCompletion(modelName, apiKey);

        return services;
    }
}
