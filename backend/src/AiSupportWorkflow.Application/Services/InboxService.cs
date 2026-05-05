namespace AiSupportWorkflow.Application.Services;

using System.Text.Json;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Interfaces;

public sealed class InboxService(IInboxRepository inboxRepository)
{
    public Task<Guid> SubmitEmailAsync(IncomingEmail email, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(email);
        return inboxRepository.SaveMessageAsync("SupportEmail", payload, ct);
    }

    public Task<IReadOnlyList<InboxMessageDto>> GetMessagesAsync(string? statusFilter, CancellationToken ct = default) =>
        inboxRepository.GetMessagesAsync(statusFilter, ct);
}
