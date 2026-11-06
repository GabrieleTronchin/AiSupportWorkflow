namespace AiSupportWorkflow.Infrastructure.WorkflowEngine;

using System.Collections.Concurrent;

/// <summary>
/// Tracks running workflows so they can be aborted. Aborting cancels the run's token:
/// in-flight LLM calls and the human approval gate stop, and the orchestrator records
/// a single terminal transition (Failed, "Aborted by user").
/// </summary>
public sealed class WorkflowRunRegistry
{
    public const string AbortReason = "Aborted by user";

    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runs = new();

    internal void Register(Guid issueId, CancellationTokenSource runCancellation) => _runs[issueId] = runCancellation;

    internal void Unregister(Guid issueId) => _runs.TryRemove(issueId, out _);

    public bool IsRunning(Guid issueId) => _runs.ContainsKey(issueId);

    /// <summary>Requests cancellation of a running workflow. Returns false if it is not running.</summary>
    public bool TryAbort(Guid issueId)
    {
        if (!_runs.TryGetValue(issueId, out var cts))
        {
            return false;
        }

        try
        {
            cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            // The run completed while we were aborting it.
            return false;
        }
    }
}
