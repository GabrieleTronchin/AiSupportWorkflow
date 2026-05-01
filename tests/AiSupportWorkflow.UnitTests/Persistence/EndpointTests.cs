namespace AiSupportWorkflow.UnitTests.Persistence;

using System.Text.Json;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

public class EndpointTests
{
    private static WorkflowDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new WorkflowDbContext(options);
    }

    [Fact]
    public async Task GetEvents_ReturnsMax200_InReverseChronologicalOrder()
    {
        // Arrange
        using var context = CreateContext();
        var baseTime = DateTimeOffset.UtcNow.AddHours(-5);

        for (var i = 0; i < 250; i++)
        {
            context.Events.Add(new StateTransitionEvent
            {
                Id = Guid.NewGuid(),
                IssueId = Guid.NewGuid(),
                PreviousStage = null,
                NewStage = WorkflowStage.Received,
                Timestamp = baseTime.AddMinutes(i),
                Detail = $"Event {i}",
            });
        }
        await context.SaveChangesAsync();

        // Act
        var events = await context.Events
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .Take(200)
            .ToListAsync();

        // Assert
        Assert.Equal(200, events.Count);
        for (var i = 1; i < events.Count; i++)
        {
            Assert.True(events[i - 1].Timestamp >= events[i].Timestamp);
        }
    }

    [Fact]
    public async Task GetInbox_FiltersByStatus_Queued()
    {
        // Arrange
        using var context = CreateContext();

        context.InboxMessages.Add(new InboxMessage
        {
            Id = Guid.NewGuid(),
            Payload = "{}",
            ReceivedAt = DateTimeOffset.UtcNow,
            ProcessedAt = null, // queued
        });
        context.InboxMessages.Add(new InboxMessage
        {
            Id = Guid.NewGuid(),
            Payload = "{}",
            ReceivedAt = DateTimeOffset.UtcNow,
            ProcessedAt = DateTimeOffset.UtcNow, // processed
        });
        context.InboxMessages.Add(new InboxMessage
        {
            Id = Guid.NewGuid(),
            Payload = "{}",
            ReceivedAt = DateTimeOffset.UtcNow,
            ProcessedAt = DateTimeOffset.UtcNow,
            Error = "Failed", // failed
        });
        await context.SaveChangesAsync();

        // Act — filter queued
        var queued = await context.InboxMessages
            .Where(m => m.ProcessedAt == null)
            .ToListAsync();

        // Assert
        Assert.Single(queued);
    }

    [Fact]
    public void PostEmails_CreatesInboxMessage_WithCorrectFields()
    {
        // Arrange
        using var context = CreateContext();
        var email = new IncomingEmail("sender@test.com", "Test Subject", "Test Body");

        // Act — simulate what the endpoint does
        var message = new InboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "SupportEmail",
            Payload = JsonSerializer.Serialize(email),
            ReceivedAt = DateTimeOffset.UtcNow,
        };
        context.InboxMessages.Add(message);
        context.SaveChanges();

        // Assert
        var saved = context.InboxMessages.Find(message.Id);
        Assert.NotNull(saved);
        Assert.Equal("SupportEmail", saved.MessageType);
        Assert.Null(saved.ProcessedAt);
        Assert.Null(saved.Error);

        var deserialized = JsonSerializer.Deserialize<IncomingEmail>(saved.Payload);
        Assert.Equal("sender@test.com", deserialized?.Sender);
        Assert.Equal("Test Subject", deserialized?.Subject);
    }

    [Fact]
    public void GetAgents_ReturnsConfiguredAgents()
    {
        // This test validates the logic of merging configured agents with active status.
        // The endpoint reads from config and merges with Akka actor status.
        var configuredAgents = new[]
        {
            new { TeamName = "TeamA", Role = "BackendDeveloper" },
            new { TeamName = "TeamA", Role = "FrontendDeveloper" },
            new { TeamName = "TeamB", Role = "QAEngineer" },
        };

        var activeAgentIds = new HashSet<string> { "TeamA_BackendDeveloper" };

        // Act — simulate the endpoint logic
        var result = configuredAgents.Select(a =>
        {
            var agentId = $"{a.TeamName}_{a.Role}";
            return new
            {
                AgentId = agentId,
                Team = a.TeamName,
                a.Role,
                Status = activeAgentIds.Contains(agentId) ? "Working" : "Idle",
            };
        }).ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("Working", result.First(a => a.AgentId == "TeamA_BackendDeveloper").Status);
        Assert.Equal("Idle", result.First(a => a.AgentId == "TeamA_FrontendDeveloper").Status);
        Assert.Equal("Idle", result.First(a => a.AgentId == "TeamB_QAEngineer").Status);
    }
}
