namespace AiSupportWorkflow.UnitTests.Persistence;

using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class EfWorkflowStateTrackerTests
{
    private static WorkflowDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new WorkflowDbContext(options);
    }

    [Fact]
    public void TransitionAsync_CreatesIssueEntity_WhenNotExists()
    {
        // Arrange
        using var context = CreateContext();
        var tracker = new EfWorkflowStateTracker(context);
        var issueId = Guid.NewGuid();

        // Act
        tracker.Transition(issueId, WorkflowStage.Received, "New issue");

        // Assert
        var entity = context.Issues.Find(issueId);
        Assert.NotNull(entity);
        Assert.Equal(WorkflowStage.Received, entity.CurrentStage);
        Assert.Equal("New issue", entity.Detail);
    }

    [Fact]
    public void TransitionAsync_UpdatesExistingIssueEntity()
    {
        // Arrange
        using var context = CreateContext();
        var tracker = new EfWorkflowStateTracker(context);
        var issueId = Guid.NewGuid();

        tracker.Transition(issueId, WorkflowStage.Received, "Initial");

        // Act
        tracker.Transition(issueId, WorkflowStage.Classified, "Classified as backend bug");

        // Assert
        var entity = context.Issues.Find(issueId);
        Assert.NotNull(entity);
        Assert.Equal(WorkflowStage.Classified, entity.CurrentStage);
        Assert.Equal("Classified as backend bug", entity.Detail);
    }

    [Fact]
    public void GetEventsAsync_RespectsLimitOf200()
    {
        // Arrange
        using var context = CreateContext();
        var tracker = new EfWorkflowStateTracker(context);
        var issueId = Guid.NewGuid();

        for (var i = 0; i < 250; i++)
        {
            tracker.Transition(issueId, WorkflowStage.Received, $"Event {i}");
        }

        // Act
        var events = tracker.GetEvents(200);

        // Assert
        Assert.Equal(200, events.Count);
    }

    [Fact]
    public void GetAllStatesAsync_ReturnsAllIssues()
    {
        // Arrange
        using var context = CreateContext();
        var tracker = new EfWorkflowStateTracker(context);

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        tracker.Transition(id1, WorkflowStage.Received);
        tracker.Transition(id2, WorkflowStage.Classified);
        tracker.Transition(id3, WorkflowStage.Failed, "Something went wrong");

        // Act
        var states = tracker.GetAllStates();

        // Assert
        Assert.Equal(3, states.Count);
        Assert.Contains(states, s => s.IssueId == id1 && s.Stage == WorkflowStage.Received);
        Assert.Contains(states, s => s.IssueId == id2 && s.Stage == WorkflowStage.Classified);
        Assert.Contains(states, s => s.IssueId == id3 && s.Stage == WorkflowStage.Failed);
    }

    [Fact]
    public void GetState_ReturnsDefaultState_WhenIssueNotFound()
    {
        // Arrange
        using var context = CreateContext();
        var tracker = new EfWorkflowStateTracker(context);
        var unknownId = Guid.NewGuid();

        // Act
        var state = tracker.GetState(unknownId);

        // Assert
        Assert.Equal(unknownId, state.IssueId);
        Assert.Equal(WorkflowStage.Received, state.Stage);
    }
}
