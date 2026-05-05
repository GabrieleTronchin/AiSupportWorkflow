namespace AiSupportWorkflow.Presentation.Services;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Infrastructure.Services;
using AiSupportWorkflow.Presentation.Grpc;
using global::Grpc.Core;
using Microsoft.Extensions.Options;

public sealed class WorkflowMonitorService(
    WorkflowUpdateChannel updateChannel,
    IOptions<WorkflowConfiguration> config,
    ILogger<WorkflowMonitorService> logger) : WorkflowMonitor.WorkflowMonitorBase
{
    public override async Task SubscribeToUpdates(
        SubscribeRequest request,
        IServerStreamWriter<WorkflowStateUpdate> responseStream,
        ServerCallContext context)
    {
        if (!config.Value.EnableVisualization)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "Visualization is disabled."));
        }

        var issueFilter = request.HasIssueId ? Guid.Parse(request.IssueId) : (Guid?)null;
        var ct = context.CancellationToken;

        logger.LogInformation("gRPC client subscribed to workflow updates. Filter: {Filter}",
            issueFilter?.ToString() ?? "all");

        try
        {
            await foreach (var state in updateChannel.Reader.ReadAllAsync(ct))
            {
                if (issueFilter.HasValue && state.IssueId != issueFilter.Value)
                    continue;

                var update = new WorkflowStateUpdate
                {
                    IssueId = state.IssueId.ToString(),
                    Stage = state.Stage.ToString(),
                    LastUpdated = state.LastUpdated.ToString("O"),
                    Detail = state.Detail ?? "",
                };

                await responseStream.WriteAsync(update, ct);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("gRPC client disconnected");
        }
    }
}
