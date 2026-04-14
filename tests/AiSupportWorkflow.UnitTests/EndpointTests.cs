namespace AiSupportWorkflow.UnitTests;

using Akka.Hosting;
using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using AiSupportWorkflow.Infrastructure.Actors;
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

public class VisualizationEndpointMetadataTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private IReadOnlyList<RouteEndpoint> _endpoints = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        // Register services needed by VisualizationEndpoints parameter inference
        builder.Services.AddSingleton(Substitute.For<IWorkflowStateTracker>());
        builder.Services.AddSingleton(Substitute.For<IRequiredActor<SupervisorActor>>());
        builder.Services.Configure<WorkflowConfiguration>(c => c.EnableVisualization = false);

        _app = builder.Build();

        var visualizationEndpoints = new VisualizationEndpoints();
        visualizationEndpoints.MapEndpoint(_app);

        await _app.StartAsync();

        var dataSource = _app.Services.GetRequiredService<EndpointDataSource>();
        _endpoints = dataSource.Endpoints.OfType<RouteEndpoint>().ToList();
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private RouteEndpoint GetEndpoint(string pathSuffix)
    {
        return _endpoints.Single(e => e.DisplayName?.Contains(pathSuffix) == true);
    }

    [Fact]
    public void StreamEndpoint_HasVisualizationAndFrontendTags()
    {
        var endpoint = GetEndpoint("/stream");
        var tagMetadata = endpoint.Metadata.GetMetadata<ITagsMetadata>();

        Assert.NotNull(tagMetadata);
        Assert.Contains("Visualization", tagMetadata!.Tags);
        Assert.Contains("Frontend", tagMetadata.Tags);
    }

    [Fact]
    public void AgentsEndpoint_HasVisualizationAndFrontendTags()
    {
        var endpoint = GetEndpoint("/agents");
        var tagMetadata = endpoint.Metadata.GetMetadata<ITagsMetadata>();

        Assert.NotNull(tagMetadata);
        Assert.Contains("Visualization", tagMetadata!.Tags);
        Assert.Contains("Frontend", tagMetadata.Tags);
    }

    [Fact]
    public void StreamEndpoint_HasExpectedSummary()
    {
        var endpoint = GetEndpoint("/stream");
        var summary = endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>();

        Assert.NotNull(summary);
        Assert.Equal("Frontend-dedicated: SSE stream of workflow state updates", summary!.Summary);
    }

    [Fact]
    public void AgentsEndpoint_HasExpectedSummary()
    {
        var endpoint = GetEndpoint("/agents");
        var summary = endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>();

        Assert.NotNull(summary);
        Assert.Equal("Frontend-dedicated: Current state of all AI agents", summary!.Summary);
    }

    [Fact]
    public async Task StreamEndpoint_Returns404_WhenVisualizationDisabled()
    {
        using var client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
        var response = await client.GetAsync("/api/support/stream");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AgentsEndpoint_Returns404_WhenVisualizationDisabled()
    {
        using var client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
        var response = await client.GetAsync("/api/support/agents");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
