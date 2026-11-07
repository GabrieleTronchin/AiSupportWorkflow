namespace Flowgent.Engine.Tests.Runtime;

using System.Text.Json.Nodes;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Flowgent.Contracts.Runs;
using Flowgent.Engine.Service.Endpoints;
using Flowgent.Engine.Tests.Helpers;
using Flowgent.Runtime.Configuration;
using Flowgent.Runtime.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

public class LlmTests
{
    private static readonly Dictionary<string, string?> RealProvider = new()
    {
        ["Flowgent:Llm:Provider"] = "OpenAI",
        ["Flowgent:Llm:ApiKey"] = "test-key",
    };

    // ── Chat client setup ──────────────────────────────────────────────

    [Fact]
    public void ResolveSettings_OpenAiWithoutEndpoint_UsesSdkDefaultEndpoint()
    {
        var settings = ChatClientSetup.ResolveSettings(new LlmOptions { Provider = "OpenAI", ModelName = "gpt-4o-mini" });

        Assert.Null(settings.Endpoint);
        Assert.Equal("gpt-4o-mini", settings.ModelName);
    }

    [Fact]
    public void ResolveSettings_OpenAiWithEndpoint_UsesCustomEndpoint()
    {
        var settings = ChatClientSetup.ResolveSettings(new LlmOptions
        {
            Provider = "openai",
            Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/",
            ModelName = "gemini-flash",
        });

        Assert.Equal(new Uri("https://generativelanguage.googleapis.com/v1beta/openai/"), settings.Endpoint);
        Assert.Equal("gemini-flash", settings.ModelName);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://example.com")]
    public void ResolveSettings_InvalidEndpoint_Throws(string endpoint)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ChatClientSetup.ResolveSettings(new LlmOptions { Provider = "OpenAI", Endpoint = endpoint }));

        Assert.Contains("Invalid LLM endpoint", ex.Message);
    }

    // Any provider other than OpenAI(-compatible) is rejected with a clear message.
    [Property(MaxTest = 100)]
    public Property UnsupportedProvider_Throws(NonEmptyString provider)
    {
        var name = provider.Get;
        if (name.Equals("openai", StringComparison.OrdinalIgnoreCase))
        {
            return true.ToProperty();
        }

        var ex = Record.Exception(() => ChatClientSetup.ResolveSettings(new LlmOptions { Provider = name }));
        return (ex is InvalidOperationException && ex.Message.Contains("Unsupported LLM provider")).ToProperty();
    }

    [Fact]
    public void Defaults_AreDemo()
    {
        Assert.True(new LlmOptions().IsDemo);
        using var provider = Build([]);
        Assert.IsType<LlmTelemetryMiddleware>(provider.GetRequiredService<IChatClient>());
    }

    [Fact]
    public void EngineAppSettings_DefaultProviderIsDemo()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .Build();

        Assert.Equal("Demo", config["Flowgent:Llm:Provider"]);
    }

    [Fact]
    public void OpenAiWithoutApiKey_ThrowsWithDemoHint()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(new() { ["Flowgent:Llm:Provider"] = "OpenAI" }));

        Assert.Contains("Demo", ex.Message);
    }

    [Fact]
    public void OpenAiWithApiKey_BuildsTheClient()
    {
        using var provider = Build(RealProvider);

        Assert.NotNull(provider.GetRequiredService<IChatClient>());
    }

    [Fact]
    public void RuntimeConfig_NeverExposesSecretsOrEndpoint()
    {
        var info = EngineEndpoints.BuildConfig(new EngineOptions(), new LlmOptions
        {
            Provider = "OpenAI",
            ModelName = "gemini-flash",
            ApiKey = "sk-secret",
            Endpoint = "https://secret.example/v1",
        });

        Assert.False(info.DemoMode);
        Assert.Equal("OpenAI", info.LlmProvider);
        Assert.Equal("gemini-flash", info.LlmModel);
        var json = System.Text.Json.JsonSerializer.Serialize(info);
        Assert.DoesNotContain("sk-secret", json);
        Assert.DoesNotContain("secret.example", json);
    }

    [Fact]
    public void RuntimeConfig_DemoReportsSimulatedModel()
    {
        var info = EngineEndpoints.BuildConfig(new EngineOptions { SequentialProcessing = true }, new LlmOptions { Provider = "demo" });

        Assert.True(info.DemoMode);
        Assert.True(info.SequentialProcessing);
        Assert.Equal("Demo", info.LlmProvider);
        Assert.Equal(DemoChatClient.DemoModelId, info.LlmModel);
    }

    // ── Telemetry middleware ───────────────────────────────────────────

    [Fact]
    public async Task Middleware_AttributesCallsAndNeverForwardsInternalKeys()
    {
        var inner = new FakeChatClient("{}");
        var store = new LlmTelemetryStore();
        var sut = new LlmTelemetryMiddleware(inner, NullLogger<LlmTelemetryMiddleware>.Instance, store);
        var runId = Guid.NewGuid();
        var options = LlmCallAttribution.ForAgent(new ChatOptions { Temperature = 0.2f }, "TeamA_BackendDeveloper", "support", "support-email", runId, "resolve");

        await sut.GetResponseAsync([new(ChatRole.User, "hi")], options, TestContext.Current.CancellationToken);

        var telemetry = store.GetAgentTelemetry("TeamA_BackendDeveloper");
        Assert.Equal(1, telemetry.TotalCalls);
        Assert.Equal(10, telemetry.TotalPromptTokens);
        Assert.Equal(5, telemetry.TotalCompletionTokens);
        Assert.Equal(runId, telemetry.LastCall!.RunId);
        Assert.Equal("resolve", telemetry.LastCall.StepId);
        Assert.Equal("support", telemetry.LastCall.ModuleId);

        var forwarded = Assert.Single(inner.Calls).Options!;
        Assert.Equal(0.2f, forwarded.Temperature);
        Assert.Null(forwarded.AdditionalProperties);
    }

    [Fact]
    public void ForAgent_DoesNotMutateSharedOptions()
    {
        var shared = new ChatOptions { Temperature = 0.1f };

        _ = LlmCallAttribution.ForAgent(shared, "TeamB_QAEngineer");

        Assert.Null(shared.AdditionalProperties);
    }

    [Fact]
    public async Task Middleware_CallWithoutAgent_IsCountedGloballyOnly()
    {
        var store = new LlmTelemetryStore();
        var sut = new LlmTelemetryMiddleware(new FakeChatClient("{}"), NullLogger<LlmTelemetryMiddleware>.Instance, store);

        await sut.GetResponseAsync([new(ChatRole.User, "hi")], new ChatOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(1, store.GetSummary().TotalCalls);
        Assert.Equal(0, store.GetAgentTelemetry("TeamA_BackendDeveloper").TotalCalls);
    }

    [Fact]
    public async Task Middleware_RecordsFailures()
    {
        var store = new LlmTelemetryStore();
        var sut = new LlmTelemetryMiddleware(FakeChatClient.Throwing(new HttpRequestException("503 high demand")), NullLogger<LlmTelemetryMiddleware>.Instance, store);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetResponseAsync([new(ChatRole.User, "hi")], null, TestContext.Current.CancellationToken));

        var summary = store.GetSummary();
        Assert.Equal(1, summary.TotalCalls);
        Assert.Equal(1.0, summary.ErrorRate);
        Assert.Equal("503 high demand", store.GetCalls().Single().ErrorMessage);
    }

    [Fact]
    public async Task StripDemoResponse_RemovesTheDemoKeyBeforeRealProviders()
    {
        var inner = new FakeChatClient("{}");
        var sut = new StripDemoResponseChatClient(inner);

        await sut.GetResponseAsync([new(ChatRole.User, "hi")], LlmCallAttribution.WithDemoResponse(new ChatOptions(), "{}"), TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(inner.Calls).Options!.AdditionalProperties);
    }

    // ── Demo client ────────────────────────────────────────────────────

    [Fact]
    public async Task DemoClient_ReturnsProvidedResponseWithUsageAndDemoModel()
    {
        var client = new DemoChatClient(TimeSpan.Zero);

        var response = await client.GetResponseAsync(
            [new(ChatRole.User, "A prompt of some length")],
            LlmCallAttribution.WithDemoResponse(new ChatOptions(), """{"a":1}"""),
            TestContext.Current.CancellationToken);

        Assert.Equal("""{"a":1}""", response.Text);
        Assert.Equal(DemoChatClient.DemoModelId, response.ModelId);
        Assert.True(response.Usage!.InputTokenCount > 0);
        Assert.True(response.Usage.OutputTokenCount > 0);
    }

    [Fact]
    public async Task DemoClient_WithoutResponse_Throws()
    {
        var client = new DemoChatClient(TimeSpan.Zero);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync([new(ChatRole.User, "x")], null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DemoClient_LatencyIsCancellable()
    {
        var client = new DemoChatClient(TimeSpan.FromMinutes(5));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetResponseAsync([new(ChatRole.User, "x")], LlmCallAttribution.WithDemoResponse(new ChatOptions(), "{}"), cts.Token));
    }

    // ── llm step with a real (fake) provider ───────────────────────────

    [Fact]
    public async Task LlmStep_SendsPromptsSchemaAndTemperature()
    {
        var chat = new FakeChatClient("""{"summary":"From the model output"}""");
        await using var engine = await StartRealAsync(chat);

        var run = await engine.RunAsync(new JsonObject { ["text"] = "Agent Framework workflows in YAML" });

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal("From the model output", run.Detail);
        var (messages, options) = Assert.Single(chat.Calls);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Equal("You are A concise writer.", messages[0].Text);
        Assert.Equal("Summarise Agent Framework workflows in YAML", messages[1].Text);
        Assert.Equal(0.2f, options!.Temperature);
        var format = Assert.IsType<ChatResponseFormatJson>(options.ResponseFormat);
        Assert.Equal("summarize", format.SchemaName);
        Assert.Contains("\"additionalProperties\":false", format.Schema!.Value.GetRawText());
        Assert.Null(options.AdditionalProperties);
    }

    [Theory]
    [InlineData("not json", "is not valid JSON")]
    [InlineData("""{"other":1}""", "does not match its schema")]
    [InlineData("""{"summary":1}""", "does not match its schema")]
    public async Task LlmStep_InvalidResponse_FailsTheRun(string response, string expected)
    {
        await using var engine = await StartRealAsync(new FakeChatClient(response));

        var run = await engine.RunAsync(new JsonObject { ["text"] = "x" });

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("summarize", run.CurrentStepId);
        Assert.Contains(expected, run.Detail);
    }

    [Fact]
    public async Task LlmStep_ResponseInCodeFence_IsAccepted()
    {
        await using var engine = await StartRealAsync(new FakeChatClient("```json\n{\"summary\":\"fenced\"}\n```"));

        var run = await engine.RunAsync(new JsonObject { ["text"] = "Agent Framework workflows in YAML" });

        Assert.Equal("fenced", run.StepOutputs["summarize"]!["summary"]!.GetValue<string>());
    }

    [Fact]
    public async Task LlmStep_ProviderError_FailsTheRunAndRecordsTelemetry()
    {
        await using var engine = await StartRealAsync(FakeChatClient.Throwing(new HttpRequestException("Service unavailable")));

        var run = await engine.RunAsync(new JsonObject { ["text"] = "x" });

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("LLM call failed for step 'summarize': Service unavailable", run.Detail);
        Assert.False(engine.Telemetry.GetCalls().Single().Success);
    }

    // The temperature declared in YAML is the one sent to the provider.
    [Property(MaxTest = 30)]
    public Property Temperature_IsPreserved(NormalFloat raw)
    {
        var temperature = Math.Round(Math.Clamp(Math.Abs(raw.Get) % 2.0, 0, 2), 2);
        return Prop.ToProperty(CheckTemperatureAsync(temperature).GetAwaiter().GetResult());
    }

    private static async Task<bool> CheckTemperatureAsync(double temperature)
    {
        var chat = new FakeChatClient("""{"summary":"x y z w"}""");
        var text = temperature.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await using var engine = await StartRealAsync(chat, f => f.Replace("workflows/hello.yaml", "temperature: 0.2", $"temperature: {text}"));
        await engine.RunAsync(new JsonObject { ["text"] = "x" });
        return Math.Abs((double)chat.Calls.Single().Options!.Temperature!.Value - temperature) < 0.0001;
    }

    private static Task<TestEngine> StartRealAsync(FakeChatClient chat, Action<Dictionary<string, string>>? patch = null) =>
        TestEngine.StartAsync(
            patch,
            services => services.AddSingleton<IChatClient>(sp => new LlmTelemetryMiddleware(chat, NullLogger<LlmTelemetryMiddleware>.Instance, sp.GetRequiredService<LlmTelemetryStore>())),
            RealProvider);

    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFlowgentChatClient(config);
        return services.BuildServiceProvider();
    }
}
