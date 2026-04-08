namespace AiSupportWorkflow.Infrastructure.SemanticKernel;

using System.Text.Json;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

public class IssueClassifierService(Kernel kernel, ILogger<IssueClassifierService> logger) : IIssueClassifier
{
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
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory(SystemPrompt);
            history.AddUserMessage($"Subject: {issue.Subject}\n\nBody: {issue.Body}");

            var response = await chatService.GetChatMessageContentAsync(history, cancellationToken: ct);
            return ParseClassificationResponse(response.Content ?? "");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM classification failed for issue {IssueId}", issue.Id);
            return new ClassificationResult(false, IssueCategory.OutOfScope, 0.0, "Classification failed — manual review required");
        }
    }

    private static ClassificationResult ParseClassificationResponse(string responseText)
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
