namespace AiSupportWorkflow.PropertyTests;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;
using AiSupportWorkflow.PropertyTests.Generators;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

public class LoggingProperties
{
    private sealed class CapturingLogger : ILogger<Orchestrator>
    {
        public List<(LogLevel Level, string Message, IReadOnlyList<KeyValuePair<string, object?>> Properties)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = new List<KeyValuePair<string, object?>>();
            if (state is IReadOnlyList<KeyValuePair<string, object?>> kvps)
            {
                properties.AddRange(kvps);
            }
            Entries.Add((logLevel, formatter(state, exception), properties));
        }
    }

    // Feature: developer-experience-improvements, Property 2: Decision logs include all required structured properties
    // **Validates: Requirements 4.3, 4.4, 4.5**
    [Property(MaxTest = 100)]
    public Property DecisionLogs_IncludeAllRequiredStructuredProperties()
    {
        var gen =
            from classification in ClassificationGenerators.ValidClassificationArb().Generator
                .Where(c => c.IsCodeRelated)
            from teamName in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from appName in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from agentRole in Gen.Elements(AgentRole.BackendDeveloper, AgentRole.FrontendDeveloper, AgentRole.QAEngineer)
            select (classification, teamName, appName, agentRole);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (classification, teamName, appName, agentRole) = tuple;

            var capturingLogger = new CapturingLogger();

            var emailProcessor = Substitute.For<IEmailProcessor>();
            var issueClassifier = Substitute.For<IIssueClassifier>();
            var teamRouter = Substitute.For<ITeamRouter>();
            var agentSelector = Substitute.For<IAgentSelector>();
            var codeChangeGenerator = Substitute.For<ICodeChangeGenerator>();
            var stateTracker = Substitute.For<IWorkflowStateTracker>();
            var supervisorBridge = Substitute.For<ISupervisorActorBridge>();

            var issue = new IssueRecord(Guid.NewGuid(), "user@test.com", "Bug",
                "The /orders endpoint returns 500 in ApplicationA", DateTimeOffset.UtcNow);
            var team = new TeamAssignment(teamName, appName);
            var agentId = $"{teamName}_{agentRole}";
            var agent = new AgentAssignment(agentId, teamName, agentRole);

            emailProcessor.Process(Arg.Any<IncomingEmail>())
                .Returns(Result<IssueRecord>.Success(issue));
            issueClassifier.ClassifyAsync(Arg.Any<IssueRecord>(), Arg.Any<CancellationToken>())
                .Returns(classification);
            teamRouter.Route(Arg.Any<IssueRecord>(), Arg.Any<ClassificationResult>())
                .Returns(Result<TeamAssignment>.Success(team));
            agentSelector.Select(Arg.Any<TeamAssignment>(), Arg.Any<IssueCategory>())
                .Returns(agent);

            var resolution = new ResolutionReport(issue.Id, "Root cause", "Component", "High", "Fix it", false, null);
            supervisorBridge.AssignIssueAsync(
                Arg.Any<string>(), Arg.Any<IssueRecord>(), Arg.Any<IssueCategory>(),
                Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                .Returns(resolution);

            var pr = new PullRequest(Guid.NewGuid(), issue.Id, "Fix", "Description", ["file.cs"], "diff");
            codeChangeGenerator.GenerateAsync(Arg.Any<ResolutionReport>(), Arg.Any<CancellationToken>())
                .Returns(pr);

            var config = Options.Create(new WorkflowConfiguration
            {
                EnableVisualization = false,
                ActorAskTimeoutSeconds = 120
            });

            var sut = new Orchestrator(
                emailProcessor, issueClassifier, teamRouter, agentSelector,
                codeChangeGenerator, stateTracker, supervisorBridge,
                capturingLogger, config);

            sut.ProcessIssueAsync(new IncomingEmail("user@test.com", "Bug",
                "The /orders endpoint returns 500 in ApplicationA")).GetAwaiter().GetResult();

            // Verify classification log has required structured properties
            var classificationLog = capturingLogger.Entries.FirstOrDefault(e =>
                e.Message.Contains("[Visualization] Classification decision"));
            var classificationProps = classificationLog.Properties.Select(p => p.Key).ToHashSet();

            var hasClassificationProps =
                classificationProps.Contains("IssueId") &&
                classificationProps.Contains("Category") &&
                classificationProps.Contains("ConfidenceScore") &&
                classificationProps.Contains("IsCodeRelated") &&
                classificationProps.Contains("Reasoning");

            // Verify team assignment log has required structured properties
            var teamLog = capturingLogger.Entries.FirstOrDefault(e =>
                e.Message.Contains("[Visualization] Team assignment decision"));
            var teamProps = teamLog.Properties.Select(p => p.Key).ToHashSet();

            var hasTeamProps =
                teamProps.Contains("IssueId") &&
                teamProps.Contains("TeamName") &&
                teamProps.Contains("ApplicationName");

            // Verify agent selection log has required structured properties
            var agentLog = capturingLogger.Entries.FirstOrDefault(e =>
                e.Message.Contains("[Visualization] Agent selection decision"));
            var agentProps = agentLog.Properties.Select(p => p.Key).ToHashSet();

            var hasAgentProps =
                agentProps.Contains("IssueId") &&
                agentProps.Contains("AgentId") &&
                agentProps.Contains("Role");

            return (hasClassificationProps && hasTeamProps && hasAgentProps)
                .ToProperty()
                .Label($"Classification props: [{string.Join(", ", classificationProps)}]")
                .Label($"Team props: [{string.Join(", ", teamProps)}]")
                .Label($"Agent props: [{string.Join(", ", agentProps)}]");
        });
    }

    // Feature: developer-experience-improvements, Property 3: Stage transition logs are emitted at Debug level
    // **Validates: Requirements 5.1**
    [Property(MaxTest = 100)]
    public Property StageTransitionLogs_EmittedAtDebugLevel()
    {
        var gen =
            from classification in ClassificationGenerators.ValidClassificationArb().Generator
            from sender in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from subject in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from body in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from teamName in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from appName in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from agentRole in Gen.Elements(AgentRole.BackendDeveloper, AgentRole.FrontendDeveloper, AgentRole.QAEngineer)
            select (classification, sender, subject, body, teamName, appName, agentRole);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (classification, sender, subject, body, teamName, appName, agentRole) = tuple;

            var capturingLogger = new CapturingLogger();

            var emailProcessor = Substitute.For<IEmailProcessor>();
            var issueClassifier = Substitute.For<IIssueClassifier>();
            var teamRouter = Substitute.For<ITeamRouter>();
            var agentSelector = Substitute.For<IAgentSelector>();
            var codeChangeGenerator = Substitute.For<ICodeChangeGenerator>();
            var stateTracker = Substitute.For<IWorkflowStateTracker>();
            var supervisorBridge = Substitute.For<ISupervisorActorBridge>();

            var issue = new IssueRecord(Guid.NewGuid(), sender, subject, body, DateTimeOffset.UtcNow);
            var team = new TeamAssignment(teamName, appName);
            var agentId = $"{teamName}_{agentRole}";
            var agent = new AgentAssignment(agentId, teamName, agentRole);

            emailProcessor.Process(Arg.Any<IncomingEmail>())
                .Returns(Result<IssueRecord>.Success(issue));
            issueClassifier.ClassifyAsync(Arg.Any<IssueRecord>(), Arg.Any<CancellationToken>())
                .Returns(classification);
            teamRouter.Route(Arg.Any<IssueRecord>(), Arg.Any<ClassificationResult>())
                .Returns(Result<TeamAssignment>.Success(team));
            agentSelector.Select(Arg.Any<TeamAssignment>(), Arg.Any<IssueCategory>())
                .Returns(agent);

            var resolution = new ResolutionReport(issue.Id, "Root cause", "Component", "High", "Fix it", false, null);
            supervisorBridge.AssignIssueAsync(
                Arg.Any<string>(), Arg.Any<IssueRecord>(), Arg.Any<IssueCategory>(),
                Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                .Returns(resolution);

            var pr = new PullRequest(Guid.NewGuid(), issue.Id, "Fix", "Description", ["file.cs"], "diff");
            codeChangeGenerator.GenerateAsync(Arg.Any<ResolutionReport>(), Arg.Any<CancellationToken>())
                .Returns(pr);

            var config = Options.Create(new WorkflowConfiguration
            {
                EnableVisualization = false,
                ActorAskTimeoutSeconds = 120
            });

            var sut = new Orchestrator(
                emailProcessor, issueClassifier, teamRouter, agentSelector,
                codeChangeGenerator, stateTracker, supervisorBridge,
                capturingLogger, config);

            sut.ProcessIssueAsync(new IncomingEmail(sender, subject, body)).GetAwaiter().GetResult();

            // Get all stage transition log entries (contain "Workflow stage transition")
            var transitionLogs = capturingLogger.Entries
                .Where(e => e.Message.Contains("Workflow stage transition"))
                .ToList();

            if (classification.IsCodeRelated)
            {
                // Full pipeline: Received, Classified, TeamAssigned, AgentAssigned, Resolving, Resolved, CodeChangeGenerated
                var expectedStages = new[]
                {
                    WorkflowStage.Received, WorkflowStage.Classified, WorkflowStage.TeamAssigned,
                    WorkflowStage.AgentAssigned, WorkflowStage.Resolving, WorkflowStage.Resolved,
                    WorkflowStage.CodeChangeGenerated
                };

                var allDebug = transitionLogs.All(e => e.Level == LogLevel.Debug);
                var hasAllStages = expectedStages.All(stage =>
                    transitionLogs.Any(e =>
                        e.Properties.Any(p => p.Key == "TargetStage" && p.Value?.ToString() == stage.ToString())));

                // Verify each transition log has IssueId, SourceStage, TargetStage, Detail
                var hasRequiredProps = transitionLogs.All(e =>
                {
                    var keys = e.Properties.Select(p => p.Key).ToHashSet();
                    return keys.Contains("IssueId") && keys.Contains("SourceStage") &&
                           keys.Contains("TargetStage") && keys.Contains("Detail");
                });

                return (allDebug && hasAllStages && hasRequiredProps)
                    .ToProperty()
                    .Label($"allDebug={allDebug}, hasAllStages={hasAllStages}, hasRequiredProps={hasRequiredProps}, transitionCount={transitionLogs.Count}");
            }
            else
            {
                // Out-of-scope: Received, ClassifiedOutOfScope
                var expectedStages = new[] { WorkflowStage.Received, WorkflowStage.ClassifiedOutOfScope };

                var allDebug = transitionLogs.All(e => e.Level == LogLevel.Debug);
                var hasAllStages = expectedStages.All(stage =>
                    transitionLogs.Any(e =>
                        e.Properties.Any(p => p.Key == "TargetStage" && p.Value?.ToString() == stage.ToString())));

                var hasRequiredProps = transitionLogs.All(e =>
                {
                    var keys = e.Properties.Select(p => p.Key).ToHashSet();
                    return keys.Contains("IssueId") && keys.Contains("SourceStage") &&
                           keys.Contains("TargetStage") && keys.Contains("Detail");
                });

                return (allDebug && hasAllStages && hasRequiredProps)
                    .ToProperty()
                    .Label($"OutOfScope: allDebug={allDebug}, hasAllStages={hasAllStages}, hasRequiredProps={hasRequiredProps}, transitionCount={transitionLogs.Count}");
            }
        });
    }

    // Feature: developer-experience-improvements, Property 4: Received stage log includes email metadata
    // **Validates: Requirements 5.2**
    [Property(MaxTest = 100)]
    public Property ReceivedStageLog_IncludesEmailMetadata()
    {
        var gen =
            from sender in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from subject in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from body in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            select (sender, subject, body);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (sender, subject, body) = tuple;

            var capturingLogger = new CapturingLogger();

            var emailProcessor = Substitute.For<IEmailProcessor>();
            var issueClassifier = Substitute.For<IIssueClassifier>();
            var teamRouter = Substitute.For<ITeamRouter>();
            var agentSelector = Substitute.For<IAgentSelector>();
            var codeChangeGenerator = Substitute.For<ICodeChangeGenerator>();
            var stateTracker = Substitute.For<IWorkflowStateTracker>();
            var supervisorBridge = Substitute.For<ISupervisorActorBridge>();

            var issue = new IssueRecord(Guid.NewGuid(), sender, subject, body, DateTimeOffset.UtcNow);

            emailProcessor.Process(Arg.Any<IncomingEmail>())
                .Returns(Result<IssueRecord>.Success(issue));
            // Classification returns out-of-scope so we don't need full pipeline setup
            issueClassifier.ClassifyAsync(Arg.Any<IssueRecord>(), Arg.Any<CancellationToken>())
                .Returns(new ClassificationResult(false, IssueCategory.OutOfScope, 0.8, "Not code related"));

            var config = Options.Create(new WorkflowConfiguration
            {
                EnableVisualization = false,
                ActorAskTimeoutSeconds = 120
            });

            var sut = new Orchestrator(
                emailProcessor, issueClassifier, teamRouter, agentSelector,
                codeChangeGenerator, stateTracker, supervisorBridge,
                capturingLogger, config);

            sut.ProcessIssueAsync(new IncomingEmail(sender, subject, body)).GetAwaiter().GetResult();

            // Find the Received stage transition log
            var receivedLog = capturingLogger.Entries.FirstOrDefault(e =>
                e.Level == LogLevel.Debug &&
                e.Message.Contains("Workflow stage transition") &&
                e.Properties.Any(p => p.Key == "TargetStage" && p.Value?.ToString() == WorkflowStage.Received.ToString()));

            var props = receivedLog.Properties;
            var hasSender = props.Any(p => p.Key == "Sender" && p.Value?.ToString() == sender);
            var hasSubject = props.Any(p => p.Key == "Subject" && p.Value?.ToString() == subject);
            var isDebug = receivedLog.Level == LogLevel.Debug;

            return (hasSender && hasSubject && isDebug)
                .ToProperty()
                .Label($"hasSender={hasSender}, hasSubject={hasSubject}, isDebug={isDebug}, sender={sender}, subject={subject}");
        });
    }

    // Feature: developer-experience-improvements, Property 5: Failed stage logs at Warning level
    // **Validates: Requirements 5.3**
    [Property(MaxTest = 100)]
    public Property FailedStageLog_EmittedAtWarningLevel()
    {
        var gen =
            from sender in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from subject in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from body in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            from failureMessage in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            select (sender, subject, body, failureMessage);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (sender, subject, body, failureMessage) = tuple;

            var capturingLogger = new CapturingLogger();

            var emailProcessor = Substitute.For<IEmailProcessor>();
            var issueClassifier = Substitute.For<IIssueClassifier>();
            var teamRouter = Substitute.For<ITeamRouter>();
            var agentSelector = Substitute.For<IAgentSelector>();
            var codeChangeGenerator = Substitute.For<ICodeChangeGenerator>();
            var stateTracker = Substitute.For<IWorkflowStateTracker>();
            var supervisorBridge = Substitute.For<ISupervisorActorBridge>();

            var issue = new IssueRecord(Guid.NewGuid(), sender, subject, body, DateTimeOffset.UtcNow);

            emailProcessor.Process(Arg.Any<IncomingEmail>())
                .Returns(Result<IssueRecord>.Success(issue));
            // Classification succeeds as code-related so we enter the try block
            issueClassifier.ClassifyAsync(Arg.Any<IssueRecord>(), Arg.Any<CancellationToken>())
                .Returns(new ClassificationResult(true, IssueCategory.BackendBug, 0.9, "Backend issue"));
            // Team routing throws to trigger the catch block with our failure message
            teamRouter.Route(Arg.Any<IssueRecord>(), Arg.Any<ClassificationResult>())
                .Returns(Result<TeamAssignment>.Failure(failureMessage));

            var config = Options.Create(new WorkflowConfiguration
            {
                EnableVisualization = false,
                ActorAskTimeoutSeconds = 120
            });

            var sut = new Orchestrator(
                emailProcessor, issueClassifier, teamRouter, agentSelector,
                codeChangeGenerator, stateTracker, supervisorBridge,
                capturingLogger, config);

            sut.ProcessIssueAsync(new IncomingEmail(sender, subject, body)).GetAwaiter().GetResult();

            // Find the Failed stage transition log
            var failedLog = capturingLogger.Entries.FirstOrDefault(e =>
                e.Message.Contains("Workflow stage transition FAILED"));

            var isWarning = failedLog.Level == LogLevel.Warning;
            var hasFailureReason = failedLog.Properties.Any(p =>
                p.Key == "FailureReason" && p.Value?.ToString()?.Contains(failureMessage) == true);
            var hasIssueId = failedLog.Properties.Any(p => p.Key == "IssueId");
            var hasSourceStage = failedLog.Properties.Any(p => p.Key == "SourceStage");
            var hasTargetStage = failedLog.Properties.Any(p =>
                p.Key == "TargetStage" && p.Value?.ToString() == WorkflowStage.Failed.ToString());

            return (isWarning && hasFailureReason && hasIssueId && hasSourceStage && hasTargetStage)
                .ToProperty()
                .Label($"isWarning={isWarning}, hasFailureReason={hasFailureReason}, hasIssueId={hasIssueId}, hasSourceStage={hasSourceStage}, hasTargetStage={hasTargetStage}");
        });
    }
}
