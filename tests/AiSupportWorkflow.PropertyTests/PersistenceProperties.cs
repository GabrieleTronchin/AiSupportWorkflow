namespace AiSupportWorkflow.PropertyTests;

using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Infrastructure.Persistence;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;

public class PersistenceProperties
{
    private static WorkflowDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new WorkflowDbContext(options);
    }

    // Feature: dashboard-realtime-monitoring, Property 8: State transition dual-write invariant
    // For any issue and any stage transition, the system SHALL both update the issue's current stage
    // in the Issues table AND create a new StateTransitionEvent record with the correct previous stage,
    // new stage, timestamp, and detail.
    // **Validates: Requirements 4.7, 9.6**
    [Property(MaxTest = 100)]
    public Property StateTransition_DualWrite_CreatesIssueAndEvent(
        Guid issueId,
        WorkflowStage stage,
        string? detail)
    {
        using var context = CreateInMemoryContext();
        var tracker = new EfWorkflowStateTracker(context);

        tracker.Transition(issueId, stage, detail);

        var issueEntity = context.Issues.Find(issueId);
        var events = context.Events.Where(e => e.IssueId == issueId).ToList();

        var issueCreated = issueEntity is not null;
        var issueHasCorrectStage = issueEntity?.CurrentStage == stage;
        var issueHasCorrectDetail = issueEntity?.Detail == detail;
        var eventCreated = events.Count == 1;
        var eventHasCorrectStage = events.FirstOrDefault()?.NewStage == stage;
        var eventHasNullPreviousStage = events.FirstOrDefault()?.PreviousStage is null;
        var eventHasCorrectDetail = events.FirstOrDefault()?.Detail == detail;

        return (issueCreated
            && issueHasCorrectStage
            && issueHasCorrectDetail
            && eventCreated
            && eventHasCorrectStage
            && eventHasNullPreviousStage
            && eventHasCorrectDetail).ToProperty();
    }

    // Feature: dashboard-realtime-monitoring, Property 8 (multi-transition variant)
    // For any sequence of stage transitions on the same issue, each transition SHALL update the
    // issue's current stage AND create a new event with the correct previous stage.
    // **Validates: Requirements 4.7, 9.6**
    [Property(MaxTest = 100)]
    public Property StateTransition_DualWrite_MultipleTransitions_TracksPreviousStage(
        Guid issueId,
        WorkflowStage firstStage,
        WorkflowStage secondStage)
    {
        using var context = CreateInMemoryContext();
        var tracker = new EfWorkflowStateTracker(context);

        tracker.Transition(issueId, firstStage, "first");
        tracker.Transition(issueId, secondStage, "second");

        var issueEntity = context.Issues.Find(issueId);
        var events = context.Events
            .Where(e => e.IssueId == issueId)
            .OrderBy(e => e.Timestamp)
            .ToList();

        var issueHasLatestStage = issueEntity?.CurrentStage == secondStage;
        var twoEventsCreated = events.Count == 2;
        var firstEventHasNoPrevious = events[0].PreviousStage is null;
        var firstEventHasCorrectNew = events[0].NewStage == firstStage;
        var secondEventHasPrevious = events[1].PreviousStage == firstStage;
        var secondEventHasCorrectNew = events[1].NewStage == secondStage;

        return (issueHasLatestStage
            && twoEventsCreated
            && firstEventHasNoPrevious
            && firstEventHasCorrectNew
            && secondEventHasPrevious
            && secondEventHasCorrectNew).ToProperty();
    }
}
