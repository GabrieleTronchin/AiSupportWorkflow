namespace AiSupportWorkflow.Domain.Interfaces;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.ValueObjects;

public interface IAIAgent
{
    string AgentId { get; }
    string TeamName { get; }
    AgentRole Role { get; }
    Task<ResolutionReport> AnalyzeAndResolveAsync(IssueRecord issue, CancellationToken ct = default);
}
