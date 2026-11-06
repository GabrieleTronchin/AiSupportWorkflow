namespace AiSupportWorkflow.UnitTests;

using System.Text.Json;
using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Infrastructure.Configuration;
using AiSupportWorkflow.Presentation.Endpoints;

public class ConfigEndpointsTests
{
    [Fact]
    public void BuildResponse_DemoProvider_ReportsDemoModeAndSimulatedModel()
    {
        var response = ConfigEndpoints.BuildResponse(new WorkflowConfiguration(), new LlmProviderConfiguration { Provider = "demo" });

        Assert.True(response.DemoMode);
        Assert.Equal("Demo", response.LlmProvider);
        Assert.Equal("demo-simulated", response.LlmModel);
    }

    [Fact]
    public void BuildResponse_OpenAiProvider_ReportsProviderAndModel()
    {
        var response = ConfigEndpoints.BuildResponse(
            new WorkflowConfiguration { SequentialProcessing = true },
            new LlmProviderConfiguration { Provider = "OpenAI", ModelName = "gpt-4o-mini", ApiKey = "sk-secret", Endpoint = "https://internal.example.com/v1" });

        Assert.False(response.DemoMode);
        Assert.True(response.SequentialProcessing);
        Assert.Equal("OpenAI", response.LlmProvider);
        Assert.Equal("gpt-4o-mini", response.LlmModel);
    }

    [Fact]
    public void BuildResponse_NeverExposesSecretsOrEndpoint()
    {
        var response = ConfigEndpoints.BuildResponse(
            new WorkflowConfiguration(),
            new LlmProviderConfiguration { Provider = "OpenAI", ApiKey = "sk-secret", Endpoint = "https://internal.example.com/v1" });

        var json = JsonSerializer.Serialize(response);

        Assert.DoesNotContain("sk-secret", json);
        Assert.DoesNotContain("internal.example.com", json);
    }
}
