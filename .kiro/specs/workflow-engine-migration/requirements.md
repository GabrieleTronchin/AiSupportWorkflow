# Requirements Document

## Introduction

This specification defines the migration of the AI Support Workflow orchestration layer from Akka.NET actors (SupervisorActor, AIAgentActor) and the custom `Orchestrator` class to Microsoft Agent Framework Workflows (WorkflowBuilder, Executors, Edges). The migration replaces manual JSON prompt/response parsing with ChatClientAgent structured output and function calling, introduces Human-in-the-Loop approval before code change generation, adds native workflow streaming events for the dashboard, and includes middleware for LLM call telemetry. The Akka.NET dependency is removed entirely.

## Glossary

- **Workflow_Engine**: The Microsoft.Agents.AI.Workflows runtime that executes a directed graph of steps (nodes connected by edges) with built-in state management, streaming events, and error handling.
- **WorkflowBuilder**: The fluent API from Microsoft.Agents.AI.Workflows used to define workflow graphs declaratively with nodes, edges, and conditions.
- **Executor**: A workflow node that performs a single unit of work (e.g., classification, resolution, code generation). Replaces the concept of an Akka.NET actor performing an async LLM call.
- **Edge**: A directed connection between two Executors in the workflow graph, optionally guarded by a condition that determines whether traversal occurs.
- **ChatClientAgent**: A Microsoft.Agents.AI abstraction that wraps IChatClient with structured output schemas and function-calling support, eliminating manual JSON prompt/response parsing.
- **Human_Approval_Gate**: A workflow node that pauses execution and waits for an external human decision (approve or reject) before allowing the workflow to proceed to the next stage.
- **Workflow_Event**: A streaming event emitted by the Workflow_Engine at each state transition, carrying stage, detail, and timestamp information for real-time monitoring.
- **LLM_Middleware**: A delegating handler or pipeline component that intercepts LLM calls to capture request/response metadata for logging and telemetry.
- **Orchestrator**: The current custom class (`Orchestrator.cs`) that drives the linear pipeline through imperative code. To be replaced by the Workflow_Engine.
- **State_Tracker**: The `IWorkflowStateTracker` implementation that records workflow stage transitions with dual-write semantics (issue entity update + audit event).
- **Dashboard**: The React-based real-time monitoring UI that consumes workflow state updates via gRPC-Web streaming.

## Requirements

### Requirement 1: Workflow Graph Definition

**User Story:** As a developer, I want the support workflow pipeline defined as a declarative workflow graph using WorkflowBuilder, so that the orchestration logic is explicit, maintainable, and leverages the framework's built-in state management.

#### Acceptance Criteria

1. THE Workflow_Engine SHALL define the support pipeline as a directed graph with nodes for: Received, Classification, TeamAssignment, AgentAssignment, Resolution, HumanApproval, and CodeChangeGeneration.
2. WHEN a node completes successfully, THE Workflow_Engine SHALL traverse the outgoing Edge to the next node based on the Edge condition.
3. WHEN the Classification Executor determines an issue is not code-related, THE Workflow_Engine SHALL traverse the Edge to the ClassifiedOutOfScope terminal state and halt execution.
4. WHEN any Executor throws an unhandled exception, THE Workflow_Engine SHALL transition the workflow to the Failed terminal state and record the error detail.
5. THE Workflow_Engine SHALL support the following terminal states: Failed, CodeChangeGenerated, and ClassifiedOutOfScope.
6. THE Workflow_Engine SHALL preserve the existing stage ordering: Received, Classified, TeamAssigned, AgentAssigned, Resolving, Resolved, HumanApproval, CodeChangeGenerated.

### Requirement 2: Executor Implementation

**User Story:** As a developer, I want each pipeline stage implemented as a discrete Executor, so that stages are independently testable, composable, and replaceable.

#### Acceptance Criteria

1. THE Workflow_Engine SHALL implement a Classification_Executor that invokes the ChatClientAgent to classify an issue and returns a ClassificationResult.
2. THE Workflow_Engine SHALL implement a TeamAssignment_Executor that routes the issue to the correct team using the existing deterministic routing logic.
3. THE Workflow_Engine SHALL implement an AgentAssignment_Executor that selects the appropriate agent based on IssueCategory-to-AgentRole mapping.
4. THE Workflow_Engine SHALL implement a Resolution_Executor that invokes the ChatClientAgent to perform root cause analysis and returns a ResolutionReport.
5. THE Workflow_Engine SHALL implement a CodeGeneration_Executor that invokes the ChatClientAgent to generate a PullRequest from a ResolutionReport.
6. WHEN an Executor completes, THE Workflow_Engine SHALL pass the Executor output as input to the next Executor via the workflow state context.

### Requirement 3: Replace Manual JSON Parsing with Structured Output

**User Story:** As a developer, I want LLM interactions to use ChatClientAgent with structured output schemas and function calling, so that response parsing is type-safe and eliminates fragile manual JSON deserialization.

#### Acceptance Criteria

1. THE Classification_Executor SHALL use ChatClientAgent with a structured output schema that maps directly to the ClassificationResult type.
2. THE Resolution_Executor SHALL use ChatClientAgent with a structured output schema that maps directly to the ResolutionReport type.
3. THE CodeGeneration_Executor SHALL use ChatClientAgent with a structured output schema that maps directly to the PullRequest type.
4. WHEN the ChatClientAgent returns a response, THE Executor SHALL receive a strongly-typed object without manual JSON parsing.
5. IF the ChatClientAgent fails to produce a valid structured response, THEN THE Executor SHALL return an error result that transitions the workflow to the Failed state.

### Requirement 4: Human-in-the-Loop Approval Gate

**User Story:** As a support team lead, I want a human approval step before code changes are generated, so that I can review the resolution and prevent undesirable automated code modifications.

#### Acceptance Criteria

1. WHEN the Resolution_Executor completes successfully, THE Workflow_Engine SHALL pause execution at the Human_Approval_Gate before proceeding to CodeGeneration.
2. WHILE the workflow is paused at the Human_Approval_Gate, THE Workflow_Engine SHALL emit a Workflow_Event indicating the workflow is awaiting approval.
3. WHEN a human approves the resolution, THE Workflow_Engine SHALL resume execution and traverse the Edge to the CodeGeneration_Executor.
4. WHEN a human rejects the resolution, THE Workflow_Engine SHALL transition the workflow to the ManualReviewRequired terminal state.
5. THE Workflow_Engine SHALL expose an API endpoint for submitting approval or rejection decisions for a specific workflow instance.
6. WHILE the workflow is paused at the Human_Approval_Gate, THE State_Tracker SHALL record the stage as a new AwaitingApproval value in the WorkflowStage enum.

### Requirement 5: Native Workflow Streaming Events

**User Story:** As a dashboard consumer, I want workflow state transitions emitted as native streaming events from the Workflow_Engine, so that the dashboard receives real-time updates without a custom intermediary channel.

#### Acceptance Criteria

1. WHEN the Workflow_Engine transitions between stages, THE Workflow_Engine SHALL emit a Workflow_Event containing the issue ID, new stage, timestamp, and detail.
2. THE Workflow_Engine SHALL replace the custom WorkflowUpdateChannel with native workflow event emission as the primary source of real-time updates.
3. THE Dashboard SHALL receive Workflow_Events via the existing gRPC server streaming endpoint without changes to the gRPC contract.
4. WHEN a workflow reaches a terminal state, THE Workflow_Engine SHALL emit a final Workflow_Event indicating completion.
5. THE Workflow_Engine SHALL support multiple concurrent subscribers to the Workflow_Event stream without message loss.

### Requirement 6: LLM Call Middleware for Logging and Telemetry

**User Story:** As an operations engineer, I want middleware on LLM calls that captures request/response metadata, so that I can monitor latency, token usage, and error rates for AI operations.

#### Acceptance Criteria

1. THE LLM_Middleware SHALL log the model name, prompt token count, completion token count, and total latency for each LLM call.
2. THE LLM_Middleware SHALL be registered in the DI pipeline as a delegating handler that wraps the ChatClientAgent.
3. WHEN an LLM call fails, THE LLM_Middleware SHALL log the error type, error message, and elapsed time before the failure is propagated.
4. THE LLM_Middleware SHALL not alter the request or response payload passing through the pipeline.
5. THE LLM_Middleware SHALL support structured logging compatible with the existing ILogger infrastructure.

### Requirement 7: Remove Akka.NET Dependency

**User Story:** As a developer, I want the Akka.NET packages and all actor-related code removed from the solution, so that the codebase has a single orchestration mechanism and reduced dependency surface.

#### Acceptance Criteria

1. THE Solution SHALL remove the Akka.NET, Akka.Hosting, and Akka.TestKit.Xunit2 NuGet package references from all projects.
2. THE Solution SHALL delete the SupervisorActor, AIAgentActor, and all files in the Actors directory.
3. THE Solution SHALL remove the ISupervisorActorBridge interface and its implementation from the codebase.
4. THE Solution SHALL remove all Akka.NET actor system configuration from Program.cs.
5. THE Solution SHALL remove the ActorMessages file and all Akka-specific message types from the Domain layer.
6. WHEN the migration is complete, THE Solution SHALL compile and pass all tests without any Akka.NET references.

### Requirement 8: Preserve Existing Behavioral Contracts

**User Story:** As a developer, I want the migration to preserve all existing workflow behavior and API contracts, so that the dashboard and API consumers continue to function without modification.

#### Acceptance Criteria

1. THE Workflow_Engine SHALL produce identical WorkflowState transitions for the same input as the current Orchestrator (excluding the new AwaitingApproval stage).
2. THE Workflow_Engine SHALL preserve the existing IWorkflowStateTracker dual-write semantics (update IssueEntity + create StateTransitionEvent).
3. THE Workflow_Engine SHALL preserve the existing REST API contract: all endpoints return the same response shapes and status codes.
4. THE Workflow_Engine SHALL preserve the existing gRPC streaming contract: WorkflowMonitor.SubscribeToUpdates continues to stream WorkflowState updates.
5. THE Workflow_Engine SHALL preserve the existing email validation rules: both Subject and Body must be non-empty and non-whitespace.
6. THE Workflow_Engine SHALL preserve the existing team routing logic: regex-based matching of "Application A" or "Application B" in subject and body text.
7. THE Workflow_Engine SHALL preserve the existing agent selection logic: deterministic IssueCategory-to-AgentRole mapping.

### Requirement 9: Workflow State Persistence and Recovery

**User Story:** As a developer, I want workflow execution state persisted so that paused workflows (e.g., awaiting human approval) survive application restarts.

#### Acceptance Criteria

1. WHEN the Workflow_Engine pauses at the Human_Approval_Gate, THE Workflow_Engine SHALL persist the workflow execution state to the database.
2. WHEN the application restarts, THE Workflow_Engine SHALL resume any workflows that were paused at the Human_Approval_Gate.
3. THE Workflow_Engine SHALL store sufficient context in the persisted state to resume execution without re-running completed Executors.
4. IF a persisted workflow cannot be resumed due to corrupted state, THEN THE Workflow_Engine SHALL transition the workflow to the Failed state with a descriptive error.

### Requirement 10: Dashboard — LLM Telemetry Display

**User Story:** As an operations engineer, I want the Agents page in the dashboard to display LLM token usage, latency, and call history per agent, so that I can monitor AI costs and performance in real time.

#### Acceptance Criteria

1. THE Dashboard Agents page SHALL display for each agent: total prompt tokens, total completion tokens, total LLM calls, and average latency.
2. THE Dashboard Agents page SHALL display the last LLM call details: model name, prompt tokens, completion tokens, latency, and success/failure status.
3. THE Backend SHALL expose a new `GET /api/support/agents/{agentId}/telemetry` endpoint that returns aggregated LLM telemetry for a specific agent.
4. THE Backend SHALL expose a new `GET /api/support/telemetry/summary` endpoint that returns global LLM usage statistics (total tokens, total calls, average latency, error rate).
5. THE Dashboard SHALL update telemetry data via periodic polling (every 5 seconds) or via gRPC streaming events.
6. THE Dashboard SHALL display a cost estimate based on token usage (configurable rate per 1K tokens in the UI).

### Requirement 11: Dashboard — Human Approval Interface

**User Story:** As a support team lead, I want an approval interface in the dashboard where I can review pending resolutions and approve or reject them, so that I can control which code changes are generated.

#### Acceptance Criteria

1. THE Dashboard SHALL display a list of workflows awaiting human approval, showing issue ID, subject, resolution summary, and time waiting.
2. THE Dashboard SHALL provide "Approve" and "Reject" buttons for each pending workflow.
3. WHEN the user clicks "Approve", THE Dashboard SHALL call the approval API endpoint and the workflow SHALL resume to CodeGeneration.
4. WHEN the user clicks "Reject", THE Dashboard SHALL call the rejection API endpoint and the workflow SHALL transition to ManualReviewRequired.
5. THE Dashboard SHALL display the full ResolutionReport (root cause, affected component, severity, proposed fix) for review before approval.
6. THE Dashboard SHALL update the pending approvals list in real time via gRPC streaming (new items appear, approved/rejected items disappear).

### Requirement 12: Dashboard — AwaitingApproval Stage Visualization

**User Story:** As a dashboard user, I want the pipeline visualizer and issues list to reflect the new AwaitingApproval stage, so that I can see which workflows are paused and waiting for human input.

#### Acceptance Criteria

1. THE Dashboard pipeline visualizer SHALL include an AwaitingApproval node between Resolved and CodeChangeGenerated in the graph.
2. THE Dashboard pipeline visualizer SHALL highlight the AwaitingApproval node with a distinct color (amber/yellow) when an issue is paused there.
3. THE Dashboard issues list SHALL display "AwaitingApproval" as a valid stage with an amber badge.
4. THE Dashboard WorkflowStage TypeScript type SHALL include 'AwaitingApproval' as a valid value.
5. THE Dashboard stats cards SHALL include a count of workflows awaiting approval.

### Requirement 13: Configuration and Dependency Registration

**User Story:** As a developer, I want the new workflow engine registered through the existing DI extension method pattern, so that the composition root remains clean and consistent with the current architecture.

#### Acceptance Criteria

1. THE Infrastructure layer SHALL expose an `AddWorkflowEngine()` extension method that registers the Workflow_Engine, all Executors, and the LLM_Middleware.
2. THE `AddWorkflowEngine()` method SHALL accept configuration from the existing `Workflow` section in appsettings.json.
3. THE Program.cs SHALL replace the Akka.NET actor system setup with a single call to `AddWorkflowEngine()`.
4. THE Workflow_Engine SHALL reuse the existing IChatClient registration for LLM connectivity.
5. THE Workflow_Engine SHALL support the existing configuration-driven team and agent definitions from WorkflowConfiguration.
