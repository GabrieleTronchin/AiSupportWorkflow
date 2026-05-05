namespace AiSupportWorkflow.Domain.Interfaces;

public interface IAgentStatusProvider
{
    Task<IReadOnlyList<AgentStatusInfo>> GetAgentStatusesAsync(CancellationToken ct = default);
}

public record AgentStatusInfo(string AgentId, string Status, string? LastAction);
