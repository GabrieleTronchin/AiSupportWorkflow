namespace AiSupportWorkflow.Domain.Interfaces;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.ValueObjects;

public interface IIssueClassifier
{
    Task<ClassificationResult> ClassifyAsync(IssueRecord issue, CancellationToken ct = default);
}
