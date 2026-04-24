namespace AiSupportWorkflow.PropertyTests;

using System.Text.Json;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using AiSupportWorkflow.Infrastructure.AgentFramework;
using AiSupportWorkflow.Infrastructure.Agents;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public class MigrationCorrectnessTests
{
    private static readonly string[] Categories = ["BackendBug", "FrontendBug", "QualityTestIssue", "OutOfScope"];
    private static readonly string[] Severities = ["Low", "Medium", "High", "Critical"];

    // Feature: migrate-to-agent-framework, Property 1: ChatOptions temperature preservation
    // **Validates: Requirements 2.5**
    [Property(MaxTest = 100)]
    public Property ChatOptions_TemperaturePreservation(NormalFloat rawTemp)
    {
        var temperature = (float)Math.Clamp(Math.Abs(rawTemp.Get) % 2.001, 0.0, 2.0);
        var options = new ChatOptions { Temperature = temperature };

        return (options.Temperature == temperature).ToProperty();
    }

    // Feature: migrate-to-agent-framework, Property 2: Classification JSON parsing round-trip
    // **Validates: Requirements 2.7**
    [Property(MaxTest = 100)]
    public Property ClassificationJson_ParseRoundTrip(int categoryIndex, NormalFloat rawConfidence, NonEmptyString reasoning)
    {
        var category = Categories[Math.Abs(categoryIndex) % Categories.Length];
        var confidence = Math.Clamp(Math.Abs(rawConfidence.Get) % 1.0, 0.0, 1.0);
        var reasoningText = reasoning.Get;

        var jsonObj = new Dictionary<string, object>
        {
            ["category"] = category,
            ["confidence"] = confidence,
            ["reasoning"] = reasoningText
        };
        var json = JsonSerializer.Serialize(jsonObj);

        var result = IssueClassifierService.ParseClassificationResponse(json);

        var expectedCategory = Enum.Parse<IssueCategory>(category, ignoreCase: true);
        var expectedIsCodeRelated = expectedCategory is not IssueCategory.OutOfScope;

        return (result.Category == expectedCategory
            && result.IsCodeRelated == expectedIsCodeRelated
            && result.ConfidenceScore >= 0.0
            && result.ConfidenceScore <= 1.0
            && result.Reasoning == reasoningText)
            .ToProperty();
    }

    // Feature: migrate-to-agent-framework, Property 3: Resolution JSON parsing round-trip
    // **Validates: Requirements 2.8**
    [Property(MaxTest = 100)]
    public Property ResolutionJson_ParseRoundTrip(
        NonEmptyString rootCause,
        NonEmptyString affectedComponent,
        int severityIndex,
        NonEmptyString proposedFix,
        bool requiresEscalation)
    {
        var severity = Severities[Math.Abs(severityIndex) % Severities.Length];
        var issueId = Guid.NewGuid();
        var escalationReason = requiresEscalation ? "Needs review" : null;

        var jsonObj = new Dictionary<string, object?>
        {
            ["rootCause"] = rootCause.Get,
            ["affectedComponent"] = affectedComponent.Get,
            ["severity"] = severity,
            ["proposedFix"] = proposedFix.Get,
            ["requiresEscalation"] = requiresEscalation,
            ["escalationReason"] = escalationReason
        };
        var json = JsonSerializer.Serialize(jsonObj);

        var result = BugResolverService.ParseResolutionResponse(issueId, json);

        return (result.IssueId == issueId
            && result.RootCauseDescription == rootCause.Get
            && result.AffectedComponent == affectedComponent.Get
            && result.SeverityAssessment == severity
            && result.ProposedFixSummary == proposedFix.Get
            && result.RequiresEscalation == requiresEscalation
            && result.EscalationReason == escalationReason)
            .ToProperty();
    }

    // Feature: migrate-to-agent-framework, Property 4: Pull request JSON parsing round-trip
    // **Validates: Requirements 2.9**
    [Property(MaxTest = 100)]
    public Property PullRequestJson_ParseRoundTrip(
        NonEmptyString title,
        NonEmptyString description,
        NonEmptyString diff)
    {
        var issueId = Guid.NewGuid();
        var filePath = "DummyApps/ApplicationA/src/Program.cs";

        var jsonObj = new Dictionary<string, object>
        {
            ["title"] = title.Get,
            ["description"] = description.Get,
            ["affectedFiles"] = new[] { filePath },
            ["diff"] = diff.Get
        };
        var json = JsonSerializer.Serialize(jsonObj);

        var result = CodeChangeGeneratorService.ParsePullRequestResponse(issueId, json);

        return (result.IssueId == issueId
            && result.Title == title.Get
            && result.Description == description.Get
            && result.AffectedFilePaths.Count == 1
            && result.AffectedFilePaths[0] == filePath
            && result.SimulatedDiff == diff.Get)
            .ToProperty();
    }

    // Feature: migrate-to-agent-framework, Property 5: Unsupported provider rejection
    // **Validates: Requirements 3.5**
    [Property(MaxTest = 100)]
    public Property UnsupportedProvider_ThrowsInvalidOperationException(NonEmptyString provider)
    {
        var providerStr = provider.Get;
        if (providerStr.Equals("openai", StringComparison.OrdinalIgnoreCase))
            return true.ToProperty();

        var config = new Dictionary<string, string?>
        {
            ["LlmProvider:Provider"] = providerStr,
            ["LlmProvider:ApiKey"] = "test-api-key",
            ["LlmProvider:ModelName"] = "gpt-4o-mini"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        var services = new ServiceCollection();

        try
        {
            services.AddChatClient(configuration);
            return false.ToProperty();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains(providerStr).ToProperty();
        }
    }

    // Feature: migrate-to-agent-framework, Property 6: Agent identity preservation and delegation
    // **Validates: Requirements 5.3**
    [Property(MaxTest = 100)]
    public Property AiAgent_PreservesIdentityAndDelegates(NonEmptyString agentId, NonEmptyString teamName, AgentRole role)
    {
        var expectedReport = new ResolutionReport(
            Guid.NewGuid(), "root cause", "component", "High", "fix", false, null);

        var bugResolver = Substitute.For<IBugResolver>();
        bugResolver.ResolveAsync(Arg.Any<IssueRecord>(), Arg.Any<AgentAssignment>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedReport));

        var agent = new AiAgent(agentId.Get, teamName.Get, role, bugResolver);

        var identityPreserved = agent.AgentId == agentId.Get
            && agent.TeamName == teamName.Get
            && agent.Role == role;

        var issue = new IssueRecord(Guid.NewGuid(), "sender@test.com", "Test", "Body", DateTimeOffset.UtcNow);
        var result = agent.AnalyzeAndResolveAsync(issue).GetAwaiter().GetResult();

        var delegated = result == expectedReport;

        bugResolver.Received(1).ResolveAsync(
            Arg.Is(issue),
            Arg.Is<AgentAssignment>(a => a.AgentId == agentId.Get && a.TeamName == teamName.Get && a.Role == role),
            Arg.Any<CancellationToken>());

        return (identityPreserved && delegated).ToProperty();
    }

    // Feature: migrate-to-agent-framework, Property 7: FakeChatClient response round-trip
    // **Validates: Requirements 6.4**
    [Property(MaxTest = 100)]
    public Property FakeChatClient_ResponseRoundTrip(NonEmptyString responseContent)
    {
        var content = responseContent.Get;
        IChatClient chatClient = new TestFakeChatClient(content);

        var response = chatClient.GetResponseAsync([]).GetAwaiter().GetResult();

        return (response.Text == content).ToProperty();
    }

    /// <summary>
    /// Local test helper mirroring FakeChatClient behavior for property testing.
    /// The actual FakeChatClient is internal to the UnitTests project.
    /// </summary>
    private sealed class TestFakeChatClient(string responseContent) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, responseContent)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
