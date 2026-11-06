namespace AiSupportWorkflow.Domain.Enums;

public static class WorkflowStageExtensions
{
    /// <summary>
    /// Stages after which the workflow run has ended and nothing else will happen to the issue.
    /// Single source of truth for state tracking, agent status and sequential inbox processing.
    /// </summary>
    public static bool IsTerminal(this WorkflowStage stage) => stage is
        WorkflowStage.Failed
        or WorkflowStage.CodeChangeGenerated
        or WorkflowStage.ClassifiedOutOfScope
        or WorkflowStage.ManualReviewRequired;

    /// <summary>All terminal stages (useful for database queries).</summary>
    public static IReadOnlyList<WorkflowStage> TerminalStages { get; } =
        [.. Enum.GetValues<WorkflowStage>().Where(s => s.IsTerminal())];
}
