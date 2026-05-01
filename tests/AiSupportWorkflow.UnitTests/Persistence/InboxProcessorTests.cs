namespace AiSupportWorkflow.UnitTests.Persistence;

using System.Text.Json;
using AiSupportWorkflow.Domain.Entities;
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
    private static (WorkflowDbContext context, IOrchestrator orchestrator, InboxProcessor processor) CreateProcessor(int pollingSeconds = 1)
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
        var logger = NullLogger<InboxProcessor>.Instance;

        var processor = new InboxProcessor(scopeFactory, options, logger);
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
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // Act — use StartAsync/StopAsync with a short cancellation
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await processor.StartAsync(cts.Token); await Task.Delay(1500, cts.Token); }
        catch (OperationCanceledException) { }
        await processor.StopAsync(CancellationToken.None);

        // Assert — reload from DB
        var message = await context.InboxMessages.AsNoTracking().FirstAsync(m => m.Id == msgId);
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
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await processor.StartAsync(cts.Token); await Task.Delay(1500, cts.Token); }
        catch (OperationCanceledException) { }
        await processor.StopAsync(CancellationToken.None);

        // Assert — reload from DB
        var message = await context.InboxMessages.AsNoTracking().FirstAsync(m => m.Id == msgId);
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
        await context.SaveChangesAsync();

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
}
