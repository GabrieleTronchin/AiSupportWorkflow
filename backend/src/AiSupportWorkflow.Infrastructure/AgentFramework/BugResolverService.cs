namespace AiSupportWorkflow.Infrastructure.AgentFramework;

using System.Text.Json;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

public class BugResolverService(IChatClient chatClient, ILogger<BugResolverService> logger) : IBugResolver
{
    private static readonly ChatOptions Options = new() { Temperature = 0.2f };

    private const string SystemPrompt = """
        You are a senior software engineer performing root cause analysis.
        Analyze the issue and produce a resolution report.
        Respond ONLY with a JSON object in this exact format (no markdown, no extra text):
        {
          "rootCause":"...",
          "affectedComponent":"...",
          "severity":"Low|Medium|High|Critical",
          "proposedFix":"...",
          "requiresEscalation":false,
          "escalationReason":null
        }
        If you cannot determine the root cause, set requiresEscalation to true and provide an escalationReason.
        """;

    public async Task<ResolutionReport> ResolveAsync(IssueRecord issue, AgentAssignment agent, CancellationToken ct = default)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, $"Agent: {agent.AgentId} ({agent.Role})\nSubject: {issue.Subject}\n\nBody: {issue.Body}")
            };

            var response = await chatClient.GetResponseAsync(messages, Options, ct);
            return ParseResolutionResponse(issue.Id, response.Text ?? "");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM resolution failed for issue {IssueId}", issue.Id);
            return EscalatedReport(issue.Id, $"LLM error: {ex.Message}");
        }
    }

    internal static ResolutionReport ParseResolutionResponse(Guid issueId, string responseText)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            return new ResolutionReport(
                IssueId: issueId,
                RootCauseDescription: root.GetProperty("rootCause").GetString() ?? "",
                AffectedComponent: root.GetProperty("affectedComponent").GetString() ?? "",
                SeverityAssessment: root.GetProperty("severity").GetString() ?? "Medium",
                ProposedFixSummary: root.GetProperty("proposedFix").GetString() ?? "",
                RequiresEscalation: root.GetProperty("requiresEscalation").GetBoolean(),
                EscalationReason: root.TryGetProperty("escalationReason", out var esc) ? esc.GetString() : null);
        }
        catch
        {
            return EscalatedReport(issueId, "Failed to parse LLM resolution response");
        }
    }

    private static ResolutionReport EscalatedReport(Guid issueId, string reason) =>
        new(issueId, "", "", "Unknown", "", RequiresEscalation: true, EscalationReason: reason);
}
