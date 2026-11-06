namespace AiSupportWorkflow.Infrastructure.Services;

using System.Collections.Concurrent;
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

/// <summary>
/// Polls the inbox and starts a workflow for each pending message, in FIFO order (ReceivedAt).
/// Workflows can run for a long time (e.g. waiting for human approval), so the polling loop
/// only starts them and never waits for their completion: one paused workflow cannot block the inbox.
/// Each message gets its own DI scope, kept alive until its workflow ends.
/// </summary>
public sealed class InboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<InboxProcessorOptions> options,
    IOptions<WorkflowConfiguration> workflowConfig,
    ILogger<InboxProcessor> logger) : BackgroundService
{
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds);
    private readonly SemaphoreSlim _dispatchLock = new(1, 1);
    private readonly ConcurrentDictionary<Guid, Task> _inFlight = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("InboxProcessor started with polling interval {Interval}s", options.Value.PollingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "InboxProcessor cycle failed");
            }

            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // Let in-flight workflows observe cancellation and record their outcome before shutdown.
        await Task.WhenAll(_inFlight.Values);
        logger.LogInformation("InboxProcessor stopped");
    }

    /// <summary>Runs one polling cycle and waits for the workflows it started.</summary>
    internal async Task ProcessPendingMessagesAsync(CancellationToken ct) =>
        await Task.WhenAll(await DispatchPendingMessagesAsync(ct));

    private async Task<IReadOnlyList<Task>> DispatchPendingMessagesAsync(CancellationToken ct)
    {
        await _dispatchLock.WaitAsync(ct);
        try
        {
            var toStart = await SelectMessagesToStartAsync(ct);

            var started = new List<Task>(toStart.Count);
            foreach (var messageId in toStart)
            {
                // Started one after the other, so workflows are invoked in FIFO order;
                // they then run concurrently.
                var task = StartMessageAsync(messageId, ct);
                _inFlight[messageId] = task;
                started.Add(task);
                _ = task.ContinueWith(_ => _inFlight.TryRemove(messageId, out Task? _), TaskScheduler.Default);
            }

            return started;
        }
        finally
        {
            _dispatchLock.Release();
        }
    }

    private async Task<List<Guid>> SelectMessagesToStartAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();

        var pending = await dbContext.InboxMessages
            .AsNoTracking()
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.ReceivedAt)
            .Select(m => m.Id)
            .ToListAsync(ct);

        var toStart = pending.Where(id => !_inFlight.ContainsKey(id)).ToList();

        if (!workflowConfig.Value.SequentialProcessing || toStart.Count == 0)
        {
            return toStart;
        }

        // Sequential mode: one workflow at a time, wait until the previous one has finished.
        if (!_inFlight.IsEmpty)
        {
            return [];
        }

        var lastIssue = await dbContext.Issues
            .AsNoTracking()
            .OrderByDescending(i => i.LastUpdated)
            .FirstOrDefaultAsync(ct);

        return lastIssue is not null && !lastIssue.CurrentStage.IsTerminal() ? [] : [toStart[0]];
    }

    /// <summary>
    /// Starts the workflow for a message and returns a task that completes when the workflow ends.
    /// The orchestrator is invoked before the first await so invocation order matches dispatch order.
    /// </summary>
    private Task StartMessageAsync(Guid messageId, CancellationToken ct)
    {
        var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var message = dbContext.InboxMessages.FirstOrDefault(m => m.Id == messageId);

        if (message is null || message.ProcessedAt is not null)
        {
            scope.Dispose();
            return Task.CompletedTask;
        }

        Task workflow;
        try
        {
            var email = JsonSerializer.Deserialize<IncomingEmail>(message.Payload);
            workflow = email is null
                ? Task.FromException(new InvalidOperationException("Failed to deserialize email payload"))
                : scope.ServiceProvider.GetRequiredService<IOrchestrator>().ProcessIssueAsync(email, ct);
        }
        catch (Exception ex)
        {
            workflow = Task.FromException(ex);
        }

        return CompleteMessageAsync(scope, dbContext, message, workflow);
    }

    private async Task CompleteMessageAsync(IServiceScope scope, WorkflowDbContext dbContext, InboxMessage message, Task workflow)
    {
        using (scope)
        {
            try
            {
                await workflow;
            }
            catch (Exception ex)
            {
                message.Error = ex.Message;
                logger.LogWarning(ex, "Failed to process inbox message {Id}", message.Id);
            }

            // Always mark as processed (no infinite retries), even during shutdown.
            message.ProcessedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }
}
