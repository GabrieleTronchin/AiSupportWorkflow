namespace AiSupportWorkflow.Infrastructure.Services;

using System.Text.Json;
using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.Infrastructure.Persistence.Entities;
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
    IOptions<WorkflowConfiguration> workflowConfig,
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

    internal async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IOrchestrator>();

        if (workflowConfig.Value.SequentialProcessing)
        {
            await ProcessSequentiallyAsync(dbContext, orchestrator, ct);
        }
        else
        {
            await ProcessAllPendingAsync(dbContext, orchestrator, ct);
        }
    }

    private async Task ProcessSequentiallyAsync(WorkflowDbContext dbContext, IOrchestrator orchestrator, CancellationToken ct)
    {
        var message = await dbContext.InboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.ReceivedAt)
            .FirstOrDefaultAsync(ct);

        if (message is null) return;

        // Check if previous issue is still in-flight
        var lastProcessedIssue = await GetLastProcessedIssueAsync(dbContext, ct);
        if (lastProcessedIssue is not null && !IsTerminalStage(lastProcessedIssue.CurrentStage))
            return; // Wait for it to complete

        await ProcessMessageAsync(message, dbContext, orchestrator, ct);
    }

    private async Task ProcessAllPendingAsync(WorkflowDbContext dbContext, IOrchestrator orchestrator, CancellationToken ct)
    {
        var messages = await dbContext.InboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.ReceivedAt)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            await ProcessMessageAsync(message, dbContext, orchestrator, ct);
        }
    }

    private async Task ProcessMessageAsync(InboxMessage message, WorkflowDbContext dbContext, IOrchestrator orchestrator, CancellationToken ct)
    {
        try
        {
            var email = JsonSerializer.Deserialize<IncomingEmail>(message.Payload);
            if (email is null)
            {
                message.Error = "Failed to deserialize email payload";
                message.ProcessedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(ct);
                return;
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

    private static async Task<IssueEntity?> GetLastProcessedIssueAsync(WorkflowDbContext dbContext, CancellationToken ct)
    {
        return await dbContext.Issues
            .OrderByDescending(i => i.LastUpdated)
            .FirstOrDefaultAsync(ct);
    }

    private static bool IsTerminalStage(WorkflowStage stage) =>
        stage is WorkflowStage.Failed
            or WorkflowStage.CodeChangeGenerated
            or WorkflowStage.ClassifiedOutOfScope;
}
