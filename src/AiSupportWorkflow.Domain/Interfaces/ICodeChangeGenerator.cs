namespace AiSupportWorkflow.Domain.Interfaces;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.ValueObjects;

public interface ICodeChangeGenerator
{
    Task<PullRequest> GenerateAsync(ResolutionReport resolution, CancellationToken ct = default);
}
