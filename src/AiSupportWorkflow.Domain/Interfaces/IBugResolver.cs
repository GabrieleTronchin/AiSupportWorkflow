namespace AiSupportWorkflow.Domain.Interfaces;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.ValueObjects;

public interface IBugResolver
{
    Task<ResolutionReport> ResolveAsync(IssueRecord issue, AgentAssignment agent, CancellationToken ct = default);
}
