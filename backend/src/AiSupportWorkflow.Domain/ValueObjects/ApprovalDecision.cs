namespace AiSupportWorkflow.Domain.ValueObjects;

public record ApprovalDecision(bool Approved, string? Reason = null);
