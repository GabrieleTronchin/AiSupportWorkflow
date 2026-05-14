namespace AiSupportWorkflow.Infrastructure.WorkflowEngine.Executors;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

internal sealed partial class ClassificationExecutor(
    IChatClient chatClient,
    IWorkflowStateTracker stateTracker) : Executor("ClassificationExecutor")
{
    private static readonly ChatOptions ChatOpts = new() { Temperature = 0.1f };

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) =>
        protocolBuilder.AddClassAttributeTypes(GetType()).YieldsOutput<WorkflowResult>();

    [MessageHandler]
    private async ValueTask<ClassificationResult> HandleAsync(
        IssueRecord issue, IWorkflowContext context, CancellationToken ct)
    {
        await stateTracker.TransitionAsync(issue.Id, WorkflowStage.Received, subject: issue.Subject);

        var response = await chatClient.GetResponseAsync<ClassificationResult>(
            [new(ChatRole.User, $"Subject: {issue.Subject}\n\nBody: {issue.Body}")], ChatOpts, cancellationToken: ct);
        var result = response.Result;

        if (!result.IsCodeRelated)
        {
            await stateTracker.TransitionAsync(issue.Id, WorkflowStage.ClassifiedOutOfScope, result.Reasoning);
            await context.YieldOutputAsync(WorkflowResult.OutOfScope(issue.Id), ct);
            return result; // Edge condition prevents further traversal
        }

        await stateTracker.TransitionAsync(issue.Id, WorkflowStage.Classified,
            $"{result.Category} ({result.ConfidenceScore:P0})");

        // Store issue in workflow state context for downstream executors
        await context.QueueStateUpdateAsync(issue.Id.ToString(), issue, scopeName: "Issues", ct);

        return result;
    }
}
