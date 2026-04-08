namespace AiSupportWorkflow.Domain.Entities;

public record PullRequest(Guid Id, Guid IssueId, string Title, string Description, IReadOnlyList<string> AffectedFilePaths, string SimulatedDiff);
