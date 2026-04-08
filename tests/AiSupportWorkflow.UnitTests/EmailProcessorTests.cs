namespace AiSupportWorkflow.UnitTests;

using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Domain.Entities;

public class EmailProcessorTests
{
    private readonly EmailProcessor _sut = new();

    [Fact]
    public void Process_ValidEmail_ReturnsSuccessWithMatchingFields()
    {
        var email = new IncomingEmail("user@test.com", "Bug in API", "The /orders endpoint returns 500");

        var result = _sut.Process(email);

        Assert.True(result.IsSuccess);
        Assert.Equal("user@test.com", result.Value!.Sender);
        Assert.Equal("Bug in API", result.Value.Subject);
        Assert.Equal("The /orders endpoint returns 500", result.Value.Body);
        Assert.True(result.Value.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Process_ValidEmail_AssignsUniqueId()
    {
        var email = new IncomingEmail("user@test.com", "Bug", "Details");

        var result = _sut.Process(email);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value!.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Process_EmptyOrWhitespaceSubject_ReturnsFailure(string? subject)
    {
        var email = new IncomingEmail("user@test.com", subject!, "Some body");

        var result = _sut.Process(email);

        Assert.False(result.IsSuccess);
        Assert.Contains("subject", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Process_EmptyOrWhitespaceBody_ReturnsFailure(string? body)
    {
        var email = new IncomingEmail("user@test.com", "Valid Subject", body!);

        var result = _sut.Process(email);

        Assert.False(result.IsSuccess);
        Assert.Contains("body", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Process_TwoEmails_AssignsDifferentIds()
    {
        var email1 = new IncomingEmail("a@test.com", "Bug 1", "Body 1");
        var email2 = new IncomingEmail("b@test.com", "Bug 2", "Body 2");

        var result1 = _sut.Process(email1);
        var result2 = _sut.Process(email2);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.NotEqual(result1.Value!.Id, result2.Value!.Id);
    }
}
