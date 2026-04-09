namespace AiSupportWorkflow.Infrastructure.SemanticKernel;

using System.Text.Json;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

public class CodeChangeGeneratorService(IChatCompletionService chatService, ILogger<CodeChangeGeneratorService> logger) : ICodeChangeGenerator
{
    private static readonly PromptExecutionSettings Settings = new()
    {
        ExtensionData = new Dictionary<string, object> { ["temperature"] = 0.5 }
    };

    private const string SystemPrompt = """
        You are a code change generator. Given a resolution report, produce a simulated code fix.
        Respond ONLY with a JSON object in this exact format (no markdown, no extra text):
        {
          "title":"PR title",
          "description":"PR description",
          "affectedFiles":["path/to/file1.cs","path/to/file2.cs"],
          "diff":"--- a/file\n+++ b/file\n@@ ... @@\n-old line\n+new line"
        }
        File paths must be under DummyApps/ApplicationA/ or DummyApps/ApplicationB/.
        """;

    public async Task<PullRequest> GenerateAsync(ResolutionReport resolution, CancellationToken ct = default)
    {
        try
        {
            var history = new ChatHistory(SystemPrompt);
            history.AddUserMessage(BuildPrompt(resolution));

            var response = await chatService.GetChatMessageContentAsync(history, Settings, cancellationToken: ct);
            return ParsePullRequestResponse(resolution.IssueId, response.Content ?? "");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM code generation failed for issue {IssueId}", resolution.IssueId);
            return FallbackPullRequest(resolution);
        }
    }

    private static string BuildPrompt(ResolutionReport resolution) =>
        $"""
        Issue ID: {resolution.IssueId}
        Root Cause: {resolution.RootCauseDescription}
        Affected Component: {resolution.AffectedComponent}
        Severity: {resolution.SeverityAssessment}
        Proposed Fix: {resolution.ProposedFixSummary}
        """;

    private static PullRequest ParsePullRequestResponse(Guid issueId, string responseText)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            var title = root.GetProperty("title").GetString() ?? "Fix issue";
            var description = root.GetProperty("description").GetString() ?? "";
            var diff = root.GetProperty("diff").GetString() ?? "";

            var files = root.GetProperty("affectedFiles")
                .EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .ToList();

            return new PullRequest(Guid.NewGuid(), issueId, title, description, files, diff);
        }
        catch
        {
            return new PullRequest(Guid.NewGuid(), issueId, "Fix issue", "Auto-generated fix", ["DummyApps/ApplicationA/src/Program.cs"], "// simulated diff");
        }
    }

    private static PullRequest FallbackPullRequest(ResolutionReport resolution) =>
        new(Guid.NewGuid(), resolution.IssueId,
            $"Fix: {resolution.AffectedComponent}",
            resolution.ProposedFixSummary,
            [$"DummyApps/ApplicationA/src/{resolution.AffectedComponent}.cs"],
            $"// Simulated diff for {resolution.AffectedComponent}");
}
