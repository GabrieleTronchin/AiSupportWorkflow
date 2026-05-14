namespace AiSupportWorkflow.UnitTests;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using AiSupportWorkflow.Presentation.Endpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using NSubstitute;

public class EndpointTests
{
    [Fact]
    public async Task PostEmail_ValidEmail_ReturnsOk()
    {
        var orchestrator = Substitute.For<IOrchestrator>();
        var issueId = Guid.NewGuid();
        var pr = new PullRequest(Guid.NewGuid(), issueId, "Fix", "Desc", ["file.cs"], "diff");
        orchestrator.ProcessIssueAsync(Arg.Any<IncomingEmail>(), Arg.Any<CancellationToken>())
            .Returns(WorkflowResult.Completed(issueId, pr));

        var email = new IncomingEmail("user@test.com", "Bug in ApplicationA", "Details about the bug");

        var result = await InvokePostEmail(email, orchestrator);

        Assert.IsType<Ok<WorkflowResult>>(result);
    }

    [Fact]
    public async Task PostEmail_EmptySubject_ReturnsBadRequest()
    {
        var orchestrator = Substitute.For<IOrchestrator>();
        var email = new IncomingEmail("user@test.com", "", "Body");

        var result = await InvokePostEmail(email, orchestrator);

        Assert.StartsWith("BadRequest", result.GetType().Name);
    }

    [Fact]
    public async Task PostEmail_EmptyBody_ReturnsBadRequest()
    {
        var orchestrator = Substitute.For<IOrchestrator>();
        var email = new IncomingEmail("user@test.com", "Subject", "  ");

        var result = await InvokePostEmail(email, orchestrator);

        Assert.StartsWith("BadRequest", result.GetType().Name);
    }

    [Fact]
    public void GetIssueById_ReturnsOkWithState()
    {
        var stateTracker = Substitute.For<IWorkflowStateTracker>();
        var id = Guid.NewGuid();
        var state = new WorkflowState(id, WorkflowStage.Classified, DateTimeOffset.UtcNow, "BackendBug");
        stateTracker.GetState(id).Returns(state);

        var result = InvokeGetIssueById(id, stateTracker);

        var okResult = Assert.IsType<Ok<WorkflowState>>(result);
        Assert.Equal(id, okResult.Value!.IssueId);
    }

    [Fact]
    public void GetAllIssues_ReturnsOkWithList()
    {
        var stateTracker = Substitute.For<IWorkflowStateTracker>();
        var states = new List<WorkflowState>
        {
            new(Guid.NewGuid(), WorkflowStage.Classified, DateTimeOffset.UtcNow, null),
            new(Guid.NewGuid(), WorkflowStage.Resolved, DateTimeOffset.UtcNow, null)
        };
        stateTracker.GetAllStates().Returns(states);

        var result = InvokeGetAllIssues(stateTracker);

        var okResult = Assert.IsType<Ok<IReadOnlyList<WorkflowState>>>(result);
        Assert.Equal(2, okResult.Value!.Count);
    }

    private static async Task<IResult> InvokePostEmail(IncomingEmail email, IOrchestrator orchestrator)
    {
        if (string.IsNullOrWhiteSpace(email.Subject) || string.IsNullOrWhiteSpace(email.Body))
            return Results.BadRequest(new { Error = "Subject and Body are required." });

        var result = await orchestrator.ProcessIssueAsync(email);

        return result.IsSuccess
            ? Results.Ok(result)
            : Results.BadRequest(new { result.FailureReason });
    }

    private static IResult InvokeGetIssueById(Guid id, IWorkflowStateTracker stateTracker)
    {
        var state = stateTracker.GetState(id);
        return Results.Ok(state);
    }

    private static IResult InvokeGetAllIssues(IWorkflowStateTracker stateTracker)
    {
        var states = stateTracker.GetAllStates();
        return Results.Ok(states);
    }
}
