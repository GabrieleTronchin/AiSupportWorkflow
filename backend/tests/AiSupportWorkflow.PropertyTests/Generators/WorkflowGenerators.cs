namespace AiSupportWorkflow.PropertyTests.Generators;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;

public static class WorkflowGenerators
{
    private static readonly WorkflowStage[][] ValidPaths =
    [
        [WorkflowStage.Received, WorkflowStage.Classified, WorkflowStage.TeamAssigned,
         WorkflowStage.AgentAssigned, WorkflowStage.Resolving, WorkflowStage.Resolved,
         WorkflowStage.CodeChangeGenerated],
        [WorkflowStage.Received, WorkflowStage.ClassifiedOutOfScope],
        [WorkflowStage.Received, WorkflowStage.Failed],
        [WorkflowStage.Received, WorkflowStage.Classified, WorkflowStage.Failed],
        [WorkflowStage.Received, WorkflowStage.Classified, WorkflowStage.TeamAssigned, WorkflowStage.Failed],
        [WorkflowStage.Received, WorkflowStage.ManualReviewRequired],
    ];

    public static Arbitrary<WorkflowStage[]> ValidWorkflowStageSequenceArb() =>
        Arb.From(Gen.Elements(ValidPaths));

    public static Arbitrary<WorkflowConfiguration> WorkflowConfigurationArb() =>
        Arb.From(
            from enableViz in ArbMap.Default.GeneratorFor<bool>()
            from teamCount in Gen.Choose(1, 4)
            from teams in Gen.ListOf(TeamConfigGen(), teamCount)
            select new WorkflowConfiguration
            {
                EnableVisualization = enableViz,
                Teams = teams.ToList()
            });

    private static Gen<TeamConfiguration> TeamConfigGen() =>
        from teamName in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
        from appName in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
        from agents in Gen.ListOf(AgentRoleConfigGen(), 3)
        select new TeamConfiguration
        {
            TeamName = teamName,
            ApplicationName = appName,
            Agents = agents.DistinctBy(a => a.Role).ToList()
        };

    private static Gen<AgentRoleConfiguration> AgentRoleConfigGen() =>
        from role in Gen.Elements(AgentRole.BackendDeveloper, AgentRole.FrontendDeveloper, AgentRole.QAEngineer)
        from persona in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
        select new AgentRoleConfiguration { Role = role, Persona = persona };
}
