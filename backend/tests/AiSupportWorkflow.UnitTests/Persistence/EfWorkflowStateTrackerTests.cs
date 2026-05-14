namespace AiSupportWorkflow.UnitTests.Persistence;

using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.UnitTests.Helpers;
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
    public async Task TransitionAsync_CreatesIssueEntity_WhenNotExists()
    {
        // Arrange
        using var context = CreateContext();
        var tracker = new EfWorkflowStateTracker(new TestDbContextFactory(context));
        var issueId = Guid.NewGuid();

        // Act
        await tracker.TransitionAsync(issueId, WorkflowStage.Received, "New issue");

        // Assert
        var entity = context.Issues.Find(issueId);
        Assert.NotNull(entity);
        Assert.Equal(WorkflowStage.Received, entity.CurrentStage);
        Assert.Equal("New issue", entity.Detail);
    }

    [Fact]
    public async Task TransitionAsync_UpdatesExistingIssueEntity()
    {
        // Arrange
        using var context = CreateContext();
        var tracker = new EfWorkflowStateTracker(new TestDbContextFactory(context));
        var issueId = Guid.NewGuid();

        await tracker.TransitionAsync(issueId, WorkflowStage.Received, "Initial");

        // Act
        await tracker.TransitionAsync(issueId, WorkflowStage.Classified, "Classified as backend bug");

        // Assert
        var entity = context.Issues.Find(issueId);
        Assert.NotNull(entity);
        Assert.Equal(WorkflowStage.Classified, entity.CurrentStage);
        Assert.Equal("Classified as backend bug", entity.Detail);
    }

    [Fact]
    public async Task TransitionAsync_CreatesStateTransitionEvent()
    {
        // Arrange
        using var context = CreateContext();
        var tracker = new EfWorkflowStateTracker(new TestDbContextFactory(context));
        var issueId = Guid.NewGuid();

        // Act
        await tracker.TransitionAsync(issueId, WorkflowStage.Received, "Event detail");

        // Assert
        var events = await context.Events.Where(e => e.IssueId == issueId).ToListAsync();
        Assert.Single(events);
        Assert.Equal(WorkflowStage.Received, events[0].NewStage);
        Assert.Equal("Event detail", events[0].Detail);
    }

    [Fact]
    public async Task GetAllStates_ReturnsAllIssues()
    {
        // Arrange
        using var context = CreateContext();
        var tracker = new EfWorkflowStateTracker(new TestDbContextFactory(context));

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        await tracker.TransitionAsync(id1, WorkflowStage.Received);
        await tracker.TransitionAsync(id2, WorkflowStage.Classified);
        await tracker.TransitionAsync(id3, WorkflowStage.Failed, "Something went wrong");

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
        var tracker = new EfWorkflowStateTracker(new TestDbContextFactory(context));
        var unknownId = Guid.NewGuid();

        // Act
        var state = tracker.GetState(unknownId);

        // Assert
        Assert.Equal(unknownId, state.IssueId);
        Assert.Equal(WorkflowStage.Received, state.Stage);
    }
}
