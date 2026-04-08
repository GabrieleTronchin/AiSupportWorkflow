namespace AiSupportWorkflow.UnitTests;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Extensions.Options;

public class TeamRouterTests
{
    private readonly TeamRouter _sut;

    public TeamRouterTests()
    {
        var config = Options.Create(new WorkflowConfiguration
        {
            Teams =
            [
                new TeamConfiguration
                {
                    TeamName = "TeamA",
                    ApplicationName = "ApplicationA",
                    Agents = [new AgentRoleConfiguration { Role = AgentRole.BackendDeveloper }]
                },
                new TeamConfiguration
                {
                    TeamName = "TeamB",
                    ApplicationName = "ApplicationB",
                    Agents = [new AgentRoleConfiguration { Role = AgentRole.BackendDeveloper }]
                }
            ]
        });
        _sut = new TeamRouter(config);
    }

    private static IssueRecord MakeIssue(string subject, string body) =>
        new(Guid.NewGuid(), "user@test.com", subject, body, DateTimeOffset.UtcNow);

    private static ClassificationResult CodeRelated() =>
        new(true, IssueCategory.BackendBug, 0.9, "test");

    [Fact]
    public void Route_ApplicationAInBody_ReturnsTeamA()
    {
        var issue = MakeIssue("Bug report", "There is a bug in ApplicationA");

        var result = _sut.Route(issue, CodeRelated());

        Assert.True(result.IsSuccess);
        Assert.Equal("TeamA", result.Value!.TeamName);
    }

    [Fact]
    public void Route_ApplicationBInSubject_ReturnsTeamB()
    {
        var issue = MakeIssue("Application B is broken", "Details here");

        var result = _sut.Route(issue, CodeRelated());

        Assert.True(result.IsSuccess);
        Assert.Equal("TeamB", result.Value!.TeamName);
    }

    [Fact]
    public void Route_BothApplicationsMentioned_ReturnsFailure()
    {
        var issue = MakeIssue("ApplicationA issue", "Also affects Application B");

        var result = _sut.Route(issue, CodeRelated());

        Assert.False(result.IsSuccess);
        Assert.Contains("Ambiguous", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Route_NeitherApplicationMentioned_ReturnsFailure()
    {
        var issue = MakeIssue("Generic bug", "Something is broken");

        var result = _sut.Route(issue, CodeRelated());

        Assert.False(result.IsSuccess);
        Assert.Contains("neither", result.Error!, StringComparison.OrdinalIgnoreCase);
    }
}
