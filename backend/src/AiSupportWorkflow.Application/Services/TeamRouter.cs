namespace AiSupportWorkflow.Application.Services;

using System.Text.RegularExpressions;
using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Extensions.Options;

public partial class TeamRouter(IOptions<WorkflowConfiguration> config) : ITeamRouter
{
    public Result<TeamAssignment> Route(IssueRecord issue, ClassificationResult classification)
    {
        var content = $"{issue.Subject} {issue.Body}";
        var mentionsA = AppAPattern().IsMatch(content);
        var mentionsB = AppBPattern().IsMatch(content);

        return (mentionsA, mentionsB) switch
        {
            (true, false) => FindTeamForApplication("ApplicationA"),
            (false, true) => FindTeamForApplication("ApplicationB"),
            (true, true) => Result<TeamAssignment>.Failure(
                "Ambiguous routing: both ApplicationA and ApplicationB are mentioned."),
            _ => Result<TeamAssignment>.Failure(
                "Cannot determine affected application: neither ApplicationA nor ApplicationB is mentioned.")
        };
    }

    private Result<TeamAssignment> FindTeamForApplication(string applicationName)
    {
        var team = config.Value.Teams.FirstOrDefault(t =>
            t.ApplicationName.Equals(applicationName, StringComparison.OrdinalIgnoreCase));

        return team is not null
            ? Result<TeamAssignment>.Success(new TeamAssignment(team.TeamName, team.ApplicationName))
            : Result<TeamAssignment>.Failure($"No team configured for {applicationName}.");
    }

    [GeneratedRegex(@"Application\s?A", RegexOptions.IgnoreCase)]
    private static partial Regex AppAPattern();

    [GeneratedRegex(@"Application\s?B", RegexOptions.IgnoreCase)]
    private static partial Regex AppBPattern();
}
