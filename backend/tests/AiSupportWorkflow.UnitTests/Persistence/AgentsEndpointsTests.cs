namespace AiSupportWorkflow.UnitTests.Persistence;

using Akka.Actor;
using Akka.Hosting;
using Akka.TestKit.Xunit2;
using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Messages;
using AiSupportWorkflow.Infrastructure.Actors;
using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.Infrastructure.Persistence.Entities;
using AiSupportWorkflow.Presentation.Endpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public class AgentsEndpointsTests : TestKit
{
    private sealed class StubRequiredActor(IActorRef actorRef) : IRequiredActor<SupervisorActor>
    {
        public IActorRef ActorRef { get; } = actorRef;
        public Task<IActorRef> GetAsync(CancellationToken ct = default) => Task.FromResult(ActorRef);
    }

    private static WorkflowDbContext CreateDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new WorkflowDbContext(options);
    }

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
        var probe = CreateTestProbe();
        var requiredActor = new StubRequiredActor(probe.Ref);
        var config = CreateConfig("TeamA", AgentRole.BackendDeveloper);
        var agentId = "TeamA_BackendDeveloper";

        using var dbContext = CreateDbContext();

        var issueId = Guid.NewGuid();
        dbContext.Issues.Add(new IssueEntity
        {
            Id = issueId,
            CurrentStage = WorkflowStage.Resolving,
            LastUpdated = DateTimeOffset.UtcNow,
            Detail = "Bug in OrderController"
        });

        dbContext.Events.Add(new StateTransitionEvent
        {
            Id = Guid.NewGuid(),
            IssueId = issueId,
            PreviousStage = WorkflowStage.TeamAssigned,
            NewStage = WorkflowStage.AgentAssigned,
            Timestamp = DateTimeOffset.UtcNow,
            Detail = agentId
        });

        await dbContext.SaveChangesAsync();

        // Act — invoke the endpoint handler logic directly
        var task = InvokeAgentsEndpoint(requiredActor, config, dbContext, CancellationToken.None);

        // Respond to the Ask from the endpoint
        var msg = probe.ExpectMsg<AgentStatusQuery>();
        probe.Reply(new AggregatedAgentStatusResponse(
        [
            new Domain.Messages.AgentStatusResponse(agentId, "Idle", null)
        ]));

        var result = await task;

        // Assert
        var okResult = Assert.IsType<Ok<List<Presentation.Endpoints.AgentStatusResponse>>>(result);
        var agents = okResult.Value!;
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
        var probe = CreateTestProbe();
        var requiredActor = new StubRequiredActor(probe.Ref);
        var config = CreateConfig("TeamA", AgentRole.BackendDeveloper);
        var agentId = "TeamA_BackendDeveloper";

        using var dbContext = CreateDbContext();
        // No issues or events in the database

        // Act
        var task = InvokeAgentsEndpoint(requiredActor, config, dbContext, CancellationToken.None);

        var msg = probe.ExpectMsg<AgentStatusQuery>();
        probe.Reply(new AggregatedAgentStatusResponse(
        [
            new Domain.Messages.AgentStatusResponse(agentId, "Idle", null)
        ]));

        var result = await task;

        // Assert
        var okResult = Assert.IsType<Ok<List<Presentation.Endpoints.AgentStatusResponse>>>(result);
        var agents = okResult.Value!;
        Assert.Single(agents);

        var agent = agents[0];
        Assert.Equal(agentId, agent.AgentId);
        Assert.Equal("Idle", agent.Status);
        Assert.Null(agent.CurrentIssueId);
        Assert.Null(agent.CurrentSubject);
        Assert.Null(agent.CurrentStage);
    }

    /// <summary>
    /// Replicates the endpoint handler logic from AgentsEndpoints for testability.
    /// </summary>
    private static async Task<IResult> InvokeAgentsEndpoint(
        IRequiredActor<SupervisorActor> supervisorActor,
        IOptions<WorkflowConfiguration> config,
        WorkflowDbContext dbContext,
        CancellationToken ct)
    {
        if (!config.Value.EnableVisualization)
            return Results.NotFound(new { Error = "Visualization is disabled." });

        var supervisor = supervisorActor.ActorRef;
        var response = await supervisor.Ask<AggregatedAgentStatusResponse>(
            new AgentStatusQuery(null),
            TimeSpan.FromSeconds(10),
            ct);

        var activeAgents = response.Statuses.ToDictionary(s => s.AgentId, s => s);

        WorkflowStage[] terminalStages =
        [
            WorkflowStage.Failed,
            WorkflowStage.CodeChangeGenerated,
            WorkflowStage.ClassifiedOutOfScope,
        ];

        var nonTerminalIssues = await dbContext.Issues
            .AsNoTracking()
            .Where(i => !terminalStages.Contains(i.CurrentStage))
            .ToListAsync(ct);

        var nonTerminalIssueIds = nonTerminalIssues.Select(i => i.Id).ToList();
        var agentAssignments = await dbContext.Events
            .AsNoTracking()
            .Where(e => nonTerminalIssueIds.Contains(e.IssueId)
                && e.NewStage == WorkflowStage.AgentAssigned
                && e.Detail != null)
            .ToListAsync(ct);

        var agentToIssue = agentAssignments
            .GroupBy(e => e.Detail!)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var latestAssignment = g.OrderByDescending(e => e.Timestamp).First();
                    var issue = nonTerminalIssues.FirstOrDefault(i => i.Id == latestAssignment.IssueId);
                    return issue;
                });

        var allAgents = config.Value.Teams
            .SelectMany(team => team.Agents.Select(agent =>
            {
                var agentId = $"{team.TeamName}_{agent.Role}";
                var isActive = activeAgents.TryGetValue(agentId, out var status);
                var hasAssignedIssue = agentToIssue.TryGetValue(agentId, out var assignedIssue)
                    && assignedIssue is not null;

                var agentStatus = hasAssignedIssue ? "Working"
                    : isActive ? status!.Status
                    : "Idle";

                return new Presentation.Endpoints.AgentStatusResponse(
                    AgentId: agentId,
                    Team: team.TeamName,
                    Role: agent.Role.ToString(),
                    Status: agentStatus,
                    LastAction: isActive ? status!.LastAction : null,
                    CurrentIssueId: hasAssignedIssue ? assignedIssue!.Id.ToString() : null,
                    CurrentSubject: hasAssignedIssue ? assignedIssue!.Detail : null,
                    CurrentStage: hasAssignedIssue ? assignedIssue!.CurrentStage.ToString() : null
                );
            }))
            .ToList();

        return Results.Ok(allAgents);
    }
}
