namespace AiSupportWorkflow.Infrastructure.Configuration;

public class LlmProviderConfiguration
{
    public const string DemoProvider = "Demo";
    public const string OpenAIProvider = "OpenAI";

    /// <summary>
    /// <c>Demo</c> (default: simulated responses, no API key needed) or <c>OpenAI</c>
    /// (any OpenAI-compatible API, see <see cref="Endpoint"/>). Override it in appsettings to use a real model.
    /// </summary>
    public string Provider { get; set; } = DemoProvider;
    public string ModelName { get; set; } = "gpt-4o-mini";
    public string ApiKey { get; set; } = "";

    /// <summary>Optional OpenAI-compatible endpoint (e.g. Groq, Ollama). Defaults to OpenAI.</summary>
    public string? Endpoint { get; set; }

    /// <summary>Simulated latency per LLM call in Demo mode, so pipeline progress is visible in the UI.</summary>
    public int DemoLatencyMs { get; set; } = 1500;

    public bool IsDemo => Provider.Equals(DemoProvider, StringComparison.OrdinalIgnoreCase);
}
