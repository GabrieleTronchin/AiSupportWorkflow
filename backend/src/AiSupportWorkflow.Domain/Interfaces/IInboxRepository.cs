namespace AiSupportWorkflow.Domain.Interfaces;

public interface IInboxRepository
{
    Task<Guid> SaveMessageAsync(string messageType, string payload, CancellationToken ct = default);
}
