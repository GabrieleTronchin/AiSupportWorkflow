namespace AiSupportWorkflow.Application.Services;

using System.Text.Json;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Interfaces;

public sealed class InboxService(IInboxRepository inboxRepository, IInboxQueryService inboxQueryService)
{
    public Task<Guid> SubmitEmailAsync(IncomingEmail email, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(email);
        return inboxRepository.SaveMessageAsync("SupportEmail", payload, ct);
    }

    public Task<IReadOnlyList<InboxMessageDto>> GetMessagesAsync(string? statusFilter, CancellationToken ct = default) =>
        inboxQueryService.GetMessagesAsync(statusFilter, ct);
}

public record InboxMessageDto(
    Guid Id,
    string MessageType,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt,
    string? Error,
    string Status,
    string? Payload);

public interface IInboxQueryService
{
    Task<IReadOnlyList<InboxMessageDto>> GetMessagesAsync(string? statusFilter, CancellationToken ct = default);
}
