# Implementation Plan: Workflow Engine Migration

## Overview

This plan migrates the orchestration layer from Akka.NET actors to Microsoft Agent Framework Workflows. Tasks are ordered to build foundational types first, then executors, then the workflow graph, then API/frontend, and finally cleanup of Akka code. Each task builds incrementally on the previous, ensuring no orphaned code.

## Tasks

- [ ] 1. Update packages and add domain types
  - [ ] 1.1 Update NuGet package references
    - Add `Microsoft.Agents.AI.Workflows` (1.3.0) to `AiSupportWorkflow.Infrastructure.csproj`
    - Remove `Akka.NET`, `Akka.Hosting` from `AiSupportWorkflow.Infrastructure.csproj` and `AiSupportWorkflow.Presentation.csproj`
    - Remove `Akka.TestKit.Xunit2` from test projects
    - Update `Directory.Packages.props` accordingly
    - _Requirements: 7.1, 13.4_

  - [ ] 1.2 Add AwaitingApproval to WorkflowStage enum
    - Add `AwaitingApproval` between `Resolved` and `CodeChangeGenerated` in `Domain/Enums/WorkflowStage.cs`
    - _Requirements: 4.6, 1.6_

  - [ ] 1.3 Create ApprovalDecision value object
    - Create `Domain/ValueObjects/ApprovalDecision.cs` as `public record ApprovalDecision(bool Approved, string? Reason = null)`
    - _Requirements: 4.3, 4.4_

  - [ ] 1.4 Create WorkflowCheckpoint persistence entity
    - Create `Infrastructure/Persistence/Entities/WorkflowCheckpoint.cs` with Id, IssueId, ExecutorId, SerializedState, PausedAt, ResumedAt, IsActive
    - Add `DbSet<WorkflowCheckpoint>` to `WorkflowDbContext`
    - Add EF Core entity configuration
    - _Requirements: 9.1, 9.3_

  - [ ] 1.5 Create LlmCallRecord persistence entity
    - Create `Infrastructure/Persistence/Entities/LlmCallRecord.cs` with Id, AgentId, ModelName, PromptTokens, CompletionTokens, LatencyMs, Success, ErrorMessage, Timestamp
    - Add `DbSet<LlmCallRecord>` to `WorkflowDbContext`
    - Add EF Core entity configuration
    - _Requirements: 6.1_

- [ ] 2. Implement LLM telemetry middleware and store
  - [ ] 2.1 Create LlmTelemetryStore
    - Create `Infrastructure/AgentFramework/LlmTelemetryStore.cs` with thread-safe in-memory store
    - Implement `Record(LlmCallEntry)`, `GetAgentTelemetry(agentId)`, `GetGlobalSummary()` methods
    - Define `LlmCallEntry` record type
    - _Requirements: 6.1, 10.3, 10.4_

  - [ ] 2.2 Create LlmTelemetryMiddleware (DelegatingChatClient)
    - Create `Infrastructure/AgentFramework/LlmTelemetryMiddleware.cs` extending `DelegatingChatClient`
    - Override `GetResponseAsync` to capture model name, prompt tokens, completion tokens, latency
    - Log success/failure via ILogger, record to LlmTelemetryStore
    - Ensure request/response payloads are not altered
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

  - [ ]* 2.3 Write property test for LLM Middleware Telemetry Capture
    - **Property 8: LLM Middleware Telemetry Capture**
    - **Validates: Requirements 6.1, 6.3**

  - [ ]* 2.4 Write property test for LLM Middleware Transparency
    - **Property 9: LLM Middleware Transparency**
    - **Validates: Requirements 6.4**

  - [ ]* 2.5 Write unit tests for LlmTelemetryMiddleware
    - Test successful call records correct token counts and latency
    - Test failed call records error message and elapsed time
    - Test that response object is returned unmodified
    - _Requirements: 6.1, 6.3, 6.4_

- [ ] 3. Implement ChatClientAgentFactory with structured output
  - [ ] 3.1 Create ChatClientAgentFactory
    - Create `Infrastructure/AgentFramework/ChatClientAgentFactory.cs`
    - Implement `CreateClassificationAgent(IChatClient)` with structured output schema for `ClassificationResult`
    - Implement `CreateResolutionAgent(IChatClient)` with structured output schema for `ResolutionReport`
    - Implement `CreateCodeGenAgent(IChatClient)` with structured output schema for `PullRequest`
    - Configure appropriate temperature settings per agent (0.1, 0.2, 0.5)
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [ ] 4. Implement workflow executors
  - [ ] 4.1 Create ClassificationExecutor
    - Create `Infrastructure/WorkflowEngine/Executors/ClassificationExecutor.cs`
    - Use ChatClientAgent with structured output to classify issues
    - Handle out-of-scope termination (yield output, prevent edge traversal)
    - Store issue in workflow state context for downstream executors
    - Call stateTracker.TransitionAsync for Received → Classified/ClassifiedOutOfScope
    - _Requirements: 2.1, 3.1, 3.4, 1.3_

  - [ ] 4.2 Create TeamAssignmentExecutor
    - Create `Infrastructure/WorkflowEngine/Executors/TeamAssignmentExecutor.cs`
    - Wrap existing `ITeamRouter.Route` deterministic logic
    - Read issue from workflow state context
    - Call stateTracker.TransitionAsync for TeamAssigned
    - _Requirements: 2.2, 8.6_

  - [ ] 4.3 Create AgentAssignmentExecutor
    - Create `Infrastructure/WorkflowEngine/Executors/AgentAssignmentExecutor.cs`
    - Wrap existing `IAgentSelector.Select` deterministic logic
    - Read classification from workflow state context
    - Call stateTracker.TransitionAsync for AgentAssigned
    - _Requirements: 2.3, 8.7_

  - [ ] 4.4 Create ResolutionExecutor
    - Create `Infrastructure/WorkflowEngine/Executors/ResolutionExecutor.cs`
    - Use ChatClientAgent with structured output for root cause analysis
    - Read issue and agent assignment from workflow state context
    - Call stateTracker.TransitionAsync for Resolving → Resolved
    - _Requirements: 2.4, 3.2, 3.4_

  - [ ] 4.5 Create CodeGenerationExecutor
    - Create `Infrastructure/WorkflowEngine/Executors/CodeGenerationExecutor.cs`
    - Use ChatClientAgent with structured output to generate PullRequest
    - Read resolution report from workflow state context
    - Call stateTracker.TransitionAsync for CodeChangeGenerated
    - Yield final workflow output
    - _Requirements: 2.5, 3.3, 3.4_

  - [ ]* 4.6 Write unit tests for all executors
    - Test ClassificationExecutor happy path (code-related) and out-of-scope path
    - Test TeamAssignmentExecutor with valid routing and routing failure
    - Test AgentAssignmentExecutor for each IssueCategory → AgentRole mapping
    - Test ResolutionExecutor happy path with mocked ChatClientAgent
    - Test CodeGenerationExecutor happy path with mocked ChatClientAgent
    - Test each executor throws on missing workflow state
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 3.5_

- [ ] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 6. Implement Human Approval Gate and workflow persistence
  - [ ] 6.1 Create HumanApprovalGate via RequestPort
    - Configure `RequestPort<ResolutionReport, ApprovalDecision>` named "HumanApprovalGate"
    - Integrate with stateTracker to transition to AwaitingApproval when paused
    - _Requirements: 4.1, 4.2, 4.6_

  - [ ] 6.2 Create WorkflowCheckpointStore
    - Create `Infrastructure/Persistence/WorkflowCheckpointStore.cs`
    - Implement `SaveCheckpointAsync` to persist workflow state on pause
    - Implement `GetActiveCheckpointsAsync` to retrieve paused workflows
    - Implement `MarkResumedAsync` to deactivate checkpoint on resume
    - Serialize workflow context (IssueRecord, ClassificationResult, TeamAssignment, AgentAssignment, ResolutionReport)
    - _Requirements: 9.1, 9.3_

  - [ ] 6.3 Create WorkflowApprovalService
    - Create `Infrastructure/Services/WorkflowApprovalService.cs`
    - Implement `GetPendingApprovalsAsync` returning workflows in AwaitingApproval state
    - Implement `ApproveAsync(issueId)` that sends ApprovalDecision(true) to the RequestPort and resumes workflow
    - Implement `RejectAsync(issueId, reason)` that sends ApprovalDecision(false) and transitions to ManualReviewRequired
    - Handle non-existent workflow (404) and wrong state (409 Conflict)
    - _Requirements: 4.3, 4.4, 4.5_

  - [ ]* 6.4 Write property test for Human Approval Gate Pauses Execution
    - **Property 4: Human Approval Gate Pauses Execution**
    - **Validates: Requirements 4.1, 4.6**

  - [ ]* 6.5 Write property test for Approval Resumes to Code Generation
    - **Property 5: Approval Resumes to Code Generation**
    - **Validates: Requirements 4.3**

  - [ ]* 6.6 Write property test for Rejection Terminates at ManualReviewRequired
    - **Property 6: Rejection Terminates at ManualReviewRequired**
    - **Validates: Requirements 4.4**

  - [ ]* 6.7 Write property test for Checkpoint Persistence on Pause
    - **Property 14: Checkpoint Persistence on Pause**
    - **Validates: Requirements 9.1, 9.3**

  - [ ]* 6.8 Write property test for Corrupted Checkpoint Recovery
    - **Property 15: Corrupted Checkpoint Recovery**
    - **Validates: Requirements 9.4**

- [ ] 7. Build SupportWorkflowFactory and wire the workflow graph
  - [ ] 7.1 Create SupportWorkflowFactory
    - Create `Infrastructure/WorkflowEngine/SupportWorkflowFactory.cs`
    - Inject all 5 executors and the approval RequestPort
    - Build workflow graph with WorkflowBuilder: Classification → TeamAssignment → AgentAssignment → Resolution → ApprovalPort → CodeGeneration
    - Add edge condition on Classification (IsCodeRelated=true) for main flow
    - Add edge condition on ApprovalPort (Approved=true) for code generation
    - Define terminal outputs from CodeGenerationExecutor
    - _Requirements: 1.1, 1.2, 1.3, 1.5, 1.6_

  - [ ] 7.2 Create new IOrchestrator implementation using workflow engine
    - Create `Infrastructure/WorkflowEngine/WorkflowOrchestrator.cs` implementing `IOrchestrator`
    - Execute workflow via `InProcessExecution.RunStreamingAsync`
    - Handle workflow events (completion, errors)
    - Catch unhandled exceptions and transition to Failed state
    - Preserve existing `ProcessIssueAsync` contract (same return type, same behavior)
    - _Requirements: 1.4, 2.6, 8.1, 8.2_

  - [ ]* 7.3 Write property test for Stage Ordering Invariant
    - **Property 1: Stage Ordering Invariant**
    - **Validates: Requirements 1.2, 1.6, 2.6**

  - [ ]* 7.4 Write property test for Out-of-Scope Conditional Termination
    - **Property 2: Out-of-Scope Conditional Termination**
    - **Validates: Requirements 1.3**

  - [ ]* 7.5 Write property test for Unhandled Exception Transitions to Failed
    - **Property 3: Unhandled Exception Transitions to Failed**
    - **Validates: Requirements 1.4, 3.5**

  - [ ]* 7.6 Write property test for Behavioral Equivalence
    - **Property 10: Behavioral Equivalence with Previous Orchestrator**
    - **Validates: Requirements 8.1**

  - [ ]* 7.7 Write property test for Dual-Write Persistence Invariant
    - **Property 11: Dual-Write Persistence Invariant**
    - **Validates: Requirements 8.2**

  - [ ]* 7.8 Write property test for Email Validation Preservation
    - **Property 12: Email Validation Preservation**
    - **Validates: Requirements 8.5**

  - [ ]* 7.9 Write property test for Deterministic Routing and Agent Selection
    - **Property 13: Deterministic Routing and Agent Selection**
    - **Validates: Requirements 8.6, 8.7**

- [ ] 8. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 9. Create DI registration and update Program.cs
  - [ ] 9.1 Create AddWorkflowEngine extension method
    - Create `Infrastructure/WorkflowEngine/WorkflowEngineServiceExtensions.cs`
    - Register LlmTelemetryStore, LlmTelemetryMiddleware (decorate IChatClient)
    - Register ChatClientAgents via ChatClientAgentFactory (keyed services for Resolution, CodeGen)
    - Register all 5 executors as singletons
    - Register RequestPort for Human Approval
    - Register SupportWorkflowFactory and built Workflow
    - Register WorkflowApprovalService, WorkflowCheckpointStore
    - Accept configuration from existing `Workflow` section
    - _Requirements: 13.1, 13.2, 13.4, 13.5_

  - [ ] 9.2 Update Program.cs to use AddWorkflowEngine
    - Remove all Akka.NET actor system configuration (`AddAkka` block)
    - Remove `ISupervisorActorBridge` and `AgentStatusProvider` registrations
    - Replace `IOrchestrator` registration to use new `WorkflowOrchestrator`
    - Add single call to `AddWorkflowEngine(builder.Configuration)`
    - Keep existing services: EmailProcessor, TeamRouter, AgentSelector, ProcessSupportEmailUseCase
    - _Requirements: 13.3, 7.4_

  - [ ] 9.3 Update InboxProcessor to use new workflow engine
    - Ensure InboxProcessor calls the new IOrchestrator implementation
    - Verify no references to Akka actors remain in the processing pipeline
    - _Requirements: 8.1, 8.3_

- [ ] 10. Create new API endpoints
  - [ ] 10.1 Create ApprovalEndpoints
    - Create `Presentation/Endpoints/ApprovalEndpoints.cs` implementing `IEndpoint`
    - `GET /api/support/approvals/pending` — list workflows awaiting approval
    - `POST /api/support/approvals/{issueId}/approve` — approve a workflow
    - `POST /api/support/approvals/{issueId}/reject` — reject a workflow with optional reason
    - Return appropriate HTTP status codes (204, 404, 409)
    - _Requirements: 4.5, 11.3, 11.4_

  - [ ] 10.2 Create TelemetryEndpoints
    - Create `Presentation/Endpoints/TelemetryEndpoints.cs` implementing `IEndpoint`
    - `GET /api/support/agents/{agentId}/telemetry` — agent-specific LLM telemetry
    - `GET /api/support/telemetry/summary` — global LLM usage statistics
    - _Requirements: 10.3, 10.4_

  - [ ]* 10.3 Write unit tests for API endpoints
    - Test approval endpoints return correct status codes
    - Test telemetry endpoints return expected response shapes
    - Test 404 for non-existent workflow approval
    - Test 409 for workflow not in AwaitingApproval state
    - _Requirements: 4.5, 8.3, 10.3, 10.4_

- [ ] 11. Implement workflow event streaming
  - [ ] 11.1 Create WorkflowEventBridge
    - Create `Infrastructure/WorkflowEngine/WorkflowEventBridge.cs`
    - Bridge native WorkflowEvent instances to existing WorkflowUpdateChannel
    - Handle ExecutorCompletedEvent, WorkflowOutputEvent, RequestInfoEvent
    - Preserve existing gRPC streaming contract (no changes to proto or WorkflowMonitorService)
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [ ]* 11.2 Write property test for Workflow Event Completeness
    - **Property 7: Workflow Event Completeness**
    - **Validates: Requirements 5.1, 5.4**

- [ ] 12. Delete Akka.NET actors and related code
  - [ ] 12.1 Delete Akka actor files and interfaces
    - Delete `Infrastructure/Actors/SupervisorActor.cs`
    - Delete `Infrastructure/Actors/AIAgentActor.cs`
    - Delete `Domain/Interfaces/ISupervisorActorBridge.cs`
    - Delete `Domain/Messages/ActorMessages.cs`
    - Delete `Presentation/SupervisorActorBridge.cs` (or equivalent bridge implementation)
    - Delete `Presentation/AgentStatusProvider.cs` (replaced by telemetry-based status)
    - Remove Actors directory if empty
    - Remove any remaining Akka `using` statements across the solution
    - _Requirements: 7.2, 7.3, 7.4, 7.5, 7.6_

  - [ ] 12.2 Remove old service implementations replaced by executors
    - Remove `IssueClassifierService.cs` (replaced by ClassificationExecutor)
    - Remove `BugResolverService.cs` (replaced by ResolutionExecutor)
    - Remove `CodeChangeGeneratorService.cs` (replaced by CodeGenerationExecutor)
    - Remove corresponding domain interfaces if no longer needed (`IIssueClassifier`, `IBugResolver`, `ICodeChangeGenerator`)
    - Update `InfrastructureServiceExtensions.cs` to remove old registrations
    - _Requirements: 7.2, 7.6_

  - [ ] 12.3 Remove old Orchestrator
    - Delete `Application/Services/Orchestrator.cs`
    - Verify `IOrchestrator` interface is now implemented only by `WorkflowOrchestrator`
    - _Requirements: 7.6, 8.1_

- [ ] 13. Checkpoint - Ensure backend compiles and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 14. Frontend — Update types and pipeline visualizer
  - [ ] 14.1 Update WorkflowStage TypeScript type
    - Add `'AwaitingApproval'` to the `WorkflowStage` union type in `dashboard/src/types/index.ts`
    - Add `AgentTelemetry`, `LlmCallDetail`, `TelemetrySummary`, `PendingApproval` interfaces
    - _Requirements: 12.4, 10.1, 10.2, 11.1_

  - [ ] 14.2 Update pipeline visualizer for AwaitingApproval
    - Add AwaitingApproval node between Resolved and CodeChangeGenerated in `PipelineVisualizer`
    - Style AwaitingApproval node with amber/yellow color when active
    - Update stage badge utility to include amber badge for AwaitingApproval
    - _Requirements: 12.1, 12.2, 12.3_

  - [ ] 14.3 Update OverviewPage stats to include awaiting approval count
    - Add a stats card showing count of workflows in AwaitingApproval stage
    - _Requirements: 12.5_

- [ ] 15. Frontend — Create Approvals page
  - [ ] 15.1 Add API client functions for approvals
    - Add `fetchPendingApprovals()` → `GET /api/support/approvals/pending`
    - Add `approveWorkflow(issueId)` → `POST /api/support/approvals/{issueId}/approve`
    - Add `rejectWorkflow(issueId, reason?)` → `POST /api/support/approvals/{issueId}/reject`
    - _Requirements: 11.3, 11.4_

  - [ ] 15.2 Create ApprovalsPage component
    - Create `dashboard/src/pages/ApprovalsPage.tsx`
    - Display list of pending approvals with issue ID, subject, resolution summary, time waiting
    - Show full ResolutionReport details (root cause, affected component, severity, proposed fix)
    - Add "Approve" and "Reject" buttons for each pending workflow
    - Update list in real time via gRPC streaming (new items appear, resolved items disappear)
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6_

  - [ ] 15.3 Add ApprovalsPage to router and navigation
    - Add route for `/approvals` in the app router
    - Add navigation link in the sidebar/header
    - _Requirements: 11.1_

- [ ] 16. Frontend — Add telemetry display to Agents page
  - [ ] 16.1 Add API client functions for telemetry
    - Add `fetchAgentTelemetry(agentId)` → `GET /api/support/agents/{agentId}/telemetry`
    - Add `fetchTelemetrySummary()` → `GET /api/support/telemetry/summary`
    - _Requirements: 10.3, 10.4_

  - [ ] 16.2 Update AgentsPage with telemetry display
    - Display per-agent telemetry: total prompt tokens, completion tokens, total calls, average latency
    - Display last LLM call details: model name, tokens, latency, success/failure
    - Add cost estimate display with configurable rate per 1K tokens
    - Poll telemetry data every 5 seconds
    - _Requirements: 10.1, 10.2, 10.5, 10.6_

  - [ ]* 16.3 Write frontend property test for cost estimate calculation
    - **Property 16: Cost Estimate Calculation**
    - Test that `calculateCost(tokens, rate)` equals `(tokens / 1000) * rate` rounded to 2 decimal places
    - Use fast-check with non-negative token counts and rates
    - **Validates: Requirements 10.6**

- [ ] 17. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.
  - Verify solution compiles without Akka.NET references
  - Verify frontend builds without errors (`npx tsc --noEmit`)

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The Akka deletion (task 12) is placed after the new workflow engine is fully wired to avoid breaking the build mid-migration
- Frontend tasks are independent of backend deletion and can be parallelized
