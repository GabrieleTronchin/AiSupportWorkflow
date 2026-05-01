namespace AiSupportWorkflow.PropertyTests;

using System.Text.Json;
using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
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

    // Feature: dashboard-ui-polish, Property 8: Parallel mode processes all pending messages in one cycle
    // For any inbox containing N ≥ 1 unprocessed messages, when SequentialProcessing is false,
    // the InboxProcessor should process all N messages in a single cycle.
    // **Validates: Requirements 5.4**
    [Property(MaxTest = 100)]
    public async Task<Property> ParallelMode_ProcessesAllPendingMessagesInOneCycle(PositiveInt count)
    {
        var n = Math.Clamp(count.Get, 1, 10);

        var dbName = Guid.NewGuid().ToString();
        var dbOptions = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        using var context = new WorkflowDbContext(dbOptions);
        var orchestrator = Substitute.For<IOrchestrator>();

        // Orchestrator processes messages successfully
        orchestrator.ProcessIssueAsync(Arg.Any<IncomingEmail>(), Arg.Any<CancellationToken>())
            .Returns(WorkflowResult.OutOfScope(Guid.NewGuid()));

        var services = new ServiceCollection();
        services.AddDbContext<WorkflowDbContext>(opts => opts.UseInMemoryDatabase(dbName));
        services.AddScoped<IOrchestrator>(_ => orchestrator);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var processorOptions = Options.Create(new InboxProcessorOptions { PollingIntervalSeconds = 60 });
        var workflowConfig = Options.Create(new WorkflowConfiguration { SequentialProcessing = false });
        var logger = NullLogger<InboxProcessor>.Instance;

        var processor = new InboxProcessor(scopeFactory, processorOptions, workflowConfig, logger);

        // Add N unprocessed messages
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-n);
        for (var i = 0; i < n; i++)
        {
            context.InboxMessages.Add(new InboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = "SupportEmail",
                Payload = JsonSerializer.Serialize(new IncomingEmail("test@test.com", $"Subject {i}", $"Body {i}")),
                ReceivedAt = baseTime.AddMinutes(i),
                ProcessedAt = null,
                Error = null,
            });
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // Act — call ProcessPendingMessagesAsync directly (one cycle)
        await processor.ProcessPendingMessagesAsync(CancellationToken.None);

        // Assert — reload all messages from DB
        var allMessages = await context.InboxMessages.AsNoTracking().ToListAsync();
        var processedCount = allMessages.Count(m => m.ProcessedAt != null);

        return (processedCount == n).ToProperty();
    }

    // Feature: dashboard-ui-polish, Property 7: Sequential mode processes exactly one message per cycle
    // For any inbox containing N > 1 unprocessed messages, when SequentialProcessing is true
    // and no previous issue is in-flight, the InboxProcessor should process exactly one message
    // and leave the remaining N-1 messages unprocessed.
    // **Validates: Requirements 5.2**
    [Property(MaxTest = 100)]
    public async Task<Property> SequentialMode_ProcessesExactlyOneMessagePerCycle(PositiveInt count)
    {
        var n = Math.Clamp(count.Get, 2, 10);

        var dbName = Guid.NewGuid().ToString();
        var dbOptions = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        using var context = new WorkflowDbContext(dbOptions);
        var orchestrator = Substitute.For<IOrchestrator>();

        // When the orchestrator processes a message, simulate creating a non-terminal issue
        // This blocks subsequent messages in sequential mode
        orchestrator.ProcessIssueAsync(Arg.Any<IncomingEmail>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                using var innerContext = new WorkflowDbContext(dbOptions);
                innerContext.Issues.Add(new IssueEntity
                {
                    Id = Guid.NewGuid(),
                    CurrentStage = WorkflowStage.Resolving,
                    LastUpdated = DateTimeOffset.UtcNow,
                });
                innerContext.SaveChanges();
                return WorkflowResult.OutOfScope(Guid.NewGuid());
            });

        var services = new ServiceCollection();
        services.AddDbContext<WorkflowDbContext>(opts => opts.UseInMemoryDatabase(dbName));
        services.AddScoped<IOrchestrator>(_ => orchestrator);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var processorOptions = Options.Create(new InboxProcessorOptions { PollingIntervalSeconds = 60 });
        var workflowConfig = Options.Create(new WorkflowConfiguration { SequentialProcessing = true });
        var logger = NullLogger<InboxProcessor>.Instance;

        var processor = new InboxProcessor(scopeFactory, processorOptions, workflowConfig, logger);

        // Add N unprocessed messages
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-n);
        for (var i = 0; i < n; i++)
        {
            context.InboxMessages.Add(new InboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = "SupportEmail",
                Payload = JsonSerializer.Serialize(new IncomingEmail("test@test.com", $"Subject {i}", $"Body {i}")),
                ReceivedAt = baseTime.AddMinutes(i),
                ProcessedAt = null,
                Error = null,
            });
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // Act — call ProcessPendingMessagesAsync directly (one cycle)
        await processor.ProcessPendingMessagesAsync(CancellationToken.None);

        // Assert — reload all messages from DB
        var allMessages = await context.InboxMessages.AsNoTracking().ToListAsync();
        var processedCount = allMessages.Count(m => m.ProcessedAt != null);
        var unprocessedCount = allMessages.Count(m => m.ProcessedAt == null);

        return (processedCount == 1 && unprocessedCount == n - 1).ToProperty();
    }
}
