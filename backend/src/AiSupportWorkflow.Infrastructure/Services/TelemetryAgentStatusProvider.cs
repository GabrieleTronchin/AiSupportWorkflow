namespace AiSupportWorkflow.Infrastructure.Services;

using AiSupportWorkflow.Domain.Interfaces;

internal sealed class TelemetryAgentStatusProvider : IAgentStatusProvider
{
    public Task<IReadOnlyList<AgentStatusInfo>> GetAgentStatusesAsync(CancellationToken ct = default)
    {
        // With the new workflow engine, agent status is derived from telemetry.
        // Return empty list — the AgentsEndpoint uses configuration-driven agents instead.
        IReadOnlyList<AgentStatusInfo> result = [];
        return Task.FromResult(result);
    }
}
