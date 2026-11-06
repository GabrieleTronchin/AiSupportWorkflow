namespace AiSupportWorkflow.UnitTests.Persistence;

using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

public class EfWorkflowEventRepositoryTests
{
    private static WorkflowDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void AddAssignedIssue(WorkflowDbContext context, Guid issueId, WorkflowStage currentStage, string agentId)
    {
        context.Issues.Add(new IssueEntity { Id = issueId, CurrentStage = currentStage, LastUpdated = DateTimeOffset.UtcNow });
        context.Events.Add(new StateTransitionEvent
        {
            Id = Guid.NewGuid(),
            IssueId = issueId,
            PreviousStage = WorkflowStage.TeamAssigned,
            NewStage = WorkflowStage.AgentAssigned,
            Timestamp = DateTimeOffset.UtcNow,
            Detail = agentId,
        });
    }

    [Theory]
    [InlineData(WorkflowStage.ManualReviewRequired)]
    [InlineData(WorkflowStage.CodeChangeGenerated)]
    [InlineData(WorkflowStage.Failed)]
    public async Task GetAgentAssignments_TerminalIssue_IsNotReported(WorkflowStage terminalStage)
    {
        using var context = CreateContext();
        AddAssignedIssue(context, Guid.NewGuid(), terminalStage, "TeamA_BackendDeveloper");
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var assignments = await new EfWorkflowEventRepository(context)
            .GetAgentAssignmentsForNonTerminalIssuesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(assignments);
    }

    [Fact]
    public async Task GetAgentAssignments_IssueAwaitingApproval_IsReported()
    {
        using var context = CreateContext();
        var issueId = Guid.NewGuid();
        AddAssignedIssue(context, issueId, WorkflowStage.AwaitingApproval, "TeamA_BackendDeveloper");
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var assignments = await new EfWorkflowEventRepository(context)
            .GetAgentAssignmentsForNonTerminalIssuesAsync(TestContext.Current.CancellationToken);

        var assignment = Assert.Single(assignments);
        Assert.Equal("TeamA_BackendDeveloper", assignment.AgentId);
        Assert.Equal(issueId, assignment.IssueId);
    }
}
