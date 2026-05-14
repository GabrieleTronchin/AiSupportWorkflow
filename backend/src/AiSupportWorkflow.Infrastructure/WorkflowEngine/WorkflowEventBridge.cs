namespace AiSupportWorkflow.Infrastructure.WorkflowEngine;

using AiSupportWorkflow.Infrastructure.Services;
using Microsoft.Agents.AI.Workflows;

internal sealed class WorkflowEventBridge(WorkflowUpdateChannel updateChannel)
{
    private readonly WorkflowUpdateChannel _updateChannel = updateChannel;

    public async Task BridgeEventsAsync(StreamingRun run, CancellationToken ct)
    {
        await foreach (var evt in run.WatchStreamAsync(ct))
        {
            // The StateTracker handles gRPC streaming via WorkflowUpdateChannel
            // This bridge is for additional workflow-level event handling

            if (evt is WorkflowOutputEvent output)
            {
                // Terminal event — no additional action needed since StateTracker handles it.
                // The _updateChannel is available for future workflow-level event bridging.
            }
        }
    }
}
