namespace AiSupportWorkflow.Infrastructure.Persistence;

using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public sealed class EfWorkflowEventRepository(WorkflowDbContext dbContext) : IWorkflowEventRepository
{
    private static readonly WorkflowStage[] TerminalStages =
    [
        WorkflowStage.Failed,
        WorkflowStage.CodeChangeGenerated,
        WorkflowStage.ClassifiedOutOfScope,
    ];

    public async Task<IReadOnlyList<WorkflowEventDto>> GetEventsAsync(int limit, CancellationToken ct = default)
    {
        var events = await dbContext.Events
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .Select(e => new WorkflowEventDto(
                e.Id,
                e.IssueId,
                e.PreviousStage != null ? e.PreviousStage.ToString() : null,
                e.NewStage.ToString(),
                e.Timestamp,
                e.Detail))
            .ToListAsync(ct);

        return events;
    }

    public async Task<IReadOnlyList<AgentAssignmentInfo>> GetAgentAssignmentsForNonTerminalIssuesAsync(CancellationToken ct = default)
    {
        var nonTerminalIssues = await dbContext.Issues
            .AsNoTracking()
            .Where(i => !TerminalStages.Contains(i.CurrentStage))
            .ToListAsync(ct);

        var nonTerminalIssueIds = nonTerminalIssues.Select(i => i.Id).ToList();

        var agentAssignments = await dbContext.Events
            .AsNoTracking()
            .Where(e => nonTerminalIssueIds.Contains(e.IssueId)
                && e.NewStage == WorkflowStage.AgentAssigned
                && e.Detail != null)
            .ToListAsync(ct);

        var issueLookup = nonTerminalIssues.ToDictionary(i => i.Id);

        return agentAssignments
            .Select(e =>
            {
                var issue = issueLookup.GetValueOrDefault(e.IssueId);
                return new AgentAssignmentInfo(
                    AgentId: e.Detail!,
                    IssueId: e.IssueId,
                    CurrentStage: issue?.CurrentStage ?? WorkflowStage.Failed,
                    Detail: issue?.Detail,
                    Timestamp: e.Timestamp);
            })
            .ToList();
    }
}
