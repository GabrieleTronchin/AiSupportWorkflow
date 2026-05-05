namespace AiSupportWorkflow.UnitTests.Persistence;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using Microsoft.Extensions.Options;
using NSubstitute;

public class AgentsEndpointsTests
{
    private static IOptions<WorkflowConfiguration> CreateConfig(string teamName = "TeamA", AgentRole role = AgentRole.BackendDeveloper)
    {
        return Options.Create(new WorkflowConfiguration
        {
            EnableVisualization = true,
            Teams =
            [
                new TeamConfiguration
                {
                    TeamName = teamName,
                    ApplicationName = "Application A",
                    Agents =
                    [
                        new AgentRoleConfiguration { Role = role, Persona = "Test persona" }
                    ]
                }
            ]
        });
    }

    [Fact]
    public async Task WorkingAgent_WithAssignedIssue_ReturnsCurrentIssueFields()
    {
        // Arrange
        var agentId = "TeamA_BackendDeveloper";
        var issueId = Guid.NewGuid();
        var config = CreateConfig("TeamA", AgentRole.BackendDeveloper);

        var agentStatusProvider = Substitute.For<IAgentStatusProvider>();
        agentStatusProvider.GetAgentStatusesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AgentStatusInfo>
            {
                new(agentId, "Idle", null)
            });

        var eventRepository = Substitute.For<IWorkflowEventRepository>();
        eventRepository.GetAgentAssignmentsForNonTerminalIssuesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AgentAssignmentInfo>
            {
                new(agentId, issueId, WorkflowStage.Resolving, "Bug in OrderController", DateTimeOffset.UtcNow)
            });

        var service = new AgentStatusService(agentStatusProvider, eventRepository, config);

        // Act
        var agents = await service.GetAllAgentStatusesAsync();

        // Assert
        Assert.Single(agents);
        var agent = agents[0];
        Assert.Equal(agentId, agent.AgentId);
        Assert.Equal("Working", agent.Status);
        Assert.Equal(issueId.ToString(), agent.CurrentIssueId);
        Assert.Equal("Bug in OrderController", agent.CurrentSubject);
        Assert.Equal(WorkflowStage.Resolving.ToString(), agent.CurrentStage);
    }

    [Fact]
    public async Task IdleAgent_WithNoAssignedIssue_ReturnsNullForCurrentEmailFields()
    {
        // Arrange
        var agentId = "TeamA_BackendDeveloper";
        var config = CreateConfig("TeamA", AgentRole.BackendDeveloper);

        var agentStatusProvider = Substitute.For<IAgentStatusProvider>();
        agentStatusProvider.GetAgentStatusesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AgentStatusInfo>
            {
                new(agentId, "Idle", null)
            });

        var eventRepository = Substitute.For<IWorkflowEventRepository>();
        eventRepository.GetAgentAssignmentsForNonTerminalIssuesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AgentAssignmentInfo>());

        var service = new AgentStatusService(agentStatusProvider, eventRepository, config);

        // Act
        var agents = await service.GetAllAgentStatusesAsync();

        // Assert
        Assert.Single(agents);
        var agent = agents[0];
        Assert.Equal(agentId, agent.AgentId);
        Assert.Equal("Idle", agent.Status);
        Assert.Null(agent.CurrentIssueId);
        Assert.Null(agent.CurrentSubject);
        Assert.Null(agent.CurrentStage);
    }
}
