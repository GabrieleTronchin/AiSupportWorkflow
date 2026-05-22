namespace AiSupportWorkflow.Infrastructure.WorkflowEngine.Executors;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

public sealed partial class CodeGenerationExecutor(
    IChatClient chatClient,
    IWorkflowStateTracker stateTracker) : Executor("CodeGenerationExecutor")
{
    private static readonly ChatOptions ChatOpts = new() { Temperature = 0.5f };

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
    {
        protocolBuilder.RouteBuilder.AddHandler<ApprovalDecision>((Func<ApprovalDecision, IWorkflowContext, CancellationToken, ValueTask>)HandleAsync);
        return protocolBuilder;
    }

    public async ValueTask HandleAsync(
        ApprovalDecision approval, IWorkflowContext context, CancellationToken ct)
    {
        var issueId = await context.ReadStateAsync<Guid>("CurrentIssueId", scopeName: "Workflow", ct);
        var report = await context.ReadStateAsync<ResolutionReport>(
            "LatestResolution", scopeName: "Workflow", ct)
            ?? throw new InvalidOperationException("Resolution not found");

        var response = await chatClient.GetResponseAsync<PullRequest>(
            [new(ChatRole.User, BuildCodeGenPrompt(report))], ChatOpts, cancellationToken: ct);
        var pullRequest = response.Result;

        await stateTracker.TransitionAsync(issueId, WorkflowStage.CodeChangeGenerated, pullRequest.Title);
        await context.YieldOutputAsync(WorkflowResult.Completed(issueId, pullRequest), ct);
    }

    private static string BuildCodeGenPrompt(ResolutionReport report) =>
        $"""
        Issue ID: {report.IssueId}
        Root Cause: {report.RootCauseDescription}
        Affected Component: {report.AffectedComponent}
        Severity: {report.SeverityAssessment}
        Proposed Fix: {report.ProposedFixSummary}
        """;
}
