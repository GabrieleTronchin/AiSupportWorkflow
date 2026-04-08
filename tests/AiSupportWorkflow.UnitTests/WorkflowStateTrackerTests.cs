namespace AiSupportWorkflow.UnitTests;

using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Infrastructure.Services;

public class WorkflowStateTrackerTests
{
    private readonly WorkflowStateTracker _sut = new();

    [Fact]
    public void Transition_UpdatesState()
    {
        var id = Guid.NewGuid();

        _sut.Transition(id, WorkflowStage.Classified, "BackendBug");

        var state = _sut.GetState(id);
        Assert.Equal(WorkflowStage.Classified, state.Stage);
        Assert.Equal("BackendBug", state.Detail);
    }

    [Fact]
    public void GetState_UnknownId_ReturnsReceived()
    {
        var state = _sut.GetState(Guid.NewGuid());

        Assert.Equal(WorkflowStage.Received, state.Stage);
    }

    [Fact]
    public void GetAllStates_ReturnsAllTracked()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _sut.Transition(id1, WorkflowStage.Classified);
        _sut.Transition(id2, WorkflowStage.TeamAssigned);

        var all = _sut.GetAllStates();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Transition_OverwritesPreviousState()
    {
        var id = Guid.NewGuid();
        _sut.Transition(id, WorkflowStage.Received);
        _sut.Transition(id, WorkflowStage.Classified);

        var state = _sut.GetState(id);
        Assert.Equal(WorkflowStage.Classified, state.Stage);
    }

    [Fact]
    public void ConcurrentTransitions_DoNotCorruptState()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList();

        Parallel.ForEach(ids, id =>
        {
            _sut.Transition(id, WorkflowStage.Received);
            _sut.Transition(id, WorkflowStage.Classified);
            _sut.Transition(id, WorkflowStage.TeamAssigned);
        });

        var all = _sut.GetAllStates();
        Assert.Equal(100, all.Count);

        foreach (var id in ids)
        {
            var state = _sut.GetState(id);
            Assert.Equal(WorkflowStage.TeamAssigned, state.Stage);
        }
    }
}
