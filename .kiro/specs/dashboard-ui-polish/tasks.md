# Implementation Plan: Dashboard UI Polish

## Overview

This plan implements five coordinated improvements: real-time pipeline visualization, dead code cleanup, email templates, empty/error state handling with agent email visibility, and configurable sequential processing mode. Tasks are ordered to build incrementally — backend changes first (types, config, endpoints), then frontend types, then UI components, with testing woven in close to each implementation step.

## Tasks

- [x] 1. Backend: Add SequentialProcessing flag and ConfigEndpoints
  - [x] 1.1 Add `SequentialProcessing` property to `WorkflowConfiguration`
    - Add `public bool SequentialProcessing { get; set; }` to `src/AiSupportWorkflow.Application/Configuration/WorkflowConfiguration.cs`
    - Add `"SequentialProcessing": false` to the `Workflow` section in `src/AiSupportWorkflow.Presentation/appsettings.development.json`
    - _Requirements: 5.1, 5.6_

  - [x] 1.2 Implement sequential processing logic in `InboxProcessor`
    - Modify `ProcessPendingMessagesAsync` in `src/AiSupportWorkflow.Infrastructure/Services/InboxProcessor.cs`
    - Inject `IOptions<WorkflowConfiguration>` into the constructor
    - When `SequentialProcessing` is true: fetch only the first unprocessed message, check if the last processed issue has reached a terminal state before processing, skip if still in-flight
    - When false: retain existing parallel behavior (process all pending messages)
    - _Requirements: 5.2, 5.4_

  - [x] 1.3 Create `ConfigEndpoints` for exposing runtime configuration
    - Create `src/AiSupportWorkflow.Presentation/Endpoints/ConfigEndpoints.cs` implementing `IEndpoint`
    - Map `GET /api/support/config` returning `{ sequentialProcessing: boolean }`
    - Read from `IOptions<WorkflowConfiguration>`
    - _Requirements: 5.5_

  - [x] 1.4 Write unit tests for sequential processing logic
    - Test that when `SequentialProcessing` is true and a previous issue is non-terminal, no new message is processed
    - Test that when `SequentialProcessing` is true and no previous issue is in-flight, exactly one message is processed
    - Test that when `SequentialProcessing` is false, all pending messages are processed
    - _Requirements: 5.2, 5.4_

  - [x] 1.5 Write property test for sequential mode single-message processing
    - **Property 7: Sequential mode processes exactly one message per cycle**
    - **Validates: Requirements 5.2**

  - [x] 1.6 Write property test for parallel mode all-message processing
    - **Property 8: Parallel mode processes all pending messages in one cycle**
    - **Validates: Requirements 5.4**

- [x] 2. Backend: Extend AgentsEndpoints with current email info
  - [x] 2.1 Extend `AgentsEndpoints` response with `currentIssueId`, `currentSubject`, `currentStage`
    - Modify `src/AiSupportWorkflow.Presentation/Endpoints/AgentsEndpoints.cs`
    - Query `WorkflowDbContext` for non-terminal issues assigned to each agent (match by AgentId in issue detail or assigned agent field)
    - Return `CurrentIssueId`, `CurrentSubject`, `CurrentStage` as nullable fields in the response
    - _Requirements: 4.6, 4.7_

  - [x] 2.2 Write unit tests for extended AgentsEndpoints response
    - Test that a Working agent with an assigned issue returns currentIssueId, currentSubject, currentStage
    - Test that an Idle agent returns null for all current-email fields
    - _Requirements: 4.6, 4.7, 4.8_

- [x] 3. Checkpoint - Ensure all backend tests pass
  - Ensure all tests pass (`dotnet test AiSupportWorkflow.sln`), ask the user if questions arise.

- [x] 4. Frontend: gRPC-Web client integration and dead code cleanup
  - [x] 4.1 Extend `AgentStatus` type with current email fields
    - Modify `dashboard/src/types/index.ts`
    - Add `currentIssueId: string | null`, `currentSubject: string | null`, `currentStage: WorkflowStage | null` to the `AgentStatus` interface
    - _Requirements: 4.6, 4.7_

  - [x] 4.2 Set up proto code generation for the frontend
    - Add `@bufbuild/buf`, `@bufbuild/protoc-gen-es`, `@connectrpc/protoc-gen-connect-es` to devDependencies in `dashboard/package.json`
    - Create `dashboard/buf.gen.yaml` configuration for TypeScript code generation
    - Generate TypeScript client code from `src/AiSupportWorkflow.Presentation/Protos/workflow_monitor.proto` into `dashboard/src/gen/`
    - Add a `"generate"` script to `package.json`
    - _Requirements: 2.1, 2.2_

  - [x] 4.3 Rewrite `grpc-client.ts` to use real gRPC-Web streaming
    - Replace the polling-based implementation in `dashboard/src/api/grpc-client.ts`
    - Use `createGrpcWebTransport` from `@connectrpc/connect-web` to connect to the backend
    - Use `createClient` from `@connectrpc/connect` with the generated `WorkflowMonitor` service
    - Implement `subscribe()` using `for await` over the server stream
    - Implement auto-reconnect with exponential backoff on stream errors
    - Maintain the existing `GrpcStreamClient` interface (`subscribe`, `disconnect`, `isConnected`)
    - _Requirements: 2.1_

  - [x] 4.4 Remove dead code and unused exports
    - Remove any unused exports from hooks, components, or utilities
    - Verify no orphaned imports remain
    - _Requirements: 2.3_

  - [x] 4.5 Verify gRPC-Web integration passes type-check and tests
    - Run `npx tsc --noEmit` to confirm TypeScript compiles
    - Run `npx vitest --run` to confirm all existing tests pass
    - _Requirements: 2.4, 2.5_

- [x] 5. Frontend: Implement email templates module and EmailComposer integration
  - [x] 5.1 Create `emailTemplates.ts` module with 10 pre-defined templates
    - Create `dashboard/src/components/emailTemplates.ts`
    - Define `EmailTemplate` interface with `id`, `name`, `category`, `sender`, `subject`, `body`
    - Export `EMAIL_TEMPLATES` array with 10 templates grouped by category: Application A (A1 NullReferenceException, A2 blank total price, A3 missing test), Application B (B1 SQL Injection, B2 missing null check, B3 flaky test), Edge Cases (Out-of-Scope, Ambiguous Routing, Failed Routing, Empty Input Validation)
    - The empty input validation template must have empty strings for subject and body
    - _Requirements: 3.3, 3.4, 3.6_

  - [x] 5.2 Add template selector dropdown to `EmailComposer`
    - Modify `dashboard/src/components/EmailComposer.tsx`
    - Add a `<select>` element above the form fields with `<optgroup>` elements for each category
    - On template selection, set sender/subject/body state from the selected template values
    - User modifications after selection are naturally preserved by React controlled inputs
    - _Requirements: 3.1, 3.2, 3.5, 3.6_

  - [x] 5.3 Write property test for template selection fills all form fields
    - **Property 3: Template selection fills all form fields**
    - **Validates: Requirements 3.2**

  - [x] 5.4 Write property test for user modifications persist after template selection
    - **Property 4: User modifications persist after template selection**
    - **Validates: Requirements 3.5**

- [x] 6. Frontend: Implement PipelineVisualizer real-time activity
  - [x] 6.1 Modify `PipelineVisualizer` to accept `activeIssues` array
    - Modify `dashboard/src/components/PipelineVisualizer.tsx`
    - Change props from `selectedIssue?: WorkflowState` to `activeIssues: WorkflowState[]`
    - Render multiple activity indicators (one per active issue at its respective stage)
    - Display issueId and subject as a label below each active stage node
    - When no issues are active, render all nodes in neutral gray (idle state)
    - Maintain existing color conventions: green for completed, blue pulsing for active, gray for pending
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [x] 6.2 Update `OverviewPage` to pass active issues to PipelineVisualizer
    - Modify `dashboard/src/pages/OverviewPage.tsx`
    - Filter `latestStates` to non-terminal issues and pass as `activeIssues` prop
    - Remove the single `mostRecentIssue` selection logic
    - _Requirements: 1.4, 5.3_

  - [x] 6.3 Write property test for pipeline node color mapping
    - **Property 1: Pipeline node color mapping is consistent with stage position**
    - **Validates: Requirements 1.1, 1.6**

  - [x] 6.4 Write property test for multi-issue activity indicators
    - **Property 2: Multi-issue activity indicators reflect all active issues**
    - **Validates: Requirements 1.3, 1.4**

- [x] 7. Frontend: Implement empty/error states and agent email visibility
  - [x] 7.1 Modify `useAgents` hook to expose a `retry` function
    - Modify `dashboard/src/hooks/useAgents.ts`
    - Add a `retry` callback that re-triggers the fetch immediately
    - Continue exposing `agents`, `isLoading`, `error`
    - _Requirements: 4.4_

  - [x] 7.2 Add empty and error states to `AgentsPage`
    - Modify `dashboard/src/pages/AgentsPage.tsx`
    - When agents array is empty and not loading: show empty-state message indicating no agents configured, suggest enabling `EnableVisualization`
    - When error is present: show error message with HTTP status code and description, provide a retry button that calls the `retry` function
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 7.3 Enhance `AgentMonitor` with loading skeleton and current email info
    - Modify `dashboard/src/components/AgentMonitor.tsx`
    - Add skeleton/loading indicator (animated placeholder cards) when `isLoading` is true
    - When agent status is "Working" and `currentIssueId` is present: display issueId, currentSubject, and currentStage in the agent card
    - When agent is "Idle": show "No recent activity"
    - _Requirements: 4.5, 4.6, 4.7, 4.8_

  - [x] 7.4 Write property test for error display contains HTTP status code and message
    - **Property 5: Error display contains HTTP status code and message**
    - **Validates: Requirements 4.3**

  - [x] 7.5 Write property test for working agent card displays current email information
    - **Property 6: Working agent card displays current email information**
    - **Validates: Requirements 4.6, 4.7**

- [x] 8. Frontend: Implement useConfig hook and sequential mode badge
  - [x] 8.1 Create `useConfig` hook
    - Create `dashboard/src/hooks/useConfig.ts`
    - Fetch `GET /api/support/config` once on mount
    - Return `{ sequentialProcessing: boolean, isLoading: boolean }`
    - Default to `sequentialProcessing: false` on fetch failure
    - _Requirements: 5.5_

  - [x] 8.2 Add sequential mode badge to `OverviewPage`
    - Modify `dashboard/src/pages/OverviewPage.tsx`
    - Use the `useConfig` hook
    - When `sequentialProcessing` is true, display a "Sequential Mode" badge/label near the page header
    - _Requirements: 5.5_

  - [x] 8.3 Add `fetchConfig` function to API client
    - Modify `dashboard/src/api/client.ts`
    - Add `fetchConfig()` function that calls `GET /api/support/config` and returns `{ sequentialProcessing: boolean }`
    - _Requirements: 5.5_

- [x] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass (`dotnet test AiSupportWorkflow.sln` and `cd dashboard && npx vitest --run`), ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties (Properties 1–8 from design)
- Backend property tests use FsCheck.Xunit; frontend property tests use fast-check
- gRPC-Web integration (task 4.2–4.3) replaces the polling fallback with real server streaming using `@connectrpc/connect-web` and generated proto types
- The `useGrpcStream` hook interface remains unchanged — only the underlying transport changes from polling to gRPC-Web push
- The `useConfig` hook defaults to `false` on failure, so the badge is only shown when the backend confirms sequential mode is active
