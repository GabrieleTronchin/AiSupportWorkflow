namespace AiSupportWorkflow.Infrastructure.Configuration;

public class LlmProviderConfiguration
{
    public string Provider { get; set; } = "OpenAI";
    public string ModelName { get; set; } = "gpt-4o-mini";
    public string ApiKey { get; set; } = "";
    public string? Endpoint { get; set; }
}
