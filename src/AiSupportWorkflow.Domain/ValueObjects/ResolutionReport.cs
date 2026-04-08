namespace AiSupportWorkflow.Domain.ValueObjects;

public record ResolutionReport(
    Guid IssueId,
    string RootCauseDescription,
    string AffectedComponent,
    string SeverityAssessment,
    string ProposedFixSummary,
    bool RequiresEscalation,
    string? EscalationReason);
