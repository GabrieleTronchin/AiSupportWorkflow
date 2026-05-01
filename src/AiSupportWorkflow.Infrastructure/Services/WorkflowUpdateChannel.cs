namespace AiSupportWorkflow.Infrastructure.Services;

using System.Threading.Channels;
using AiSupportWorkflow.Domain.Entities;

/// <summary>
/// In-memory channel for broadcasting workflow state updates to subscribers (e.g., gRPC stream).
/// </summary>
public sealed class WorkflowUpdateChannel
{
    private readonly Channel<WorkflowState> _channel = Channel.CreateUnbounded<WorkflowState>(
        new UnboundedChannelOptions { SingleWriter = false, SingleReader = false });

    public ChannelWriter<WorkflowState> Writer => _channel.Writer;
    public ChannelReader<WorkflowState> Reader => _channel.Reader;
}
