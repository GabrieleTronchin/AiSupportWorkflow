namespace AiSupportWorkflow.Infrastructure.Configuration;

public class LlmProviderConfiguration
{
    /// <summary><c>OpenAI</c> (default) or <c>Demo</c> (simulated responses, no API key needed).</summary>
    public string Provider { get; set; } = "OpenAI";
    public string ModelName { get; set; } = "gpt-4o-mini";
    public string ApiKey { get; set; } = "";

    /// <summary>Optional OpenAI-compatible endpoint (e.g. Groq, Ollama). Defaults to OpenAI.</summary>
    public string? Endpoint { get; set; }

    /// <summary>Simulated latency per LLM call in Demo mode, so pipeline progress is visible in the UI.</summary>
    public int DemoLatencyMs { get; set; } = 1500;

    public bool IsDemo => Provider.Equals("Demo", StringComparison.OrdinalIgnoreCase);
}
