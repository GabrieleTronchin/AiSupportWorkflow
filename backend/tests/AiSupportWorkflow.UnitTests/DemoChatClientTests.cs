namespace AiSupportWorkflow.UnitTests;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.ValueObjects;
using AiSupportWorkflow.Infrastructure.AgentFramework;
using Microsoft.Extensions.AI;

public class DemoChatClientTests
{
    private readonly DemoChatClient _sut = new(TimeSpan.Zero);

    [Theory]
    [InlineData("NullReferenceException in OrderController - Application A", "The API crashes", IssueCategory.BackendBug)]
    [InlineData("Blank total price in OrderSummary component", "The .razor component renders blank", IssueCategory.FrontendBug)]
    [InlineData("Flaky test due to hardcoded date", "The test fails every other day", IssueCategory.QualityTestIssue)]
    public async Task Classification_CodeRelatedEmail_ReturnsExpectedCategory(string subject, string body, IssueCategory expected)
    {
        var response = await _sut.GetResponseAsync<ClassificationResult>(
            $"Subject: {subject}\n\nBody: {body}", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.Result.IsCodeRelated);
        Assert.Equal(expected, response.Result.Category);
    }

    [Fact]
    public async Task Classification_GeneralQuestion_IsOutOfScope()
    {
        var response = await _sut.GetResponseAsync<ClassificationResult>(
            "Subject: How do I reset my password?\n\nBody: I forgot my password.",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(response.Result.IsCodeRelated);
        Assert.Equal(IssueCategory.OutOfScope, response.Result.Category);
    }

    [Fact]
    public async Task Resolution_ReturnsCompleteReportReferencingSourceFile()
    {
        var response = await _sut.GetResponseAsync<ResolutionReport>(
            "Agent: TeamA_BackendDeveloper (BackendDeveloper)\nSubject: Crash\n\nBody: Exception in OrderController.cs line 27",
            cancellationToken: TestContext.Current.CancellationToken);

        var report = response.Result;
        Assert.Equal("OrderController.cs", report.AffectedComponent);
        Assert.Equal("High", report.SeverityAssessment);
        Assert.False(string.IsNullOrWhiteSpace(report.RootCauseDescription));
        Assert.False(string.IsNullOrWhiteSpace(report.ProposedFixSummary));
    }

    [Fact]
    public async Task PullRequest_UsesIssueIdAndComponentFromPrompt()
    {
        var issueId = Guid.NewGuid();
        var prompt = $"""
            Issue ID: {issueId}
            Root Cause: Items is null
            Affected Component: OrderController.cs
            Severity: High
            Proposed Fix: Initialize Items
            """;

        var response = await _sut.GetResponseAsync<PullRequest>(prompt, cancellationToken: TestContext.Current.CancellationToken);

        var pr = response.Result;
        Assert.Equal(issueId, pr.IssueId);
        Assert.Contains("OrderController.cs", pr.Title);
        Assert.Equal(["src/OrderController.cs"], pr.AffectedFilePaths);
        Assert.False(string.IsNullOrWhiteSpace(pr.SimulatedDiff));
    }

    [Fact]
    public async Task Response_ReportsUsageAndDemoModel()
    {
        var response = await _sut.GetResponseAsync(
            [new(ChatRole.User, "Subject: x\n\nBody: y")], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(DemoChatClient.DemoModelId, response.ModelId);
        Assert.True(response.Usage?.InputTokenCount > 0);
        Assert.True(response.Usage?.OutputTokenCount > 0);
    }
}
