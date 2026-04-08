namespace AiSupportWorkflow.PropertyTests;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.ValueObjects;
using AiSupportWorkflow.PropertyTests.Generators;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Options;

public class RoutingProperties
{
    private static WorkflowConfiguration CreateTestConfig() => new()
    {
        Teams =
        [
            new TeamConfiguration
            {
                TeamName = "TeamA",
                ApplicationName = "ApplicationA",
                Agents =
                [
                    new AgentRoleConfiguration { Role = AgentRole.BackendDeveloper, Persona = "BE" },
                    new AgentRoleConfiguration { Role = AgentRole.FrontendDeveloper, Persona = "FE" },
                    new AgentRoleConfiguration { Role = AgentRole.QAEngineer, Persona = "QA" }
                ]
            },
            new TeamConfiguration
            {
                TeamName = "TeamB",
                ApplicationName = "ApplicationB",
                Agents =
                [
                    new AgentRoleConfiguration { Role = AgentRole.BackendDeveloper, Persona = "BE" },
                    new AgentRoleConfiguration { Role = AgentRole.FrontendDeveloper, Persona = "FE" },
                    new AgentRoleConfiguration { Role = AgentRole.QAEngineer, Persona = "QA" }
                ]
            }
        ]
    };

    private static IOptions<WorkflowConfiguration> TestOptions() =>
        Options.Create(CreateTestConfig());

    // Feature: ai-support-workflow, Property 4: Application-to-team mapping
    // **Validates: Requirements 3.1, 3.2, 3.3**
    [Property(MaxTest = 100)]
    public Property ApplicationA_RoutesToTeamA(NonEmptyString sender, NonEmptyString extraBody)
    {
        var router = new TeamRouter(TestOptions());
        var issue = new IssueRecord(
            Guid.NewGuid(), sender.Get, "Bug in ApplicationA",
            $"There is a problem in Application A system. {extraBody.Get}",
            DateTimeOffset.UtcNow);
        var classification = new ClassificationResult(true, IssueCategory.BackendBug, 0.9, "test");

        var result = router.Route(issue, classification);

        return (result.IsSuccess
            && result.Value!.TeamName == "TeamA"
            && result.Value.ApplicationName == "ApplicationA")
            .ToProperty();
    }

    [Property(MaxTest = 100)]
    public Property ApplicationB_RoutesToTeamB(NonEmptyString sender, NonEmptyString extraBody)
    {
        var router = new TeamRouter(TestOptions());
        var issue = new IssueRecord(
            Guid.NewGuid(), sender.Get, "Bug in ApplicationB",
            $"There is a problem in Application B system. {extraBody.Get}",
            DateTimeOffset.UtcNow);
        var classification = new ClassificationResult(true, IssueCategory.BackendBug, 0.9, "test");

        var result = router.Route(issue, classification);

        return (result.IsSuccess
            && result.Value!.TeamName == "TeamB"
            && result.Value.ApplicationName == "ApplicationB")
            .ToProperty();
    }

    // Feature: ai-support-workflow, Property 5: Category-to-role mapping
    // **Validates: Requirements 4.1, 4.2, 4.3, 4.4**
    [Property(MaxTest = 100, Arbitrary = [typeof(ClassificationGenerators)])]
    public Property CategoryMapsToCorrectRole(IssueCategory category)
    {
        if (category == IssueCategory.OutOfScope)
            return true.ToProperty();

        var selector = new AgentSelector(TestOptions());
        var teamAssignment = new TeamAssignment("TeamA", "ApplicationA");
        var assignment = selector.Select(teamAssignment, category);

        var expectedRole = category switch
        {
            IssueCategory.BackendBug => AgentRole.BackendDeveloper,
            IssueCategory.FrontendBug => AgentRole.FrontendDeveloper,
            IssueCategory.QualityTestIssue => AgentRole.QAEngineer,
            _ => throw new InvalidOperationException()
        };

        return (assignment.Role == expectedRole
            && assignment.TeamName == "TeamA")
            .ToProperty();
    }
}
