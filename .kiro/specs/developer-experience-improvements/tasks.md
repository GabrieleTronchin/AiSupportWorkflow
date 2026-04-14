# Implementation Plan: Developer Experience Improvements

## Overview

Implement six developer experience improvements across the Application, Presentation, and configuration layers, plus a new PowerShell script and documentation updates. Changes follow the existing Clean Architecture boundaries with no new projects or dependencies. Tasks are ordered so each builds on the previous, starting with configuration, then Orchestrator refactoring, endpoint tagging, the PowerShell script, and finally documentation.

## Tasks

- [x] 1. Add configurable actor ask timeout to WorkflowConfiguration and Orchestrator
  - [x] 1.1 Add `ActorAskTimeoutSeconds` property to `WorkflowConfiguration`
    - Add `public int ActorAskTimeoutSeconds { get; set; } = 120;` to `src/AiSupportWorkflow.Application/Configuration/WorkflowConfiguration.cs`
    - _Requirements: 1.1_

  - [x] 1.2 Replace hardcoded timeout in Orchestrator with config-driven value
    - Add a private `GetActorAskTimeout()` helper method that reads `ActorAskTimeoutSeconds` from config, falling back to 120 if value ≤ 0
    - Modify `ResolveWithActorAsync` to call `GetActorAskTimeout()` instead of `TimeSpan.FromMinutes(2)`
    - _Requirements: 1.3, 1.6_

  - [x] 1.3 Update `appsettings.Development.json` with timeout and logging overrides
    - Add `"Workflow": { "ActorAskTimeoutSeconds": 600 }` to `src/AiSupportWorkflow.Presentation/appsettings.Development.json`
    - Add `"Logging": { "LogLevel": { "AiSupportWorkflow": "Debug" } }` to the same file
    - Verify `appsettings.json` does NOT include `ActorAskTimeoutSeconds` (preserving 120-second default)
    - _Requirements: 1.4, 1.5, 5.4, 5.5_

  - [x] 1.4 Write property test for configurable timeout fallback rule
    - **Property 1: Configurable timeout respects fallback rule**
    - Generate random integers (positive, zero, negative), configure `WorkflowConfiguration`, mock `ISupervisorActorBridge`, run Orchestrator, capture the `TimeSpan` argument passed to `AssignIssueAsync`
    - Add test to `tests/AiSupportWorkflow.PropertyTests/` (e.g., `TimeoutProperties.cs`)
    - **Validates: Requirements 1.3, 1.6**

  - [x] 1.5 Write unit tests for timeout configuration
    - Test that `WorkflowConfiguration.ActorAskTimeoutSeconds` defaults to 120
    - Test that Orchestrator uses configured timeout value
    - Test that Orchestrator falls back to 120 when value is 0 or negative
    - Add tests to `tests/AiSupportWorkflow.UnitTests/OrchestratorTests.cs`
    - _Requirements: 1.1, 1.3, 1.6_

- [x] 2. Decouple logging from visualization flag and add structured decision logs
  - [x] 2.1 Remove `IsVisualizationEnabled` guard from Orchestrator logging
    - Remove the `private bool IsVisualizationEnabled` property from `Orchestrator`
    - Remove the `if (!IsVisualizationEnabled) return;` guard from `LogClassificationDecision`, `LogTeamAssignmentDecision`, and `LogAgentSelectionDecision`
    - Ensure all three log methods emit unconditionally
    - _Requirements: 4.1, 4.2, 4.6_

  - [x] 2.2 Add structured properties to decision log entries
    - Update `LogClassificationDecision` to include `IssueId`, `Category`, `ConfidenceScore`, `IsCodeRelated`, and `Reasoning` as structured log properties
    - Update `LogTeamAssignmentDecision` to include `IssueId`, `TeamName`, and `ApplicationName` as structured log properties
    - Update `LogAgentSelectionDecision` to include `IssueId`, `AgentId`, and `Role` as structured log properties
    - Use structured logging placeholders (`{PropertyName}`), not string interpolation
    - _Requirements: 4.3, 4.4, 4.5_

  - [x] 2.3 Write property test for decision log structured properties
    - **Property 2: Decision logs include all required structured properties**
    - Generate random `ClassificationResult`, `TeamAssignment`, `AgentAssignment` values; mock all dependencies; capture `ILogger` calls; verify structured property names are present
    - Add test to `tests/AiSupportWorkflow.PropertyTests/` (e.g., `LoggingProperties.cs`)
    - **Validates: Requirements 4.3, 4.4, 4.5**

  - [x] 2.4 Write unit tests for unconditional logging
    - Test that Orchestrator logs classification, team assignment, and agent selection when `EnableVisualization` is false
    - Test that Orchestrator logs classification, team assignment, and agent selection when `EnableVisualization` is true
    - Test via reflection that `Orchestrator` no longer has an `IsVisualizationEnabled` property
    - Add tests to `tests/AiSupportWorkflow.UnitTests/OrchestratorTests.cs`
    - _Requirements: 4.1, 4.6_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Add structured verbose logging at workflow stage transitions
  - [x] 4.1 Add Debug-level structured log entries at each stage transition in Orchestrator
    - Wrap each `stateTracker.Transition(...)` call with a structured `Debug`-level log entry containing `IssueId`, source `WorkflowStage`, target `WorkflowStage`, and transition detail string
    - For the `Received` stage, include `Sender` and `Subject` as additional structured properties
    - For the `Failed` stage, emit at `Warning` level instead of `Debug`, including the failure reason
    - Use `LoggerMessage` source-generated pattern or structured logging placeholders — no string interpolation
    - _Requirements: 5.1, 5.2, 5.3, 5.6_

  - [x] 4.2 Write property test for stage transition Debug-level logs
    - **Property 3: Stage transition logs are emitted at Debug level**
    - Generate random workflow paths (varying email content, classification results); capture all `ILogger` calls; verify each non-Failed transition has a Debug-level entry with `IssueId`, source stage, target stage, and detail
    - Add test to `tests/AiSupportWorkflow.PropertyTests/LoggingProperties.cs`
    - **Validates: Requirements 5.1**

  - [x] 4.3 Write property test for Received stage email metadata
    - **Property 4: Received stage log includes email metadata**
    - Generate random `IncomingEmail` with arbitrary `Sender` and `Subject` strings; verify the Received stage log includes both values as structured properties at Debug level
    - Add test to `tests/AiSupportWorkflow.PropertyTests/LoggingProperties.cs`
    - **Validates: Requirements 5.2**

  - [x] 4.4 Write property test for Failed stage Warning level
    - **Property 5: Failed stage logs at Warning level**
    - Generate random exceptions/failure scenarios; verify the Failed transition log is at `Warning` level (not Debug) and includes the failure reason
    - Add test to `tests/AiSupportWorkflow.PropertyTests/LoggingProperties.cs`
    - **Validates: Requirements 5.3**

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Mark visualization endpoints as frontend-dedicated
  - [x] 6.1 Add Frontend tag and summaries to VisualizationEndpoints
    - In `src/AiSupportWorkflow.Presentation/Endpoints/VisualizationEndpoints.cs`, change `.WithTags("Visualization")` to `.WithTags("Visualization", "Frontend")`
    - Add `.WithSummary("Frontend-dedicated: SSE stream of workflow state updates")` to the `/stream` endpoint
    - Add `.WithSummary("Frontend-dedicated: Current state of all AI agents")` to the `/agents` endpoint
    - Do not change the `EnableVisualization` guard behavior — endpoints still return 404 when disabled
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 6.2 Write unit tests for endpoint tags and summaries
    - Test that both endpoints have "Visualization" and "Frontend" tags
    - Test that endpoint summaries match expected strings
    - Test that endpoints still return 404 when `EnableVisualization` is false
    - Add tests to `tests/AiSupportWorkflow.UnitTests/EndpointTests.cs`
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

- [x] 7. Create PowerShell SSE monitor script
  - [x] 7.1 Create `scripts/Monitor-Workflow.ps1`
    - Create the file at `scripts/Monitor-Workflow.ps1` in the repository root
    - Add `[CmdletBinding()]` and `param()` block with `-BaseUrl` (string, default `http://localhost:5080`) and `-Agents` (switch)
    - Implement `-Agents` mode: `Invoke-RestMethod` to `{BaseUrl}/api/support/agents`, format as table, exit
    - Implement SSE streaming mode: connect to `{BaseUrl}/api/support/stream`, parse `data:` lines, print with timestamp
    - Handle 404 responses with "visualization disabled" message suggesting enabling it in appsettings
    - Handle connection failures with error message including attempted URL, exit code 1
    - Support graceful Ctrl+C termination via `try/finally` for HTTP stream cleanup
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

- [ ] 8. Update documentation
  - [ ] 8.1 Update README.md
    - Document `Workflow:ActorAskTimeoutSeconds` configuration option in the Configuration section
    - Add a section describing `scripts/Monitor-Workflow.ps1`, its parameters (`-BaseUrl`, `-Agents`), and usage examples
    - Update the API Endpoints table to mark `/api/support/stream` and `/api/support/agents` as frontend-dedicated
    - Update Key Behavioral Constraints to state that workflow decision logging is always active, independent of `EnableVisualization`
    - Document how to enable verbose logging by setting `AiSupportWorkflow` log level to `Debug` in `appsettings.Development.json`
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

  - [ ] 8.2 Update `docs/actor-architecture.md`
    - Update the ISupervisorActorBridge section to reference the configurable timeout from `WorkflowConfiguration` instead of the hardcoded 2-minute value
    - _Requirements: 6.6_

- [ ] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation after major changes
- Property tests validate the 5 correctness properties from the design document using FsCheck in `tests/AiSupportWorkflow.PropertyTests/`
- Unit tests use xUnit + NSubstitute in `tests/AiSupportWorkflow.UnitTests/`
- `docs/index.md`, `docs/clean-architecture.md`, and `docs/semantic-kernel-integration.md` remain unchanged (Requirements 6.7, 6.8, 6.9)
