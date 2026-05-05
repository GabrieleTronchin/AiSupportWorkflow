namespace AiSupportWorkflow.Infrastructure.AgentFramework;

using System.Text.Json;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

public class IssueClassifierService(IChatClient chatClient, ILogger<IssueClassifierService> logger) : IIssueClassifier
{
    private static readonly ChatOptions Options = new() { Temperature = 0.1f };

    private const string SystemPrompt = """
        You are a support email classifier. Analyze the email and classify it.
        Respond ONLY with a JSON object in this exact format (no markdown, no extra text):
        {"category":"BackendBug|FrontendBug|QualityTestIssue|OutOfScope","confidence":0.0,"reasoning":"..."}
        
        Categories:
        - BackendBug: API errors, database issues, server-side logic problems
        - FrontendBug: UI rendering issues, component errors, styling problems
        - QualityTestIssue: Test failures, missing coverage, flaky tests
        - OutOfScope: Not a code-related issue (billing, general questions, etc.)
        """;

    public async Task<ClassificationResult> ClassifyAsync(IssueRecord issue, CancellationToken ct = default)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, $"Subject: {issue.Subject}\n\nBody: {issue.Body}")
            };

            var response = await chatClient.GetResponseAsync(messages, Options, ct);
            return ParseClassificationResponse(response.Text ?? "");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM classification failed for issue {IssueId}", issue.Id);
            return new ClassificationResult(false, IssueCategory.OutOfScope, 0.0, "Classification failed — manual review required");
        }
    }

    internal static ClassificationResult ParseClassificationResponse(string responseText)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            var categoryStr = root.GetProperty("category").GetString() ?? "OutOfScope";
            var confidence = root.GetProperty("confidence").GetDouble();
            var reasoning = root.GetProperty("reasoning").GetString() ?? "";

            var category = Enum.TryParse<IssueCategory>(categoryStr, ignoreCase: true, out var parsed)
                ? parsed
                : IssueCategory.OutOfScope;

            var isCodeRelated = category is not IssueCategory.OutOfScope;
            confidence = Math.Clamp(confidence, 0.0, 1.0);

            return new ClassificationResult(isCodeRelated, category, confidence, reasoning);
        }
        catch
        {
            return new ClassificationResult(false, IssueCategory.OutOfScope, 0.0, "Failed to parse LLM response — manual review required");
        }
    }
}
