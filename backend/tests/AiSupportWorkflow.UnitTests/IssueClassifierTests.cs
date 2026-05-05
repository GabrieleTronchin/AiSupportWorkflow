namespace AiSupportWorkflow.UnitTests;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Infrastructure.AgentFramework;
using AiSupportWorkflow.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.AI;

public class IssueClassifierTests
{
    private static IssueRecord MakeIssue() =>
        new(Guid.NewGuid(), "user@test.com", "Bug", "Details", DateTimeOffset.UtcNow);

    private static IssueClassifierService CreateSut(IChatClient chatClient) =>
        new(chatClient, NullLogger<IssueClassifierService>.Instance);

    [Fact]
    public async Task ClassifyAsync_ValidBackendBugJson_ReturnsCodeRelatedClassification()
    {
        var json = """{"category":"BackendBug","confidence":0.95,"reasoning":"API error detected"}""";
        var sut = CreateSut(new FakeChatClient(json));

        var result = await sut.ClassifyAsync(MakeIssue());

        Assert.True(result.IsCodeRelated);
        Assert.Equal(IssueCategory.BackendBug, result.Category);
        Assert.Equal(0.95, result.ConfidenceScore, 2);
    }

    [Fact]
    public async Task ClassifyAsync_OutOfScopeJson_ReturnsNotCodeRelated()
    {
        var json = """{"category":"OutOfScope","confidence":0.8,"reasoning":"Billing question"}""";
        var sut = CreateSut(new FakeChatClient(json));

        var result = await sut.ClassifyAsync(MakeIssue());

        Assert.False(result.IsCodeRelated);
        Assert.Equal(IssueCategory.OutOfScope, result.Category);
    }

    [Fact]
    public async Task ClassifyAsync_LlmThrowsException_ReturnsManualReviewFallback()
    {
        var sut = CreateSut(new FakeChatClient(new HttpRequestException("API unreachable")));

        var result = await sut.ClassifyAsync(MakeIssue());

        Assert.False(result.IsCodeRelated);
        Assert.Equal(IssueCategory.OutOfScope, result.Category);
        Assert.Contains("manual review", result.Reasoning, StringComparison.OrdinalIgnoreCase);
    }
}
