namespace AiSupportWorkflow.Infrastructure.AgentFramework;

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Extensions.AI;

/// <summary>
/// Offline <see cref="IChatClient"/> used when <c>LlmProvider:Provider</c> is <c>Demo</c>.
/// Returns deterministic, plausible structured responses so the whole pipeline and the
/// dashboard can be exercised without an LLM provider or API credits.
/// </summary>
internal sealed partial class DemoChatClient(TimeSpan latency) : IChatClient
{
    public const string DemoModelId = "demo-simulated";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = string.Join("\n", messages.Select(m => m.Text));

        if (latency > TimeSpan.Zero)
        {
            // Simulated model latency, so the dashboard shows each stage in progress.
            await Task.Delay(latency, cancellationToken);
        }

        var json = DetectRequest(prompt, options) switch
        {
            DemoRequest.Resolution => Serialize(BuildResolution(prompt)),
            DemoRequest.PullRequest => Serialize(BuildPullRequest(prompt)),
            _ => Serialize(BuildClassification(prompt)),
        };

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, json))
        {
            ModelId = DemoModelId,
            Usage = new UsageDetails
            {
                InputTokenCount = EstimateTokens(prompt),
                OutputTokenCount = EstimateTokens(json),
            },
        };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text) { ModelId = DemoModelId };
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
        // Nothing to dispose
    }

    private enum DemoRequest
    {
        Classification,
        Resolution,
        PullRequest,
    }

    /// <summary>
    /// Identifies the requested output from the JSON schema (structured output) and falls back
    /// to the prompt shapes used by the workflow executors.
    /// </summary>
    private static DemoRequest DetectRequest(string prompt, ChatOptions? options)
    {
        var schema = (options?.ResponseFormat as ChatResponseFormatJson)?.Schema?.GetRawText() ?? string.Empty;
        var haystack = schema + "\n" + prompt;

        if (haystack.Contains(nameof(ResolutionReport.RootCauseDescription), StringComparison.Ordinal)
            || prompt.StartsWith("Agent:", StringComparison.Ordinal))
        {
            return DemoRequest.Resolution;
        }

        if (haystack.Contains(nameof(PullRequest.SimulatedDiff), StringComparison.Ordinal)
            || prompt.StartsWith("Issue ID:", StringComparison.Ordinal))
        {
            return DemoRequest.PullRequest;
        }

        return DemoRequest.Classification;
    }

    internal static ClassificationResult BuildClassification(string prompt)
    {
        var text = prompt.ToLowerInvariant();

        if (QaPattern().IsMatch(text))
        {
            return new(true, IssueCategory.QualityTestIssue, 0.88, "[Demo] Mentions tests, coverage or flakiness.");
        }

        if (FrontendPattern().IsMatch(text))
        {
            return new(true, IssueCategory.FrontendBug, 0.9, "[Demo] Mentions UI components or rendering.");
        }

        if (BackendPattern().IsMatch(text))
        {
            return new(true, IssueCategory.BackendBug, 0.92, "[Demo] Mentions exceptions, APIs or data access.");
        }

        return new(false, IssueCategory.OutOfScope, 0.95, "[Demo] No code-related signal found; looks like a general support request.");
    }

    internal static ResolutionReport BuildResolution(string prompt)
    {
        var subject = MatchLine(prompt, "Subject") ?? "the reported issue";
        var component = SourceFilePattern().Match(prompt) is { Success: true } file ? file.Value : "UnknownComponent";
        var text = prompt.ToLowerInvariant();
        var severity = text.Contains("injection") || text.Contains("security") ? "Critical"
            : text.Contains("exception") || text.Contains("crash") ? "High"
            : "Medium";

        // IssueId is not known from the prompt; the ResolutionExecutor sets the real one.
        return new(
            Guid.Empty,
            $"[Demo] Simulated root cause analysis for \"{subject}\": the defect originates in {component}.",
            component,
            severity,
            $"[Demo] Apply a targeted fix in {component} and add a regression test.",
            RequiresEscalation: false,
            EscalationReason: null);
    }

    internal static PullRequest BuildPullRequest(string prompt)
    {
        var issueId = Guid.TryParse(MatchLine(prompt, "Issue ID"), out var id) ? id : Guid.Empty;
        var component = MatchLine(prompt, "Affected Component") ?? "UnknownComponent";
        var fix = MatchLine(prompt, "Proposed Fix") ?? "[Demo] Simulated fix";
        var path = $"src/{component}";

        return new(
            Guid.NewGuid(),
            issueId,
            $"[Demo] Fix {component}",
            fix,
            [path],
            $"""
            --- a/{path}
            +++ b/{path}
            @@ -1,3 +1,4 @@
            +// [Demo] simulated change: {fix}
             // existing code
            """);
    }

    private static string? MatchLine(string prompt, string label)
    {
        var match = Regex.Match(prompt, $@"^{Regex.Escape(label)}:\s*(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    // Rough approximation (~4 characters per token) for simulated usage telemetry.
    private static long EstimateTokens(string text) => Math.Max(1, text.Length / 4);

    [GeneratedRegex(@"\btests?\b|flaky|coverage")]
    private static partial Regex QaPattern();

    [GeneratedRegex(@"frontend|front-end|\.razor|component|\bcss\b|\bui\b|button|render|display")]
    private static partial Regex FrontendPattern();

    [GeneratedRegex(@"exception|error|crash|\bnull\b|\bsql\b|\bapi\b|endpoint|database|\bbug\b|latency|controller|injection|timeout")]
    private static partial Regex BackendPattern();

    [GeneratedRegex(@"\b[A-Z]\w*\.(?:cs|razor)\b")]
    private static partial Regex SourceFilePattern();
}
