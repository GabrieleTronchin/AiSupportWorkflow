namespace AiSupportWorkflow.Infrastructure.Services;

using System.Text.Json;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class InboxProcessorOptions
{
    public int PollingIntervalSeconds { get; set; } = 5;
}

public sealed class InboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<InboxProcessorOptions> options,
    ILogger<InboxProcessor> logger) : BackgroundService
{
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("InboxProcessor started with polling interval {Interval}s", options.Value.PollingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "InboxProcessor cycle failed");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        logger.LogInformation("InboxProcessor stopped");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IOrchestrator>();

        var messages = await dbContext.InboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.ReceivedAt)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                var email = JsonSerializer.Deserialize<IncomingEmail>(message.Payload);
                if (email is null)
                {
                    message.Error = "Failed to deserialize email payload";
                    message.ProcessedAt = DateTimeOffset.UtcNow;
                    await dbContext.SaveChangesAsync(ct);
                    continue;
                }

                await orchestrator.ProcessIssueAsync(email, ct);
                message.ProcessedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                message.Error = ex.Message;
                message.ProcessedAt = DateTimeOffset.UtcNow;
                logger.LogWarning(ex, "Failed to process inbox message {Id}", message.Id);
            }

            await dbContext.SaveChangesAsync(ct);
        }
    }
}
