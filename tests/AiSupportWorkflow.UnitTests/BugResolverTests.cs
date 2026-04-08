namespace AiSupportWorkflow.UnitTests;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.ValueObjects;
using AiSupportWorkflow.Infrastructure.SemanticKernel;
using AiSupportWorkflow.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

public class BugResolverTests
{
    private static IssueRecord MakeIssue() =>
        new(Guid.NewGuid(), "user@test.com", "Bug", "Details", DateTimeOffset.UtcNow);

    private static AgentAssignment MakeAgent() =>
        new("TeamA_BackendDeveloper", "TeamA", AgentRole.BackendDeveloper);

    private static BugResolverService CreateSut(IChatCompletionService chatService)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        return new BugResolverService(builder.Build(), NullLogger<BugResolverService>.Instance);
    }

    [Fact]
    public async Task ResolveAsync_ValidJson_ReturnsResolutionReport()
    {
        var issue = MakeIssue();
        var json = """
        {
          "rootCause":"Null reference in OrderService",
          "affectedComponent":"OrderController",
          "severity":"High",
          "proposedFix":"Add null check before accessing order",
          "requiresEscalation":false,
          "escalationReason":null
        }
        """;
        var sut = CreateSut(new FakeChatCompletionService(json));

        var result = await sut.ResolveAsync(issue, MakeAgent());

        Assert.Equal(issue.Id, result.IssueId);
        Assert.Equal("Null reference in OrderService", result.RootCauseDescription);
        Assert.Equal("OrderController", result.AffectedComponent);
        Assert.Equal("High", result.SeverityAssessment);
        Assert.False(result.RequiresEscalation);
    }

    [Fact]
    public async Task ResolveAsync_LlmThrowsException_ReturnsEscalatedReport()
    {
        var issue = MakeIssue();
        var sut = CreateSut(new FakeChatCompletionService(new HttpRequestException("API down")));

        var result = await sut.ResolveAsync(issue, MakeAgent());

        Assert.Equal(issue.Id, result.IssueId);
        Assert.True(result.RequiresEscalation);
        Assert.Contains("LLM error", result.EscalationReason!);
    }
}
