namespace AiSupportWorkflow.Domain.Interfaces;

using AiSupportWorkflow.Domain.Messages;

public interface IAgentStatusProvider
{
    Task<AggregatedAgentStatusResponse> GetAgentStatusesAsync(CancellationToken ct = default);
}
