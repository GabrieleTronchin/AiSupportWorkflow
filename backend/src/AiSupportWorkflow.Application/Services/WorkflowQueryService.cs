namespace AiSupportWorkflow.Application.Services;

using AiSupportWorkflow.Domain.Interfaces;

public sealed class WorkflowQueryService(IWorkflowEventRepository eventRepository)
{
    private const int MaxEventLimit = 200;

    public Task<IReadOnlyList<WorkflowEventDto>> GetEventsAsync(int? limit, CancellationToken ct = default)
    {
        var maxLimit = Math.Min(limit ?? MaxEventLimit, MaxEventLimit);
        return eventRepository.GetEventsAsync(maxLimit, ct);
    }
}
