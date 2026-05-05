namespace AiSupportWorkflow.UnitTests;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Extensions.Options;

public class AgentSelectorTests
{
    private readonly AgentSelector _sut;

    public AgentSelectorTests()
    {
        var config = Options.Create(new WorkflowConfiguration
        {
            Teams =
            [
                new TeamConfiguration
                {
                    TeamName = "TeamA",
                    ApplicationName = "ApplicationA",
                    Agents =
                    [
                        new AgentRoleConfiguration { Role = AgentRole.BackendDeveloper },
                        new AgentRoleConfiguration { Role = AgentRole.FrontendDeveloper },
                        new AgentRoleConfiguration { Role = AgentRole.QAEngineer }
                    ]
                }
            ]
        });
        _sut = new AgentSelector(config);
    }

    private static TeamAssignment TeamA() => new("TeamA", "ApplicationA");

    [Fact]
    public void Select_BackendBug_ReturnsBackendDeveloper()
    {
        var result = _sut.Select(TeamA(), IssueCategory.BackendBug);

        Assert.Equal(AgentRole.BackendDeveloper, result.Role);
        Assert.Equal("TeamA_BackendDeveloper", result.AgentId);
        Assert.Equal("TeamA", result.TeamName);
    }

    [Fact]
    public void Select_FrontendBug_ReturnsFrontendDeveloper()
    {
        var result = _sut.Select(TeamA(), IssueCategory.FrontendBug);

        Assert.Equal(AgentRole.FrontendDeveloper, result.Role);
        Assert.Equal("TeamA_FrontendDeveloper", result.AgentId);
    }

    [Fact]
    public void Select_QualityTestIssue_ReturnsQAEngineer()
    {
        var result = _sut.Select(TeamA(), IssueCategory.QualityTestIssue);

        Assert.Equal(AgentRole.QAEngineer, result.Role);
        Assert.Equal("TeamA_QAEngineer", result.AgentId);
    }

    [Fact]
    public void Select_AgentIdFormat_IsTeamName_Underscore_Role()
    {
        var result = _sut.Select(TeamA(), IssueCategory.BackendBug);

        Assert.Equal("TeamA_BackendDeveloper", result.AgentId);
    }
}
