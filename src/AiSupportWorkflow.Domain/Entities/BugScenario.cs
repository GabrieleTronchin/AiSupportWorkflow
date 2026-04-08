namespace AiSupportWorkflow.Domain.Entities;

using AiSupportWorkflow.Domain.Enums;

public record BugScenario(
    string ScenarioId,
    string ApplicationName,
    IssueCategory Category,
    string Description,
    string AffectedFile,
    string AffectedLineRange,
    string BuggyCode,
    string FixedCode);
