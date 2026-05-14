namespace AiSupportWorkflow.UnitTests.Executors;

using System.Reflection;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using AiSupportWorkflow.Infrastructure.WorkflowEngine.Executors;
using AiSupportWorkflow.UnitTests.Helpers;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using NSubstitute;

public class ClassificationExecutorTests
{
    private readonly IWorkflowStateTracker _stateTracker = Substitute.For<IWorkflowStateTracker>();
    private readonly IWorkflowContext _context = Substitute.For<IWorkflowContext>();

    private static IssueRecord MakeIssue(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "user@test.com", "NullRef in API", "Getting NullReferenceException in Application A", DateTimeOffset.UtcNow);

    private static string ClassificationJson(bool isCodeRelated, string category = "BackendBug", double confidence = 0.92) =>
        $$"""{"IsCodeRelated":{{isCodeRelated.ToString().ToLowerInvariant()}},"Category":"{{category}}","ConfidenceScore":{{confidence}},"Reasoning":"Test reasoning"}""";

    [Fact]
    public async Task HandleAsync_CodeRelatedIssue_TransitionsToClassifiedAndStoresState()
    {
        // Arrange
        var issue = MakeIssue();
        var chatClient = new FakeChatClient(ClassificationJson(true));
        var sut = new ClassificationExecutor(chatClient, _stateTracker);

        // Act
        var result = await InvokeHandlerAsync<ClassificationResult>(sut, issue, _context);

        // Assert
        Assert.True(result.IsCodeRelated);
        Assert.Equal(IssueCategory.BackendBug, result.Category);
        await _stateTracker.Received(1).TransitionAsync(issue.Id, WorkflowStage.Received, Arg.Any<string?>(), subject: issue.Subject);
        await _stateTracker.Received(1).TransitionAsync(issue.Id, WorkflowStage.Classified, Arg.Any<string?>(), Arg.Any<string?>());
        await _context.Received(1).QueueStateUpdateAsync(issue.Id.ToString(), issue, scopeName: "Issues", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_OutOfScopeIssue_TransitionsToOutOfScopeAndYieldsOutput()
    {
        // Arrange
        var issue = MakeIssue();
        var chatClient = new FakeChatClient(ClassificationJson(false, "OutOfScope"));
        var sut = new ClassificationExecutor(chatClient, _stateTracker);

        // Act
        var result = await InvokeHandlerAsync<ClassificationResult>(sut, issue, _context);

        // Assert
        Assert.False(result.IsCodeRelated);
        await _stateTracker.Received(1).TransitionAsync(issue.Id, WorkflowStage.ClassifiedOutOfScope, Arg.Any<string?>(), Arg.Any<string?>());
        await _context.Received(1).YieldOutputAsync(Arg.Any<WorkflowResult>(), Arg.Any<CancellationToken>());
        await _context.DidNotReceive().QueueStateUpdateAsync(Arg.Any<string>(), Arg.Any<IssueRecord>(), scopeName: "Issues", Arg.Any<CancellationToken>());
    }

    private static async ValueTask<TResult> InvokeHandlerAsync<TResult>(
        object executor, object message, IWorkflowContext context)
    {
        var method = executor.GetType().GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method is null)
            throw new InvalidOperationException("HandleAsync method not found");

        var task = (ValueTask<TResult>)method.Invoke(executor, [message, context, CancellationToken.None])!;
        return await task;
    }
}

public class TeamAssignmentExecutorTests
{
    private readonly ITeamRouter _teamRouter = Substitute.For<ITeamRouter>();
    private readonly IWorkflowStateTracker _stateTracker = Substitute.For<IWorkflowStateTracker>();
    private readonly IWorkflowContext _context = Substitute.For<IWorkflowContext>();

    private static readonly Guid IssueId = Guid.NewGuid();

    private static IssueRecord MakeIssue() =>
        new(IssueId, "user@test.com", "Bug in Application A", "NullRef in Application A controller", DateTimeOffset.UtcNow);

    private static ClassificationResult MakeClassification() =>
        new(true, IssueCategory.BackendBug, 0.95, "Backend error");

    public TeamAssignmentExecutorTests()
    {
        _context.ReadStateAsync<Guid>("CurrentIssueId", scopeName: "Workflow", Arg.Any<CancellationToken>())
            .Returns(IssueId);
        _context.ReadStateAsync<IssueRecord>(IssueId.ToString(), scopeName: "Issues", Arg.Any<CancellationToken>())
            .Returns(MakeIssue());
    }

    [Fact]
    public async Task HandleAsync_ValidRouting_ReturnsTeamAssignmentAndTransitions()
    {
        // Arrange
        var classification = MakeClassification();
        var expectedTeam = new TeamAssignment("TeamA", "ApplicationA");
        _teamRouter.Route(Arg.Any<IssueRecord>(), classification).Returns(Result<TeamAssignment>.Success(expectedTeam));
        var sut = new TeamAssignmentExecutor(_teamRouter, _stateTracker);

        // Act
        var result = await InvokeHandlerAsync<TeamAssignment>(sut, classification, _context);

        // Assert
        Assert.Equal("TeamA", result.TeamName);
        Assert.Equal("ApplicationA", result.ApplicationName);
        await _stateTracker.Received(1).TransitionAsync(IssueId, WorkflowStage.TeamAssigned, "TeamA", Arg.Any<string?>());
    }

    [Fact]
    public async Task HandleAsync_RoutingFailure_ThrowsInvalidOperationException()
    {
        // Arrange
        var classification = MakeClassification();
        _teamRouter.Route(Arg.Any<IssueRecord>(), classification)
            .Returns(Result<TeamAssignment>.Failure("Ambiguous routing"));
        var sut = new TeamAssignmentExecutor(_teamRouter, _stateTracker);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => InvokeHandlerAsync<TeamAssignment>(sut, classification, _context).AsTask());
        Assert.Equal("Ambiguous routing", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_MissingIssueInState_ThrowsInvalidOperationException()
    {
        // Arrange
        _context.ReadStateAsync<IssueRecord>(IssueId.ToString(), scopeName: "Issues", Arg.Any<CancellationToken>())
            .Returns((IssueRecord?)null);
        var classification = MakeClassification();
        _teamRouter.Route(Arg.Any<IssueRecord>(), Arg.Any<ClassificationResult>())
            .Returns(Result<TeamAssignment>.Success(new TeamAssignment("TeamA", "ApplicationA")));
        var sut = new TeamAssignmentExecutor(_teamRouter, _stateTracker);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => InvokeHandlerAsync<TeamAssignment>(sut, classification, _context).AsTask());
    }

    private static async ValueTask<TResult> InvokeHandlerAsync<TResult>(
        object executor, object message, IWorkflowContext context)
    {
        var method = executor.GetType().GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method is null)
            throw new InvalidOperationException("HandleAsync method not found");

        var task = (ValueTask<TResult>)method.Invoke(executor, [message, context, CancellationToken.None])!;
        return await task;
    }
}

public class AgentAssignmentExecutorTests
{
    private readonly IAgentSelector _agentSelector = Substitute.For<IAgentSelector>();
    private readonly IWorkflowStateTracker _stateTracker = Substitute.For<IWorkflowStateTracker>();
    private readonly IWorkflowContext _context = Substitute.For<IWorkflowContext>();

    private static readonly Guid IssueId = Guid.NewGuid();

    public AgentAssignmentExecutorTests()
    {
        _context.ReadStateAsync<Guid>("CurrentIssueId", scopeName: "Workflow", Arg.Any<CancellationToken>())
            .Returns(IssueId);
    }

    [Theory]
    [InlineData(IssueCategory.BackendBug, AgentRole.BackendDeveloper)]
    [InlineData(IssueCategory.FrontendBug, AgentRole.FrontendDeveloper)]
    [InlineData(IssueCategory.QualityTestIssue, AgentRole.QAEngineer)]
    public async Task HandleAsync_EachCategory_ReturnsCorrectAgentRole(IssueCategory category, AgentRole expectedRole)
    {
        // Arrange
        var team = new TeamAssignment("TeamA", "ApplicationA");
        var classification = new ClassificationResult(true, category, 0.9, "test");
        var expectedAgent = new AgentAssignment($"TeamA_{expectedRole}", "TeamA", expectedRole);

        _context.ReadStateAsync<ClassificationResult>("LatestClassification", scopeName: "Workflow", Arg.Any<CancellationToken>())
            .Returns(classification);
        _agentSelector.Select(team, category).Returns(expectedAgent);

        var sut = new AgentAssignmentExecutor(_agentSelector, _stateTracker);

        // Act
        var result = await sut.HandleAsync(team, _context, CancellationToken.None);

        // Assert
        Assert.Equal(expectedRole, result.Role);
        Assert.Equal($"TeamA_{expectedRole}", result.AgentId);
        await _stateTracker.Received(1).TransitionAsync(IssueId, WorkflowStage.AgentAssigned, expectedAgent.AgentId, Arg.Any<string?>());
    }

    [Fact]
    public async Task HandleAsync_MissingClassification_ThrowsInvalidOperationException()
    {
        // Arrange
        var team = new TeamAssignment("TeamA", "ApplicationA");
        _context.ReadStateAsync<ClassificationResult>("LatestClassification", scopeName: "Workflow", Arg.Any<CancellationToken>())
            .Returns((ClassificationResult?)null);

        var sut = new AgentAssignmentExecutor(_agentSelector, _stateTracker);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.HandleAsync(team, _context, CancellationToken.None).AsTask());
    }
}

public class ResolutionExecutorTests
{
    private readonly IWorkflowStateTracker _stateTracker = Substitute.For<IWorkflowStateTracker>();
    private readonly IWorkflowContext _context = Substitute.For<IWorkflowContext>();

    private static readonly Guid IssueId = Guid.NewGuid();

    private static IssueRecord MakeIssue() =>
        new(IssueId, "user@test.com", "NullRef in API", "NullReferenceException in Application A", DateTimeOffset.UtcNow);

    private static string ResolutionJson() =>
        $$"""{"IssueId":"{{IssueId}}","RootCauseDescription":"Null check missing","AffectedComponent":"UserController","SeverityAssessment":"High","ProposedFixSummary":"Add null guard","RequiresEscalation":false,"EscalationReason":null}""";

    public ResolutionExecutorTests()
    {
        _context.ReadStateAsync<Guid>("CurrentIssueId", scopeName: "Workflow", Arg.Any<CancellationToken>())
            .Returns(IssueId);
        _context.ReadStateAsync<IssueRecord>(IssueId.ToString(), scopeName: "Issues", Arg.Any<CancellationToken>())
            .Returns(MakeIssue());
    }

    [Fact]
    public async Task HandleAsync_HappyPath_ReturnsResolutionReportAndTransitions()
    {
        // Arrange
        var agent = new AgentAssignment("TeamA_BackendDeveloper", "TeamA", AgentRole.BackendDeveloper);
        var chatClient = new FakeChatClient(ResolutionJson());
        var sut = new ResolutionExecutor(chatClient, _stateTracker);

        // Act
        var result = await InvokeHandlerAsync<ResolutionReport>(sut, agent, _context);

        // Assert
        Assert.Equal("Null check missing", result.RootCauseDescription);
        Assert.Equal("UserController", result.AffectedComponent);
        await _stateTracker.Received(1).TransitionAsync(IssueId, WorkflowStage.Resolving, Arg.Any<string?>(), Arg.Any<string?>());
        await _stateTracker.Received(1).TransitionAsync(IssueId, WorkflowStage.Resolved, "Add null guard", Arg.Any<string?>());
    }

    [Fact]
    public async Task HandleAsync_MissingIssueInState_ThrowsInvalidOperationException()
    {
        // Arrange
        _context.ReadStateAsync<IssueRecord>(IssueId.ToString(), scopeName: "Issues", Arg.Any<CancellationToken>())
            .Returns((IssueRecord?)null);
        var agent = new AgentAssignment("TeamA_BackendDeveloper", "TeamA", AgentRole.BackendDeveloper);
        var chatClient = new FakeChatClient(ResolutionJson());
        var sut = new ResolutionExecutor(chatClient, _stateTracker);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => InvokeHandlerAsync<ResolutionReport>(sut, agent, _context).AsTask());
    }

    private static async ValueTask<TResult> InvokeHandlerAsync<TResult>(
        object executor, object message, IWorkflowContext context)
    {
        var method = executor.GetType().GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method is null)
            throw new InvalidOperationException("HandleAsync method not found");

        var task = (ValueTask<TResult>)method.Invoke(executor, [message, context, CancellationToken.None])!;
        return await task;
    }
}

public class CodeGenerationExecutorTests
{
    private readonly IWorkflowStateTracker _stateTracker = Substitute.For<IWorkflowStateTracker>();
    private readonly IWorkflowContext _context = Substitute.For<IWorkflowContext>();

    private static readonly Guid IssueId = Guid.NewGuid();

    private static ResolutionReport MakeReport() =>
        new(IssueId, "Null check missing", "UserController", "High", "Add null guard", false, null);

    private static string PullRequestJson() =>
        $$"""{"Id":"{{Guid.NewGuid()}}","IssueId":"{{IssueId}}","Title":"Fix null reference in UserController","Description":"Added null guard","AffectedFilePaths":["src/Controllers/UserController.cs"],"SimulatedDiff":"+ if (user == null) return;"}""";

    public CodeGenerationExecutorTests()
    {
        _context.ReadStateAsync<Guid>("CurrentIssueId", scopeName: "Workflow", Arg.Any<CancellationToken>())
            .Returns(IssueId);
        _context.ReadStateAsync<ResolutionReport>("LatestResolution", scopeName: "Workflow", Arg.Any<CancellationToken>())
            .Returns(MakeReport());
    }

    [Fact]
    public async Task HandleAsync_HappyPath_TransitionsAndYieldsOutput()
    {
        // Arrange
        var approval = new ApprovalDecision(true);
        var chatClient = new FakeChatClient(PullRequestJson());
        var sut = new CodeGenerationExecutor(chatClient, _stateTracker);

        // Act
        await InvokeVoidHandlerAsync(sut, approval, _context);

        // Assert
        await _stateTracker.Received(1).TransitionAsync(IssueId, WorkflowStage.CodeChangeGenerated, Arg.Any<string?>(), Arg.Any<string?>());
        await _context.Received(1).YieldOutputAsync(Arg.Any<WorkflowResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_MissingResolution_ThrowsInvalidOperationException()
    {
        // Arrange
        _context.ReadStateAsync<ResolutionReport>("LatestResolution", scopeName: "Workflow", Arg.Any<CancellationToken>())
            .Returns((ResolutionReport?)null);
        var approval = new ApprovalDecision(true);
        var chatClient = new FakeChatClient(PullRequestJson());
        var sut = new CodeGenerationExecutor(chatClient, _stateTracker);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => InvokeVoidHandlerAsync(sut, approval, _context).AsTask());
    }

    private static async ValueTask InvokeVoidHandlerAsync(
        object executor, object message, IWorkflowContext context)
    {
        var method = executor.GetType().GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method is null)
            throw new InvalidOperationException("HandleAsync method not found");

        var task = (ValueTask)method.Invoke(executor, [message, context, CancellationToken.None])!;
        await task;
    }
}
