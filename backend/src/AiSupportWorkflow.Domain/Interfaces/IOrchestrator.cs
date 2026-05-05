namespace AiSupportWorkflow.Domain.Interfaces;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.ValueObjects;

public interface IOrchestrator
{
    Task<WorkflowResult> ProcessIssueAsync(IncomingEmail email, CancellationToken ct = default);
}
