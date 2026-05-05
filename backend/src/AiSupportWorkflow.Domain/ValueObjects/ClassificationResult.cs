namespace AiSupportWorkflow.Domain.ValueObjects;

using AiSupportWorkflow.Domain.Enums;

public record ClassificationResult(bool IsCodeRelated, IssueCategory Category, double ConfidenceScore, string Reasoning);
