# Requirements Document

## Introduction

This feature improves the developer experience of the AI Support Workflow project by making the system more debuggable, observable, and ergonomic during local development. It addresses five areas: configurable actor timeouts per environment, clear tagging of frontend-dedicated endpoints, a PowerShell-based SSE monitoring script for terminal use, decoupling workflow logging from the visualization feature flag, and adding structured verbose logging at every workflow stage transition.

## Glossary

- **Orchestrator**: The `Orchestrator` service in the Application layer that drives the workflow pipeline from email receipt through code change generation.
- **Actor_Ask_Timeout**: The `TimeSpan` passed to `ISupervisorActorBridge.AssignIssueAsync` controlling how long the Orchestrator waits for an Akka.NET actor to respond before timing out.
- **WorkflowConfiguration**: The strongly-typed configuration class (`WorkflowConfiguration.cs`) bound to the `Workflow` section of `appsettings.json`.
- **EnableVisualization**: A boolean flag in `WorkflowConfiguration` that controls whether the SSE stream and agents endpoints are active.
- **SSE_Stream_Endpoint**: The `GET /api/support/stream` endpoint that streams workflow state updates via Server-Sent Events.
- **Agents_Endpoint**: The `GET /api/support/agents` endpoint that returns the current state of all AI agents.
- **Visualization_Endpoints**: The collective name for the SSE_Stream_Endpoint and Agents_Endpoint, mapped in `VisualizationEndpoints.cs`.
- **SSE_Monitor_Script**: A PowerShell `.ps1` script that connects to the SSE_Stream_Endpoint and Agents_Endpoint from the terminal to display workflow progress.
- **WorkflowStage**: The enum representing pipeline stages: Received, Classified, ClassifiedOutOfScope, TeamAssigned, AgentAssigned, Resolving, Resolved, CodeChangeGenerated, Failed, ManualReviewRequired.
- **Stage_Transition_Log**: A structured log entry emitted by the Orchestrator each time the workflow transitions between WorkflowStage values.
- **Application_Namespace**: The root namespace `AiSupportWorkflow` used to scope log level configuration in `appsettings.json`.

## Requirements

### Requirement 1: Configurable Actor Ask Timeout Per Environment

**User Story:** As a developer, I want the actor Ask timeout to be configurable via appsettings so that I can set a longer timeout in Development for step-by-step debugging without hitting timeouts, while keeping the current 2-minute timeout in non-development environments.

#### Acceptance Criteria

1. THE WorkflowConfiguration SHALL expose an `ActorAskTimeoutSeconds` integer property with a default value of 120.
2. WHEN the `Workflow:ActorAskTimeoutSeconds` setting is present in appsettings, THE WorkflowConfiguration SHALL bind that value to the `ActorAskTimeoutSeconds` property.
3. WHEN the Orchestrator calls `ISupervisorActorBridge.AssignIssueAsync`, THE Orchestrator SHALL use `TimeSpan.FromSeconds(ActorAskTimeoutSeconds)` from the bound WorkflowConfiguration instead of the hardcoded `TimeSpan.FromMinutes(2)`.
4. THE default `appsettings.json` SHALL NOT include `ActorAskTimeoutSeconds`, preserving the 120-second default for non-development environments.
5. THE `appsettings.Development.json` SHALL set `Workflow:ActorAskTimeoutSeconds` to 600 to allow 10 minutes for debugging.
6. IF `ActorAskTimeoutSeconds` is set to zero or a negative value, THEN THE Orchestrator SHALL fall back to the default value of 120 seconds.

### Requirement 2: Mark Visualization Endpoints as Frontend-Dedicated

**User Story:** As a developer, I want the SSE stream and agents endpoints to be clearly tagged as frontend-dedicated so that their purpose is obvious in API documentation and code, and they remain in the codebase for future frontend integration.

#### Acceptance Criteria

1. THE Visualization_Endpoints SHALL be tagged with both `"Visualization"` and `"Frontend"` OpenAPI tags.
2. THE SSE_Stream_Endpoint SHALL include an OpenAPI summary of `"Frontend-dedicated: SSE stream of workflow state updates"`.
3. THE Agents_Endpoint SHALL include an OpenAPI summary of `"Frontend-dedicated: Current state of all AI agents"`.
4. WHEN `EnableVisualization` is false, THE Visualization_Endpoints SHALL continue to return HTTP 404 with the existing error message.
5. WHEN `EnableVisualization` is true, THE Visualization_Endpoints SHALL function identically to their current behavior.

### Requirement 3: PowerShell SSE Monitor Script

**User Story:** As a developer, I want a PowerShell script that connects to the SSE stream and agents endpoints from the terminal so that I can monitor workflow progress without a frontend.

#### Acceptance Criteria

1. THE SSE_Monitor_Script SHALL be located at `scripts/Monitor-Workflow.ps1` in the repository root.
2. THE SSE_Monitor_Script SHALL accept an optional `-BaseUrl` parameter defaulting to `http://localhost:5080`.
3. WHEN invoked, THE SSE_Monitor_Script SHALL connect to the SSE_Stream_Endpoint at `{BaseUrl}/api/support/stream` and print each received SSE event to the terminal with a timestamp.
4. WHEN invoked with a `-Agents` switch parameter, THE SSE_Monitor_Script SHALL call the Agents_Endpoint at `{BaseUrl}/api/support/agents` and display the agent statuses in a formatted table, then exit.
5. IF the SSE_Stream_Endpoint returns HTTP 404, THEN THE SSE_Monitor_Script SHALL display a message indicating that visualization is disabled and suggest enabling it in appsettings.
6. IF the connection to the base URL fails, THEN THE SSE_Monitor_Script SHALL display an error message including the attempted URL and exit with a non-zero exit code.
7. THE SSE_Monitor_Script SHALL support graceful termination via Ctrl+C during SSE streaming.

### Requirement 4: Decouple Logging from Visualization Flag

**User Story:** As a developer, I want workflow decision logging to be always available regardless of the EnableVisualization flag so that I can observe orchestrator decisions without enabling the SSE/agents endpoints.

#### Acceptance Criteria

1. THE Orchestrator SHALL emit classification, team assignment, and agent selection log entries unconditionally, without checking the `EnableVisualization` flag.
2. THE `EnableVisualization` flag SHALL only control whether the Visualization_Endpoints return data or HTTP 404.
3. WHEN the Orchestrator logs a classification decision, THE Orchestrator SHALL include IssueId, Category, ConfidenceScore, IsCodeRelated, and Reasoning as structured log properties.
4. WHEN the Orchestrator logs a team assignment decision, THE Orchestrator SHALL include IssueId, TeamName, and ApplicationName as structured log properties.
5. WHEN the Orchestrator logs an agent selection decision, THE Orchestrator SHALL include IssueId, AgentId, and Role as structured log properties.
6. THE Orchestrator SHALL remove the `IsVisualizationEnabled` property and all conditional checks gating log output.

### Requirement 5: Structured Verbose Logging at Workflow Stage Transitions

**User Story:** As a developer, I want detailed structured logging at every workflow stage transition so that I can trace the full workflow execution by toggling the log level in appsettings.

#### Acceptance Criteria

1. WHEN the Orchestrator transitions to any WorkflowStage, THE Orchestrator SHALL emit a structured log entry at `Debug` level containing the IssueId, the source WorkflowStage, the target WorkflowStage, and the transition detail string.
2. WHEN the Orchestrator transitions to the `Received` stage, THE Orchestrator SHALL log the email Sender and Subject as additional structured properties at `Debug` level.
3. WHEN the Orchestrator transitions to the `Failed` stage, THE Orchestrator SHALL log the failure reason as a structured property at `Warning` level instead of `Debug` level.
4. THE `appsettings.Development.json` SHALL configure the `Logging:LogLevel` for the Application_Namespace (`AiSupportWorkflow`) to `Debug`.
5. THE default `appsettings.json` SHALL retain `Logging:LogLevel:Default` as `Information`, ensuring verbose logs are suppressed in non-development environments.
6. THE Orchestrator SHALL use the `LoggerMessage` source-generated pattern or structured logging placeholders (not string interpolation) for all stage transition log entries.

### Requirement 6: Update Documentation to Reflect Developer Experience Changes

**User Story:** As a developer, I want the README and docs/ files to accurately reflect the new developer experience features so that documentation stays aligned with the codebase.

#### Acceptance Criteria

1. WHEN the configurable timeout feature is implemented, THE README SHALL document the `Workflow:ActorAskTimeoutSeconds` configuration option in the Getting Started or Configuration section.
2. WHEN the PowerShell monitor script is created, THE README SHALL add a section describing the `scripts/Monitor-Workflow.ps1` script, its parameters (`-BaseUrl`, `-Agents`), and usage examples.
3. WHEN the visualization endpoints are tagged as frontend-dedicated, THE README SHALL update the API Endpoints table to indicate that the `/api/support/stream` and `/api/support/agents` endpoints are frontend-dedicated.
4. WHEN logging is decoupled from the visualization flag, THE README SHALL update the "Key Behavioral Constraints" or equivalent section to state that workflow decision logging is always active and independent of `EnableVisualization`.
5. WHEN structured verbose logging is added, THE README SHALL document how to enable verbose logging by setting the `AiSupportWorkflow` log level to `Debug` in `appsettings.Development.json`.
6. WHEN the configurable timeout feature is implemented, THE `docs/actor-architecture.md` SHALL update the ISupervisorActorBridge section to reference the configurable timeout from `WorkflowConfiguration` instead of a hardcoded 2-minute value.
7. THE `docs/index.md` SHALL remain unchanged unless a new documentation file is added.
8. WHEN all developer experience changes are complete, THE `docs/clean-architecture.md` SHALL remain unchanged because the changes do not alter the layer structure or dependency flow.
9. WHEN all developer experience changes are complete, THE `docs/semantic-kernel-integration.md` SHALL remain unchanged because the changes do not affect Semantic Kernel integration.
