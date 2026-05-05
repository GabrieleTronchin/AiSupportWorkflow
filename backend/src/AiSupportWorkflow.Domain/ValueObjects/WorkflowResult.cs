namespace AiSupportWorkflow.Domain.ValueObjects;

using AiSupportWorkflow.Domain.Entities;

public record WorkflowResult(
    Guid IssueId,
    bool IsSuccess,
    PullRequest? PullRequest,
    bool IsOutOfScope,
    string? FailureReason)
{
    public static WorkflowResult Completed(Guid issueId, PullRequest pullRequest) =>
        new(issueId, IsSuccess: true, pullRequest, IsOutOfScope: false, FailureReason: null);

    public static WorkflowResult OutOfScope(Guid issueId) =>
        new(issueId, IsSuccess: true, PullRequest: null, IsOutOfScope: true, FailureReason: null);

    public static WorkflowResult Failed(Guid issueId, string reason) =>
        new(issueId, IsSuccess: false, PullRequest: null, IsOutOfScope: false, FailureReason: reason);
}
