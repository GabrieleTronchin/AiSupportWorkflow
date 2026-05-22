namespace AiSupportWorkflow.PropertyTests;

using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.Infrastructure.Services;
using AiSupportWorkflow.PropertyTests.Helpers;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;

public class PersistenceProperties
{
    [Property(MaxTest = 100)]
    public Property StateTransition_DualWrite_CreatesIssueAndEvent(
        Guid issueId,
        WorkflowStage stage,
        string? detail)
    {
        var factory = new TestDbContextFactory();
        var tracker = new EfWorkflowStateTracker(factory);

        tracker.TransitionAsync(issueId, stage, detail).GetAwaiter().GetResult();

        using var context = factory.CreateDbContext();
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

    [Property(MaxTest = 100)]
    public Property StateTransition_DualWrite_MultipleTransitions_TracksPreviousStage(
        Guid issueId,
        WorkflowStage firstStage,
        WorkflowStage secondStage)
    {
        var factory = new TestDbContextFactory();
        var tracker = new EfWorkflowStateTracker(factory);

        tracker.TransitionAsync(issueId, firstStage, "first").GetAwaiter().GetResult();
        tracker.TransitionAsync(issueId, secondStage, "second").GetAwaiter().GetResult();

        using var context = factory.CreateDbContext();
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

    [Property(MaxTest = 100)]
    public Property StateTransition_PublishesToUpdateChannel(
        Guid issueId,
        WorkflowStage stage,
        string? detail)
    {
        var factory = new TestDbContextFactory();
        var channel = new WorkflowUpdateChannel();
        var tracker = new EfWorkflowStateTracker(factory, channel);

        tracker.TransitionAsync(issueId, stage, detail).GetAwaiter().GetResult();

        var hasUpdate = channel.Reader.TryRead(out var state);

        return (hasUpdate
            && state!.IssueId == issueId
            && state.Stage == stage
            && state.Detail == detail).ToProperty();
    }
}
