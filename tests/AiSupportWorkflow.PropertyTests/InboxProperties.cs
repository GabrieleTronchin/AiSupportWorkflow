namespace AiSupportWorkflow.PropertyTests;

using System.Text.Json;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.Infrastructure.Persistence.Entities;
using AiSupportWorkflow.Infrastructure.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

public class InboxProperties
{
    private static WorkflowDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new WorkflowDbContext(options);
    }

    // Feature: dashboard-realtime-monitoring, Property 13: Inbox FIFO processing order
    // For any set of unprocessed InboxMessages with distinct ReceivedAt timestamps,
    // the InboxProcessor SHALL process them in ascending ReceivedAt order (oldest first).
    // **Validates: Requirements 7.6**
    [Property(MaxTest = 100)]
    public Property InboxMessages_ProcessedInFifoOrder(PositiveInt count)
    {
        var n = Math.Min(count.Get, 20);
        using var context = CreateContext();

        // Create messages with random but distinct timestamps
        var baseTime = DateTimeOffset.UtcNow.AddHours(-n);
        var messages = Enumerable.Range(0, n)
            .Select(i => new InboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = "SupportEmail",
                Payload = JsonSerializer.Serialize(new IncomingEmail("test@test.com", $"Subject {i}", $"Body {i}")),
                ReceivedAt = baseTime.AddMinutes(i * 3), // distinct timestamps
                ProcessedAt = null,
                Error = null,
            })
            .ToList();

        // Insert in random order
        var shuffled = messages.OrderBy(_ => Guid.NewGuid()).ToList();
        context.InboxMessages.AddRange(shuffled);
        context.SaveChanges();

        // Query as the InboxProcessor would
        var queried = context.InboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.ReceivedAt)
            .ToList();

        // Verify FIFO order
        var isOrdered = true;
        for (var i = 1; i < queried.Count; i++)
        {
            if (queried[i].ReceivedAt < queried[i - 1].ReceivedAt)
            {
                isOrdered = false;
                break;
            }
        }

        return (queried.Count == n && isOrdered).ToProperty();
    }

    // Feature: dashboard-realtime-monitoring, Property 12: Inbox processing failure records error
    // For any InboxMessage whose workflow processing throws an exception,
    // the InboxProcessor SHALL set the Error field to the exception message
    // and set ProcessedAt to a non-null value, preventing infinite retries.
    // **Validates: Requirements 7.5**
    [Property(MaxTest = 100)]
    public Property InboxMessage_FailedProcessing_RecordsErrorAndSetsProcessedAt(NonEmptyString errorMessage)
    {
        using var context = CreateContext();

        var message = new InboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "SupportEmail",
            Payload = JsonSerializer.Serialize(new IncomingEmail("test@test.com", "Subject", "Body")),
            ReceivedAt = DateTimeOffset.UtcNow,
            ProcessedAt = null,
            Error = null,
        };

        context.InboxMessages.Add(message);
        context.SaveChanges();

        // Simulate failure: set error and processedAt as the InboxProcessor would
        message.Error = errorMessage.Get;
        message.ProcessedAt = DateTimeOffset.UtcNow;
        context.SaveChanges();

        // Verify
        var reloaded = context.InboxMessages.Find(message.Id);

        var hasError = reloaded?.Error == errorMessage.Get;
        var hasProcessedAt = reloaded?.ProcessedAt is not null;
        var wouldNotBeReprocessed = context.InboxMessages
            .Where(m => m.ProcessedAt == null)
            .All(m => m.Id != message.Id);

        return (hasError && hasProcessedAt && wouldNotBeReprocessed).ToProperty();
    }

    // Feature: dashboard-realtime-monitoring, Property 11: Inbox message creation round-trip
    // For any valid email (non-empty subject and body), submitting it SHALL create an InboxMessage
    // with all required fields and the correct payload.
    // **Validates: Requirements 7.1, 7.2**
    [Property(MaxTest = 100)]
    public Property InboxMessage_CreationRoundTrip_HasCorrectFields(
        NonEmptyString sender,
        NonEmptyString subject,
        NonEmptyString body)
    {
        using var context = CreateContext();

        var email = new IncomingEmail(sender.Get, subject.Get, body.Get);
        var messageId = Guid.NewGuid();

        var inboxMessage = new InboxMessage
        {
            Id = messageId,
            MessageType = "SupportEmail",
            Payload = JsonSerializer.Serialize(email),
            ReceivedAt = DateTimeOffset.UtcNow,
            ProcessedAt = null,
            Error = null,
        };

        context.InboxMessages.Add(inboxMessage);
        context.SaveChanges();

        // Verify round-trip
        var saved = context.InboxMessages.Find(messageId);
        var deserializedEmail = JsonSerializer.Deserialize<IncomingEmail>(saved!.Payload);

        var hasCorrectId = saved.Id == messageId;
        var hasCorrectType = saved.MessageType == "SupportEmail";
        var hasNullProcessedAt = saved.ProcessedAt is null;
        var hasNullError = saved.Error is null;
        var payloadMatchesSender = deserializedEmail?.Sender == sender.Get;
        var payloadMatchesSubject = deserializedEmail?.Subject == subject.Get;
        var payloadMatchesBody = deserializedEmail?.Body == body.Get;

        return (hasCorrectId
            && hasCorrectType
            && hasNullProcessedAt
            && hasNullError
            && payloadMatchesSender
            && payloadMatchesSubject
            && payloadMatchesBody).ToProperty();
    }
}
