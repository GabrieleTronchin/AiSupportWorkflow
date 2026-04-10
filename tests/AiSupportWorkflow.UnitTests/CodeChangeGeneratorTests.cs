namespace AiSupportWorkflow.UnitTests;

using AiSupportWorkflow.Domain.ValueObjects;
using AiSupportWorkflow.Infrastructure.SemanticKernel;
using AiSupportWorkflow.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.ChatCompletion;

public class CodeChangeGeneratorTests
{
    private static ResolutionReport MakeResolution(Guid? issueId = null) =>
        new(issueId ?? Guid.NewGuid(), "Null ref", "OrderController", "High", "Add null check", false, null);

    private static CodeChangeGeneratorService CreateSut(IChatCompletionService chatService) =>
        new(chatService, NullLogger<CodeChangeGeneratorService>.Instance);

    [Fact]
    public async Task GenerateAsync_ValidJson_ReturnsPullRequestWithCorrectIssueId()
    {
        var issueId = Guid.NewGuid();
        var resolution = MakeResolution(issueId);
        var json = """
        {
          "title":"Fix null reference in OrderController",
          "description":"Added null check",
          "affectedFiles":["DummyApps/ApplicationA/src/Controllers/OrderController.cs"],
          "diff":"--- a/file\n+++ b/file\n-old\n+new"
        }
        """;
        var sut = CreateSut(new FakeChatCompletionService(json));

        var pr = await sut.GenerateAsync(resolution);

        Assert.Equal(issueId, pr.IssueId);
        Assert.Equal("Fix null reference in OrderController", pr.Title);
        Assert.Equal("Added null check", pr.Description);
        Assert.Single(pr.AffectedFilePaths);
        Assert.NotEqual(Guid.Empty, pr.Id);
    }

    [Fact]
    public async Task GenerateAsync_LlmThrowsException_ReturnsFallbackPullRequest()
    {
        var issueId = Guid.NewGuid();
        var resolution = MakeResolution(issueId);
        var sut = CreateSut(new FakeChatCompletionService(new HttpRequestException("API down")));

        var pr = await sut.GenerateAsync(resolution);

        Assert.Equal(issueId, pr.IssueId);
        Assert.NotEmpty(pr.Title);
        Assert.NotEmpty(pr.Description);
        Assert.NotEmpty(pr.AffectedFilePaths);
    }
}
