namespace AiSupportWorkflow.Application.UseCases;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;

public class ProcessSupportEmailUseCase(IOrchestrator orchestrator)
{
    public Task<WorkflowResult> ExecuteAsync(IncomingEmail email, CancellationToken ct = default) =>
        orchestrator.ProcessIssueAsync(email, ct);
}
