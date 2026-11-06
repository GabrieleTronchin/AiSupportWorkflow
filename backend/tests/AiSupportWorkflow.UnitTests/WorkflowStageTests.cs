namespace AiSupportWorkflow.UnitTests;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;

public class WorkflowStageTests
{
    [Theory]
    [InlineData(WorkflowStage.Failed)]
    [InlineData(WorkflowStage.CodeChangeGenerated)]
    [InlineData(WorkflowStage.ClassifiedOutOfScope)]
    [InlineData(WorkflowStage.ManualReviewRequired)] // rejected or aborted: the run has ended
    public void TerminalStages_AreTerminal(WorkflowStage stage)
    {
        Assert.True(stage.IsTerminal());
        Assert.True(new WorkflowState(Guid.NewGuid(), stage, DateTimeOffset.UtcNow, null).IsTerminal);
        Assert.Contains(stage, WorkflowStageExtensions.TerminalStages);
    }

    [Theory]
    [InlineData(WorkflowStage.Received)]
    [InlineData(WorkflowStage.Classified)]
    [InlineData(WorkflowStage.TeamAssigned)]
    [InlineData(WorkflowStage.AgentAssigned)]
    [InlineData(WorkflowStage.Resolving)]
    [InlineData(WorkflowStage.Resolved)]
    [InlineData(WorkflowStage.AwaitingApproval)]
    public void InProgressStages_AreNotTerminal(WorkflowStage stage)
    {
        Assert.False(stage.IsTerminal());
        Assert.DoesNotContain(stage, WorkflowStageExtensions.TerminalStages);
    }
}
