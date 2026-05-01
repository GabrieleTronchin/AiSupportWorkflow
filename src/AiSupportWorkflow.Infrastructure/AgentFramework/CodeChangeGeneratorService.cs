namespace AiSupportWorkflow.Infrastructure.AgentFramework;

using System.Text.Json;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

public class CodeChangeGeneratorService(IChatClient chatClient, ILogger<CodeChangeGeneratorService> logger) : ICodeChangeGenerator
{
    private static readonly ChatOptions Options = new() { Temperature = 0.5f };

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
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, BuildPrompt(resolution))
            };

            var response = await chatClient.GetResponseAsync(messages, Options, ct);
            return ParsePullRequestResponse(resolution.IssueId, response.Text ?? "");
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

    internal static PullRequest ParsePullRequestResponse(Guid issueId, string responseText)
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
