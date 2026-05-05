---
inclusion: always
---

# Product Overview

AI Support Workflow is a simulated AI-driven technical support system. It automates the full lifecycle of a support request: receiving emails, classifying issues via LLM, routing to the correct team and agent, performing root cause analysis, and generating code fixes as pull requests. It includes a real-time monitoring dashboard and uses the Transactional Inbox pattern for asynchronous email processing.

## Workflow Pipeline

The `Orchestrator` drives a linear pipeline with these stages (mapped to `WorkflowStage` enum):

1. `Received` — `EmailProcessor.Process` validates the email (subject and body required) and creates an `IssueRecord`.
2. `Classified` / `ClassifiedOutOfScope` — `IIssueClassifier.ClassifyAsync` returns a `ClassificationResult` with `IsCodeRelated`, `IssueCategory`, `ConfidenceScore`, and `Reasoning`. Out-of-scope issues terminate here.
3. `TeamAssigned` — `TeamRouter.Route` matches "Application A" or "Application B" mentions in the email text (regex, case-insensitive). Ambiguous or unmatched emails fail with a `Result<TeamAssignment>.Failure`.
4. `AgentAssigned` — `AgentSelector.Select` maps `IssueCategory` → `AgentRole` (BackendBug→BackendDeveloper, FrontendBug→FrontendDeveloper, QualityTestIssue→QAEngineer). Agent ID format: `{TeamName}_{Role}`.
5. `Resolving` / `Resolved` — The assigned Akka.NET actor agent performs LLM-powered root cause analysis, producing a `ResolutionReport`.
6. `CodeChangeGenerated` — `ICodeChangeGenerator.GenerateAsync` produces a simulated `PullRequest` with diff.

Terminal states: `Failed`, `CodeChangeGenerated`, `ClassifiedOutOfScope` (defined by `WorkflowState.IsTerminal`).

## Email Processing (Transactional Inbox)

Email submission is asynchronous:
1. `POST /api/support/emails` saves the email as an `InboxMessage` and returns HTTP 202 Accepted immediately.
2. The `InboxProcessor` (IHostedService) polls the inbox table for unprocessed messages in FIFO order.
3. Each message is deserialized and passed to the `Orchestrator` for workflow processing.
4. On success: `ProcessedAt` is set. On failure: `Error` is recorded and `ProcessedAt` is set (no infinite retries).

## Domain Model

Entities (immutable records):
- `IncomingEmail(Sender, Subject, Body)` — API input
- `IssueRecord(Id, Sender, Subject, Body, CreatedAt)` — created from email with a new GUID
- `WorkflowState(IssueId, Stage, LastUpdated, Detail)` — tracks pipeline progress
- `BugScenario(ScenarioId, ApplicationName, Category, ...)` — test fixture definition
- `PullRequest(Id, IssueId, Title, Description, AffectedFilePaths, SimulatedDiff)` — output artifact

Persistence entities (EF Core):
- `IssueEntity(Id, CurrentStage, LastUpdated, Detail)` — current state of each issue
- `StateTransitionEvent(Id, IssueId, PreviousStage, NewStage, Timestamp, Detail)` — audit log entry
- `InboxMessage(Id, MessageType, Payload, ReceivedAt, ProcessedAt, Error)` — email queue record

Value objects:
- `Result<T>` — generic success/failure wrapper (no exceptions for business logic)
- `WorkflowResult` — final outcome with factory methods: `Completed`, `OutOfScope`, `Failed`
- `ClassificationResult(IsCodeRelated, Category, ConfidenceScore, Reasoning)`
- `ResolutionReport(IssueId, RootCauseDescription, AffectedComponent, SeverityAssessment, ProposedFixSummary, RequiresEscalation, EscalationReason)`
- `TeamAssignment(TeamName, ApplicationName)`
- `AgentAssignment(AgentId, TeamName, Role)`

Enums:
- `IssueCategory`: BackendBug, FrontendBug, QualityTestIssue, OutOfScope
- `AgentRole`: BackendDeveloper, FrontendDeveloper, QAEngineer
- `WorkflowStage`: Received → Classified | ClassifiedOutOfScope → TeamAssigned → AgentAssigned → Resolving → Resolved → CodeChangeGenerated | Failed | ManualReviewRequired

## Routing and Assignment Rules

- Team routing is text-based: regex matches "Application A" or "Application B" in subject+body. Both mentioned = ambiguous error. Neither mentioned = routing failure.
- Each team maps to one application (`TeamConfiguration.ApplicationName`).
- Agent selection is deterministic: `IssueCategory` maps 1:1 to `AgentRole`. OutOfScope never reaches agent selection.
- Teams, agents, and personas are configuration-driven via `WorkflowConfiguration` in `appsettings.json`.

## API Surface

| Method | Route | Purpose |
|--------|-------|---------|
| POST | `/api/support/emails` | Submit a support email (async — returns 202 Accepted) |
| GET | `/api/support/issues/{id}` | Get workflow state by issue ID |
| GET | `/api/support/issues` | List all processed issues |
| GET | `/api/support/events` | List state transition events (persistent audit log, max 200) |
| GET | `/api/support/agents` | All configured agents with current status (Idle/Working) |
| GET | `/api/support/inbox` | Inbox queue messages with optional status filter |
| gRPC | `WorkflowMonitor.SubscribeToUpdates` | Server streaming for real-time workflow updates |

## Dashboard

A React-based real-time monitoring dashboard at `dashboard/`:
- **Overview**: Pipeline graph (fixed, animated) + email submission form + summary stats
- **Issues**: Filterable table of all issues with current state
- **Event Log**: Persistent audit log of all state transitions (from `/api/support/events`)
- **Agents**: All configured agents with Idle/Working status
- **Inbox**: Email queue monitoring (Queued/Processed/Failed)

Connects to backend via gRPC-Web streaming (real-time) and REST polling (periodic).

## DummyApps Test Fixtures

`DummyApps/` contains two sample applications (ApplicationA, ApplicationB) used as test fixtures. Each has a `BugScenarios.md` defining three scenarios per app (one per `IssueCategory`: BackendBug, FrontendBug, QualityTestIssue) with structured fields: ScenarioId, Category, Description, AffectedFile, LineRange, BuggyCode, and ExpectedFix.

## Key Behavioral Constraints

- Email validation: both `Subject` and `Body` must be non-empty/non-whitespace.
- Classification is async and LLM-backed; all other routing/selection is synchronous and deterministic.
- The `Result<T>` pattern is used for expected failures (validation, routing). Exceptions are reserved for unexpected errors.
- Actor resolution uses `IRequiredActor<SupervisorActor>` from Akka.Hosting with a configurable Ask timeout.
- gRPC streaming and agents endpoint are gated by `WorkflowConfiguration.EnableVisualization`.
- State transitions perform dual-write: update `IssueEntity` + create `StateTransitionEvent`.
- `InboxProcessor` processes messages FIFO by `ReceivedAt`, records errors without retrying.
