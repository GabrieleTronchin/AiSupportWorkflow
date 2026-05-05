namespace AiSupportWorkflow.Infrastructure.Persistence;

using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class EfInboxRepository(WorkflowDbContext dbContext) : IInboxRepository, IInboxQueryService
{
    public async Task<Guid> SaveMessageAsync(string messageType, string payload, CancellationToken ct = default)
    {
        var message = new InboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = messageType,
            Payload = payload,
            ReceivedAt = DateTimeOffset.UtcNow,
        };

        dbContext.InboxMessages.Add(message);
        await dbContext.SaveChangesAsync(ct);

        return message.Id;
    }

    public async Task<IReadOnlyList<InboxMessageDto>> GetMessagesAsync(string? statusFilter, CancellationToken ct = default)
    {
        var query = dbContext.InboxMessages.AsNoTracking().AsQueryable();

        query = statusFilter?.ToLowerInvariant() switch
        {
            "queued" => query.Where(m => m.ProcessedAt == null),
            "processed" => query.Where(m => m.ProcessedAt != null && m.Error == null),
            "failed" => query.Where(m => m.Error != null),
            _ => query,
        };

        var messages = await query
            .OrderByDescending(m => m.ReceivedAt)
            .Select(m => new InboxMessageDto(
                m.Id,
                m.MessageType,
                m.ReceivedAt,
                m.ProcessedAt,
                m.Error,
                m.ProcessedAt == null ? "queued"
                    : m.Error != null ? "failed"
                    : "processed"))
            .ToListAsync(ct);

        return messages;
    }
}
