namespace AiSupportWorkflow.Domain.Interfaces;

public interface IInboxRepository
{
    Task<Guid> SaveMessageAsync(string messageType, string payload, CancellationToken ct = default);

    Task<IReadOnlyList<InboxMessageDto>> GetMessagesAsync(string? statusFilter, CancellationToken ct = default);
}

public record InboxMessageDto(
    Guid Id,
    string MessageType,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt,
    string? Error,
    string Status);
