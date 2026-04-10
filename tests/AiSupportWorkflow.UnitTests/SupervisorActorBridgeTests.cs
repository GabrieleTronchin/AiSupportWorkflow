namespace AiSupportWorkflow.UnitTests;

using Akka.Actor;
using Akka.Hosting;
using Akka.TestKit.Xunit2;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Messages;
using AiSupportWorkflow.Domain.ValueObjects;
using AiSupportWorkflow.Infrastructure.Actors;

public class SupervisorActorBridgeTests : TestKit
{
    private sealed class StubRequiredActor(IActorRef actorRef) : IRequiredActor<SupervisorActor>
    {
        public IActorRef ActorRef { get; } = actorRef;
        public Task<IActorRef> GetAsync(CancellationToken ct = default) => Task.FromResult(ActorRef);
    }

    [Fact]
    public async Task AssignIssueAsync_SendsCorrectMessage_ReturnsResolutionReport()
    {
        var probe = CreateTestProbe();
        var bridge = new SupervisorActorBridge(new StubRequiredActor(probe.Ref));

        var issueId = Guid.NewGuid();
        var issue = new IssueRecord(issueId, "user@test.com", "Bug", "Details", DateTimeOffset.UtcNow);
        var expectedReport = new ResolutionReport(issueId, "Root cause", "Component", "High", "Fix it", false, null);

        var task = bridge.AssignIssueAsync("agent-1", issue, IssueCategory.BackendBug, TimeSpan.FromSeconds(5), CancellationToken.None);

        var msg = probe.ExpectMsg<AssignIssueMessage>();
        Assert.Equal("agent-1", msg.TargetAgentId);
        Assert.Equal(issue, msg.Issue);
        Assert.Equal(IssueCategory.BackendBug, msg.Category);

        probe.Reply(new ResolutionCompleteMessage(issueId, expectedReport));

        var result = await task;
        Assert.Equal(expectedReport, result);
    }
}
