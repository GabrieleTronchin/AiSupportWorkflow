namespace AiSupportWorkflow.UnitTests.Persistence;

using System.Text.Json;
using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.Infrastructure.Persistence.Entities;
using AiSupportWorkflow.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

public class InboxProcessorTests
{
    private static (WorkflowDbContext context, IOrchestrator orchestrator, InboxProcessor processor) CreateProcessor(int pollingSeconds = 1, bool sequentialProcessing = false)
    {
        var dbName = Guid.NewGuid().ToString();
        var dbOptions = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var context = new WorkflowDbContext(dbOptions);
        var orchestrator = Substitute.For<IOrchestrator>();

        orchestrator.ProcessIssueAsync(Arg.Any<IncomingEmail>(), Arg.Any<CancellationToken>())
            .Returns(WorkflowResult.OutOfScope(Guid.NewGuid()));

        var services = new ServiceCollection();
        services.AddDbContext<WorkflowDbContext>(opts => opts.UseInMemoryDatabase(dbName));
        services.AddScoped<IOrchestrator>(_ => orchestrator);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var options = Options.Create(new InboxProcessorOptions { PollingIntervalSeconds = pollingSeconds });
        var workflowConfig = Options.Create(new WorkflowConfiguration { SequentialProcessing = sequentialProcessing });
        var logger = NullLogger<InboxProcessor>.Instance;

        var processor = new InboxProcessor(scopeFactory, options, workflowConfig, logger);
        return (context, orchestrator, processor);
    }

    [Fact]
    public async Task ProcessedMessage_SetsProcessedAt_AndErrorNull()
    {
        // Arrange
        var (context, orchestrator, processor) = CreateProcessor();
        var email = new IncomingEmail("test@test.com", "Subject", "Body");

        var msgId = Guid.NewGuid();
        context.InboxMessages.Add(new InboxMessage
        {
            Id = msgId,
            Payload = JsonSerializer.Serialize(email),
            ReceivedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        // Act — use StartAsync/StopAsync with a short cancellation
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await processor.StartAsync(cts.Token); await Task.Delay(1500, cts.Token); }
        catch (OperationCanceledException) { }
        await processor.StopAsync(CancellationToken.None);

        // Assert — reload from DB
        var message = await context.InboxMessages.AsNoTracking().FirstAsync(m => m.Id == msgId, TestContext.Current.CancellationToken);
        Assert.NotNull(message.ProcessedAt);
        Assert.Null(message.Error);
    }

    [Fact]
    public async Task FailedMessage_SetsError_AndProcessedAt()
    {
        // Arrange
        var (context, orchestrator, processor) = CreateProcessor();
        var email = new IncomingEmail("test@test.com", "Subject", "Body");

        orchestrator.ProcessIssueAsync(Arg.Any<IncomingEmail>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Processing failed"));

        var msgId = Guid.NewGuid();
        context.InboxMessages.Add(new InboxMessage
        {
            Id = msgId,
            Payload = JsonSerializer.Serialize(email),
            ReceivedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await processor.StartAsync(cts.Token); await Task.Delay(1500, cts.Token); }
        catch (OperationCanceledException) { }
        await processor.StopAsync(CancellationToken.None);

        // Assert — reload from DB
        var message = await context.InboxMessages.AsNoTracking().FirstAsync(m => m.Id == msgId, TestContext.Current.CancellationToken);
        Assert.NotNull(message.ProcessedAt);
        Assert.Equal("Processing failed", message.Error);
    }

    [Fact]
    public async Task Messages_ProcessedInFifoOrder()
    {
        // Arrange
        var (context, orchestrator, processor) = CreateProcessor();
        var processedOrder = new List<string>();

        orchestrator.ProcessIssueAsync(Arg.Any<IncomingEmail>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var email = callInfo.Arg<IncomingEmail>();
                processedOrder.Add(email.Subject);
                return WorkflowResult.OutOfScope(Guid.NewGuid());
            });

        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        for (var i = 0; i < 3; i++)
        {
            context.InboxMessages.Add(new InboxMessage
            {
                Id = Guid.NewGuid(),
                Payload = JsonSerializer.Serialize(new IncomingEmail("test@test.com", $"Email-{i}", "Body")),
                ReceivedAt = baseTime.AddMinutes(i),
            });
        }
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await processor.StartAsync(cts.Token); await Task.Delay(1500, cts.Token); }
        catch (OperationCanceledException) { }
        await processor.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(["Email-0", "Email-1", "Email-2"], processedOrder);
    }

    [Fact]
    public void PollingInterval_IsConfigurable()
    {
        // Arrange & Act
        var options = new InboxProcessorOptions { PollingIntervalSeconds = 30 };

        // Assert
        Assert.Equal(30, options.PollingIntervalSeconds);
    }

    [Fact]
    public async Task SequentialMode_WithNonTerminalPreviousIssue_DoesNotProcessNewMessage()
    {
        // Arrange
        var (context, orchestrator, processor) = CreateProcessor(sequentialProcessing: true);
        var email = new IncomingEmail("test@test.com", "New Email", "Body");

        // Add a non-terminal issue (Resolving stage)
        context.Issues.Add(new IssueEntity
        {
            Id = Guid.NewGuid(),
            CurrentStage = WorkflowStage.Resolving,
            LastUpdated = DateTimeOffset.UtcNow,
        });

        // Add an unprocessed inbox message
        context.InboxMessages.Add(new InboxMessage
        {
            Id = Guid.NewGuid(),
            Payload = JsonSerializer.Serialize(email),
            ReceivedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await processor.StartAsync(cts.Token); await Task.Delay(1500, cts.Token); }
        catch (OperationCanceledException) { }
        await processor.StopAsync(CancellationToken.None);

        // Assert — orchestrator should NOT have been called
        await orchestrator.DidNotReceive().ProcessIssueAsync(Arg.Any<IncomingEmail>(), Arg.Any<CancellationToken>());

        // Message should still be unprocessed
        var message = await context.InboxMessages.AsNoTracking().FirstAsync(TestContext.Current.CancellationToken);
        Assert.Null(message.ProcessedAt);
    }

    [Fact]
    public async Task SequentialMode_WithNoInFlightIssue_ProcessesExactlyOneMessage()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var dbOptions = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var context = new WorkflowDbContext(dbOptions);
        var orchestrator = Substitute.For<IOrchestrator>();
        var processedSubjects = new List<string>();

        // When the orchestrator processes a message, simulate creating a non-terminal issue
        // This blocks subsequent messages in sequential mode
        orchestrator.ProcessIssueAsync(Arg.Any<IncomingEmail>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var email = callInfo.Arg<IncomingEmail>();
                processedSubjects.Add(email.Subject);

                // Simulate the orchestrator creating a non-terminal issue in the DB
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

        var options = Options.Create(new InboxProcessorOptions { PollingIntervalSeconds = 1 });
        var workflowConfig = Options.Create(new WorkflowConfiguration { SequentialProcessing = true });
        var logger = NullLogger<InboxProcessor>.Instance;

        var processor = new InboxProcessor(scopeFactory, options, workflowConfig, logger);

        // Add multiple unprocessed messages
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        for (var i = 0; i < 3; i++)
        {
            context.InboxMessages.Add(new InboxMessage
            {
                Id = Guid.NewGuid(),
                Payload = JsonSerializer.Serialize(new IncomingEmail("test@test.com", $"Email-{i}", "Body")),
                ReceivedAt = baseTime.AddMinutes(i),
            });
        }
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await processor.StartAsync(cts.Token); await Task.Delay(1500, cts.Token); }
        catch (OperationCanceledException) { }
        await processor.StopAsync(CancellationToken.None);

        // Assert — exactly one message should have been processed
        Assert.Single(processedSubjects);
        Assert.Equal("Email-0", processedSubjects[0]);
    }

    [Fact]
    public async Task ParallelMode_LongRunningWorkflow_DoesNotBlockOtherMessages()
    {
        // Arrange — the first workflow never completes on its own (e.g. waiting for human approval)
        var (context, orchestrator, processor) = CreateProcessor(sequentialProcessing: false);
        var blocked = new TaskCompletionSource<WorkflowResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processedSubjects = new System.Collections.Concurrent.ConcurrentQueue<string>();

        orchestrator.ProcessIssueAsync(Arg.Any<IncomingEmail>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var email = callInfo.Arg<IncomingEmail>();
                processedSubjects.Enqueue(email.Subject);
                return email.Subject == "Awaiting approval"
                    ? blocked.Task
                    : Task.FromResult(WorkflowResult.OutOfScope(Guid.NewGuid()));
            });

        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        string[] subjects = ["Awaiting approval", "Second", "Third"];
        for (var i = 0; i < subjects.Length; i++)
        {
            context.InboxMessages.Add(new InboxMessage
            {
                Id = Guid.NewGuid(),
                Payload = JsonSerializer.Serialize(new IncomingEmail("test@test.com", subjects[i], "Body")),
                ReceivedAt = baseTime.AddMinutes(i),
            });
        }
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        // Act
        await processor.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await Task.Delay(1500, TestContext.Current.CancellationToken);

            // Assert — the other messages were processed while the first one is still running
            var messages = await context.InboxMessages.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
            var state = string.Join("; ", messages.Select(m => $"{m.ProcessedAt is not null}:{m.Error}"));
            Assert.True(subjects.Order().SequenceEqual(processedSubjects.Order()), $"Processed: [{string.Join(",", processedSubjects)}] Inbox: {state}");
            Assert.Equal(2, messages.Count(m => m.ProcessedAt != null));

            // The running workflow is not started twice by later polling cycles
            await Task.Delay(1200, TestContext.Current.CancellationToken);
            Assert.Equal(1, processedSubjects.Count(s => s == "Awaiting approval"));
        }
        finally
        {
            blocked.SetResult(WorkflowResult.OutOfScope(Guid.NewGuid()));
            await processor.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ParallelMode_ProcessesAllPendingMessages()
    {
        // Arrange
        var (context, orchestrator, processor) = CreateProcessor(sequentialProcessing: false);
        var processedSubjects = new List<string>();

        orchestrator.ProcessIssueAsync(Arg.Any<IncomingEmail>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var email = callInfo.Arg<IncomingEmail>();
                processedSubjects.Add(email.Subject);
                return WorkflowResult.OutOfScope(Guid.NewGuid());
            });

        // Add multiple unprocessed messages
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        for (var i = 0; i < 3; i++)
        {
            context.InboxMessages.Add(new InboxMessage
            {
                Id = Guid.NewGuid(),
                Payload = JsonSerializer.Serialize(new IncomingEmail("test@test.com", $"Email-{i}", "Body")),
                ReceivedAt = baseTime.AddMinutes(i),
            });
        }
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await processor.StartAsync(cts.Token); await Task.Delay(1500, cts.Token); }
        catch (OperationCanceledException) { }
        await processor.StopAsync(CancellationToken.None);

        // Assert — all messages should have been processed
        Assert.Equal(3, processedSubjects.Count);
        Assert.Equal(["Email-0", "Email-1", "Email-2"], processedSubjects);
    }
}
