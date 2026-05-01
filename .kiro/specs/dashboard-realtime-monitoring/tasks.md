# Implementation Plan: Dashboard Real-Time Monitoring

## Overview

Incremental implementation of improvements to the AI Support workflow monitoring dashboard. The task order follows architectural dependencies: first the persistence layer (foundation), then backend services, and finally the frontend.

**Languages:** C# (.NET 10) for the backend, TypeScript/React for the frontend.

## Tasks

- [ ] 1. EF Core InMemory Persistence Layer
  - [ ] 1.1 Create EF Core entities (IssueEntity, StateTransitionEvent, InboxMessage)
    - Create `IssueEntity` with properties: Id (Guid), CurrentStage (WorkflowStage), LastUpdated (DateTimeOffset), Detail (string?)
    - Create `StateTransitionEvent` with properties: Id (Guid), IssueId (Guid), PreviousStage (WorkflowStage?), NewStage (WorkflowStage), Timestamp (DateTimeOffset), Detail (string?)
    - Create `InboxMessage` with properties: Id (Guid), MessageType (string), Payload (string), ReceivedAt (DateTimeOffset), ProcessedAt (DateTimeOffset?), Error (string?)
    - Place entities in `src/AiSupportWorkflow.Infrastructure/Persistence/Entities/`
    - _Requirements: 9.2, 9.3, 9.4, 7.2_

  - [ ] 1.2 Create IEntityTypeConfiguration<T> configurations
    - Create `IssueEntityConfiguration`: primary key on Id, enum→string conversion for CurrentStage, index on CurrentStage
    - Create `StateTransitionEventConfiguration`: primary key on Id, indexes on IssueId and Timestamp, enum→string conversion for PreviousStage and NewStage
    - Create `InboxMessageConfiguration`: primary key on Id, indexes on ReceivedAt and ProcessedAt
    - Place in `src/AiSupportWorkflow.Infrastructure/Persistence/Configurations/`
    - _Requirements: 9.8_

  - [ ] 1.3 Create WorkflowDbContext with DbSet and DI registration
    - Create `WorkflowDbContext` with DbSet<IssueEntity>, DbSet<StateTransitionEvent>, DbSet<InboxMessage>
    - Apply configurations via `OnModelCreating`
    - Create `AddPersistence()` extension method that registers the DbContext with InMemory provider and `IWorkflowStateTracker`
    - Place in `src/AiSupportWorkflow.Infrastructure/Persistence/`
    - _Requirements: 9.1, 9.5_

  - [ ] 1.4 Implement EfWorkflowStateTracker
    - Create `EfWorkflowStateTracker` implementing `IWorkflowStateTracker`
    - Implement `TransitionAsync`: update/create IssueEntity + create StateTransitionEvent (dual-write)
    - Implement `GetStateAsync`, `GetAllStatesAsync`, `GetEventsAsync(limit=200)`
    - Replace the `ConcurrentDictionary` in the old `WorkflowStateTracker`
    - Place in `src/AiSupportWorkflow.Infrastructure/Persistence/`
    - _Requirements: 9.6, 9.7, 4.7_

  - [ ] 1.5 Write property test for dual-write invariant (Property 8)
    - **Property 8: State transition dual-write invariant**
    - For any state transition, verify that both the IssueEntity record and the StateTransitionEvent record are created/updated correctly
    - Use FsCheck to generate random issueId and stage transitions
    - **Validates: Requirements 4.7, 9.6**

  - [ ] 1.6 Write unit tests for EfWorkflowStateTracker
    - Test: TransitionAsync creates IssueEntity if it does not exist
    - Test: TransitionAsync updates existing IssueEntity
    - Test: GetEventsAsync respects the limit of 200
    - Test: GetAllStatesAsync returns all issues
    - _Requirements: 9.6, 9.7_

- [ ] 2. Checkpoint — Verify all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 3. Transactional Inbox (Backend)
  - [ ] 3.1 Implement InboxProcessor as IHostedService
    - Create `InboxProcessor` in `src/AiSupportWorkflow.Infrastructure/Services/`
    - Implement polling loop with configurable `Task.Delay`
    - Query unprocessed messages ordered by ReceivedAt (FIFO)
    - For each message: deserialize payload, start workflow, update ProcessedAt
    - On error: record in the Error field, set ProcessedAt to prevent infinite retries
    - Register in DI as `IHostedService`
    - _Requirements: 7.3, 7.4, 7.5, 7.6, 7.7_

  - [ ] 3.2 Write property test for FIFO processing order (Property 13)
    - **Property 13: Inbox FIFO processing order**
    - For any set of unprocessed InboxMessages with distinct timestamps, verify they are processed in ascending ReceivedAt order
    - Use FsCheck to generate messages with random timestamps
    - **Validates: Requirements 7.6**

  - [ ] 3.3 Write property test for failure handling (Property 12)
    - **Property 12: Inbox processing failure records error**
    - For any InboxMessage whose processing throws an exception, verify that Error is set and ProcessedAt is non-null
    - Use FsCheck to generate messages that cause exceptions
    - **Validates: Requirements 7.5**

  - [ ] 3.4 Write unit tests for InboxProcessor
    - Test: polling interval configurable from appsettings
    - Test: successfully processed message → ProcessedAt set, Error null
    - Test: failed message → Error set, ProcessedAt set
    - Test: messages processed in FIFO order
    - _Requirements: 7.3, 7.4, 7.5, 7.6, 7.7_

- [ ] 4. gRPC Service and REST Endpoints (Backend)
  - [ ] 4.1 Define the Protobuf file and configure the project for gRPC
    - Create `Protos/workflow_monitor.proto` with service `WorkflowMonitor`, RPC `SubscribeToUpdates`, messages `SubscribeRequest` and `WorkflowStateUpdate`
    - Add NuGet packages: `Grpc.AspNetCore`, `Grpc.AspNetCore.Web`
    - Configure the `.csproj` for Protobuf code generation
    - _Requirements: 6.1, 6.2_

  - [ ] 4.2 Implement WorkflowMonitorService (gRPC server streaming)
    - Create `WorkflowMonitorService` in `src/AiSupportWorkflow.Presentation/Services/`
    - Implement `SubscribeToUpdates` with server streaming
    - Support CancellationToken for client disconnection
    - Integrate with `EfWorkflowStateTracker` to receive state change notifications
    - Implement notification mechanism (Channel<T> or event) for real-time push
    - Configure enable/disable via appsettings (`EnableVisualization` flag)
    - _Requirements: 6.1, 6.3, 6.7, 6.8_

  - [ ] 4.3 Write property test for gRPC notification (Property 10)
    - **Property 10: gRPC notification on state transition**
    - For any state transition, verify that the gRPC stream emits a WorkflowStateUpdate with correct issueId, stage, timestamp, and detail
    - Use FsCheck to generate random state transitions
    - **Validates: Requirements 6.3**

  - [ ] 4.4 Implement new REST endpoints
    - Create endpoint `GET /api/support/events` that returns the last 200 events from the Events_Table
    - Create endpoint `GET /api/support/inbox` that returns inbox messages with optional status filter
    - Modify endpoint `POST /api/support/emails` to save to inbox and return HTTP 202 Accepted with messageId
    - Create endpoint `GET /api/support/agents` that returns configured agents with Idle/Working status
    - _Requirements: 4.5, 7.1, 8.1, 3.1_

  - [ ] 4.5 Write property test for inbox creation round-trip (Property 11)
    - **Property 11: Inbox message creation round-trip**
    - For any valid email, verify that POST `/api/support/emails` creates an InboxMessage with all correct fields and returns HTTP 202
    - Use FsCheck to generate random valid emails
    - **Validates: Requirements 7.1, 7.2**

  - [ ] 4.6 Write unit tests for REST endpoints and gRPC
    - Test: GET /api/support/events returns max 200 events in reverse chronological order
    - Test: GET /api/support/inbox with status filter
    - Test: POST /api/support/emails returns 202 Accepted
    - Test: GET /api/support/agents returns configured agents
    - Test: WorkflowMonitorService handles CancellationToken
    - _Requirements: 4.5, 7.1, 8.1, 3.1, 6.7_

- [ ] 5. DI Registration, Backend Wiring, and Dead Code Cleanup
  - [ ] 5.1 Update Program.cs and DI registration
    - Call `AddPersistence()` in the service setup
    - Register `InboxProcessor` as `IHostedService`
    - Configure gRPC services and gRPC-Web middleware
    - Map the gRPC service and new REST endpoints
    - Update `IWorkflowStateTracker` interface in Domain (async methods)
    - _Requirements: 9.5, 6.8, 7.3_

  - [ ] 5.2 Reorganize backend endpoint files
    - Rename/restructure `VisualizationEndpoints.cs` → extract `/agents` into a new `AgentsEndpoints.cs`
    - Create `InboxEndpoints.cs` for `GET /api/support/inbox`
    - Move `GET /api/support/events` to `WorkflowStatusEndpoints.cs` (consistent with issues)
    - Final structure:
      - `SupportEmailEndpoints.cs` → POST /api/support/emails (202 + inbox)
      - `WorkflowStatusEndpoints.cs` → GET /issues, GET /issues/{id}, GET /events
      - `AgentsEndpoints.cs` → GET /agents (configured agents from appsettings)
      - `InboxEndpoints.cs` → GET /inbox (with status filter)
    - _Requirements: 3.1, 4.5, 7.1, 8.1_

  - [ ] 5.3 Remove dead backend code and update documentation
    - Remove the SSE endpoint `GET /api/support/stream` from `VisualizationEndpoints.cs`
    - Delete the `VisualizationEndpoints.cs` file (after extracting /agents)
    - Delete the old `WorkflowStateTracker` (ConcurrentDictionary-based) in `src/AiSupportWorkflow.Infrastructure/Services/`
    - Remove the DI registration of the old `WorkflowStateTracker` from `InfrastructureServiceExtensions.cs`
    - Verify that no other files reference the deleted components
    - Update `docs/api-endpoints.md` to reflect the new API surface (remove /stream, add /events and /inbox, update /emails and /agents)
    - Update `src/AiSupportWorkflow.Presentation/AiSupportWorkflow.Presentation.http`:
      - Remove the `GET /api/support/stream` request (SSE removed)
      - Add `GET /api/support/events` request (persistent events list)
      - Add `GET /api/support/inbox` request (inbox messages list)
      - Add `GET /api/support/inbox?status=queued` request (status filter)
      - Update the comment on the `POST /api/support/emails` response (now returns 202 Accepted)

- [ ] 6. Checkpoint — Verify backend build and tests
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 7. Frontend — Types and gRPC-Web Client
  - [ ] 7.1 Update TypeScript types
    - Add `StateTransitionEvent` interface (id, issueId, previousStage, newStage, timestamp, detail)
    - Add `InboxMessage` interface (id, sender, subject, status, receivedAt, processedAt, error)
    - Add `InboxStats` interface (queued, processed, failed)
    - Update existing types if necessary
    - _Requirements: 4.6, 8.2_

  - [ ] 7.2 Create the gRPC-Web client (replaces SSE)
    - Install dependencies: `@connectrpc/connect`, `@connectrpc/connect-web`, `@bufbuild/protobuf`
    - Generate TypeScript code from the `.proto` file
    - Create `dashboard/src/api/grpc-client.ts` with GrpcStreamClient interface
    - Implement subscribe, disconnect, auto-reconnect with exponential backoff
    - Create hook `useGrpcStream.ts` that replaces `useSSE.ts`
    - _Requirements: 6.4, 6.5, 6.6_

  - [ ] 7.3 Write unit tests for gRPC-Web client
    - Test: automatic reconnection after disconnection
    - Test: disconnected status indicator
    - Test: receiving WorkflowState updates
    - _Requirements: 6.5, 6.6_

- [ ] 8. Frontend — PipelineVisualizer (Fixed Graph + Animations)
  - [ ] 8.1 Disable interactions on PipelineVisualizer
    - Add props: `panOnDrag={false}`, `zoomOnScroll={false}`, `zoomOnPinch={false}`, `zoomOnDoubleClick={false}`, `elementsSelectable={false}`, `preventScrolling={false}`
    - Add `fitView` for automatic container fitting
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [ ] 8.2 Implement pipeline graph animations
    - Add CSS for pulsing effect on the Active_Stage node
    - Color completed stage nodes green
    - Add `animated: true` on edges between completed stages and current stage
    - Color terminal error nodes red (Failed, ClassifiedOutOfScope, ManualReviewRequired)
    - Color all nodes green on successful completion (CodeChangeGenerated)
    - Inactive state: all nodes gray without animations
    - Automatic selection of the most recent issue from the gRPC stream
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7_

  - [ ] 8.3 Write property test for pipeline visualization (Property 9)
    - **Property 9: Pipeline visualization state correctness**
    - For any active stage in the main flow, verify that the active node has a pulsing effect, preceding nodes are green, edges are animated, and terminal error nodes are red
    - Use fast-check to generate random WorkflowStage
    - **Validates: Requirements 5.1, 5.2, 5.3, 5.4**

  - [ ] 8.4 Write unit tests for PipelineVisualizer
    - Test: ReactFlow props disable pan, zoom, selection, scroll
    - Test: fitView active
    - Test: idle state → all nodes gray, no animations
    - Test: final success → all nodes green
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 5.6_

- [ ] 9. Frontend — Overview Page (Integrated Layout)
  - [ ] 9.1 Restructure the Overview Page layout
    - Two-column grid layout: PipelineVisualizer (left) + EmailComposer (right)
    - Maintain summary statistics (Total Issues, Active Agents, Recent Failures) at the top
    - Both components visible simultaneously without scrolling
    - Integrate with `useGrpcStream` for real-time graph updates
    - Auto-selection of the most recent issue for the Pipeline_Graph
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [ ] 9.2 Write unit tests for Overview Page
    - Test: EmailComposer and PipelineVisualizer present on the page
    - Test: summary statistics visible
    - Test: two-column layout
    - _Requirements: 2.1, 2.2, 2.4_

- [ ] 10. Frontend — Agents Page
  - [ ] 10.1 Implement the Agents page with configured agents
    - Display all configured agents with: agentId, team, role, status (Idle/Working)
    - Implement periodic polling for status updates
    - Display error message if the endpoint is not reachable
    - Update hook `useAgents.ts` for the new endpoint
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [ ] 10.2 Write unit tests for useAgents hook
    - Test: returns list of configured agents from the backend
    - Test: periodic polling updates status
    - Test: error handling when endpoint is not reachable
    - Test: isLoading state during fetch
    - _Requirements: 3.5, 3.6_

  - [ ] 10.3 Write property test for agent status mapping (Property 1)
    - **Property 1: Agent status mapping correctness**
    - For any configured agent, if active as an Akka actor the status must be "Working", otherwise "Idle"
    - Use fast-check to generate random agents with active/inactive state
    - **Validates: Requirements 3.2, 3.3**

  - [ ] 10.4 Write property test for agent display completeness (Property 2)
    - **Property 2: Agent display completeness**
    - For any list of agents, every agent must be rendered with all four required fields
    - Use fast-check to generate lists of agents with random fields
    - **Validates: Requirements 3.1, 3.4**

- [ ] 11. Frontend — Issues Page with Filters
  - [ ] 11.1 Implement the Issues page with current state and filters
    - Display each issue as a single row: ID, current stage, detail, last update timestamp
    - Implement filter by current stage
    - Update hook `useIssues.ts` for the new data format
    - _Requirements: 4.1, 4.2, 4.3_

  - [ ] 11.2 Write unit tests for useIssues hook
    - Test: returns issue list from the backend
    - Test: data update via gRPC stream
    - Test: error handling when endpoint is not reachable
    - _Requirements: 4.1, 4.2_

  - [ ] 11.3 Write property test for issue display (Property 3)
    - **Property 3: Issue display with current state**
    - For any list of issues, each issue must appear exactly once with stage, detail, and timestamp visible
    - Use fast-check to generate lists of random WorkflowState
    - **Validates: Requirements 4.1, 4.2**

  - [ ] 11.4 Write property test for issue filtering (Property 4)
    - **Property 4: Issue filtering by stage**
    - For any list of issues and selected stage filter, the displayed issues must be exactly those whose current stage matches the filter
    - Use fast-check to generate issues + random stage filter
    - **Validates: Requirements 4.3**

- [ ] 12. Frontend — Event Log Page (Persistent)
  - [ ] 12.1 Implement the Event Log page with persistent data
    - Read events from the new endpoint `GET /api/support/events`
    - Display for each event: issue ID, previous stage, new stage, timestamp, detail
    - Reverse chronological order (most recent at the top)
    - Maximum limit of 200 entries
    - _Requirements: 4.4, 4.5, 4.6, 4.8_

  - [ ] 12.2 Write unit tests for EventLogPage and useEvents hook
    - Test: fetch events from the new endpoint `/api/support/events`
    - Test: events displayed in reverse chronological order
    - Test: maximum limit of 200 entries respected
    - Test: error handling when endpoint is not reachable
    - _Requirements: 4.4, 4.5, 4.8_

  - [ ] 12.3 Write property test for event ordering (Property 5)
    - **Property 5: Event log reverse chronological ordering**
    - For any list of events, the Event Log must display them in strictly descending order of timestamp
    - Use fast-check to generate events with random timestamps
    - **Validates: Requirements 4.4**

  - [ ] 12.4 Write property test for event display completeness (Property 6)
    - **Property 6: Event display completeness**
    - For any event, the rendered output must contain: issue ID, previous stage (if available), new stage, timestamp, and detail
    - Use fast-check to generate random StateTransitionEvent
    - **Validates: Requirements 4.6**

  - [ ] 12.5 Write property test for event capping (Property 7)
    - **Property 7: Event log capping invariant**
    - For any number of events in the Events_Table, the Event Log page must display at most 200 entries
    - Use fast-check to generate lists of variable length
    - **Validates: Requirements 4.8**

- [ ] 13. Frontend — Inbox Page (Replaces Emails Page)
  - [ ] 13.1 Implement the Inbox page
    - Replace the `/emails` page with the new Inbox Page
    - Display table with: ID, sender, subject, status (colored badge), reception timestamp, processing timestamp
    - Badge: yellow/amber for "Queued", green for "Processed", red for "Failed" (with error message)
    - Summary counters at the top: queued, processed, and failed messages
    - Filter by status (Queued, Processed, Failed, All)
    - Periodic polling for updates
    - Create hook `useInbox.ts`
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8_

  - [ ] 13.2 Write unit tests for useInbox hook
    - Test: fetch inbox messages from the backend
    - Test: periodic polling updates the list
    - Test: status filter works correctly
    - Test: stats calculation (queued, processed, failed) correct
    - Test: error handling when endpoint is not reachable
    - _Requirements: 8.6, 8.7, 8.8_

  - [ ] 13.3 Write property test for inbox status badge mapping (Property 14)
    - **Property 14: Inbox status badge mapping**
    - For any InboxMessage: ProcessedAt null → "Queued" (yellow); ProcessedAt set + Error null → "Processed" (green); Error non-null → "Failed" (red)
    - Use fast-check to generate InboxMessage with random states
    - **Validates: Requirements 8.3, 8.4, 8.5**

  - [ ] 13.4 Write property test for inbox filtering (Property 15)
    - **Property 15: Inbox filtering by status**
    - For any list of messages and selected status filter, the displayed messages must be exactly those matching the filter
    - Use fast-check to generate messages + random status filter
    - **Validates: Requirements 8.6**

  - [ ] 13.5 Write property test for inbox counters (Property 16)
    - **Property 16: Inbox summary counters accuracy**
    - For any list of messages, the summary counters must exactly match the count of messages in each status category
    - Use fast-check to generate lists of messages with mixed states
    - **Validates: Requirements 8.8**

- [ ] 14. Frontend — Navigation, Routing, and Dead Code Cleanup
  - [ ] 14.1 Update navigation and routing
    - Update the router to replace `/emails` with `/inbox`
    - Update navigation links in the layout
    - Verify that all pages are reachable from the navigation
    - _Requirements: 8.1_

  - [ ] 14.2 Remove dead frontend code
    - Delete `dashboard/src/api/sse.ts` (SSE client, replaced by gRPC-Web)
    - Delete `dashboard/src/hooks/useSSE.ts` (SSE hook, replaced by `useGrpcStream.ts`)
    - Delete `dashboard/src/pages/EmailsPage.tsx` (replaced by InboxPage)
    - Remove imports and references to `useSSE` from all components
    - Verify that no file imports deleted modules (grep for `sse`, `useSSE`, `EmailsPage`)
    - Delete any tests related to removed modules (`useSSE.test.ts` if present)

- [ ] 15. Documentation and README
  - [ ] 15.1 Update the main README.md
    - Add "Dashboard" section with brief description and link to `docs/dashboard.md`
    - Update the "API Endpoints" table: remove `/stream`, add `/events`, `/inbox`, update `/emails` (202) and `/agents` (configured)
    - Add mention of gRPC streaming in the API section
    - Update the "Architecture" Mermaid diagram: replace `WorkflowStateTracker` with `EF Core InMemory + InboxProcessor`, add gRPC Service
    - Update "What It Does": mention asynchronous processing (inbox) and monitoring dashboard
    - Update "Project Structure": add `dashboard/` folder with description
    - Update "Getting Started": add instructions to start the dashboard (`cd dashboard && npm install && npm run dev`)
    - Update "Configuration": `EnableVisualization` → "Enables gRPC streaming and visualization endpoints"; add `InboxPollingIntervalSeconds`

  - [ ] 15.2 Create `docs/dashboard.md` — Dashboard documentation
    - Overview: purpose of the dashboard (real-time monitoring of the AI Support workflow)
    - Tech stack: React, TypeScript, Vite, Tailwind CSS, ReactFlow, gRPC-Web
    - Pages and features: Overview (graph + email form), Issues, Event Log, Agents, Inbox
    - Backend connection: gRPC-Web streaming + REST polling
    - How to start: prerequisites (Node.js), dependency installation, dev server
    - Dashboard project folder structure
    - Link to main README (`[← Back to README](../README.md)`)

  - [ ] 15.3 Create `docs/transactional-inbox.md` — Transactional Inbox pattern documentation
    - What the Transactional Inbox pattern is and why it was adopted
    - Problem solved: decoupling reception/processing, failure resilience, immediate client response
    - Flow: POST email → save InboxMessage → HTTP 202 → InboxProcessor polling → workflow processing
    - InboxMessage structure (Id, MessageType, Payload, ReceivedAt, ProcessedAt, Error)
    - Error handling: error recorded in the Error field, ProcessedAt set to prevent infinite retries
    - Processing order: FIFO by ReceivedAt
    - Configuration: polling interval in appsettings.json
    - Mermaid sequence diagram of the complete flow
    - Link to main README (`[← Back to README](../README.md)`)

- [ ] 16. Final Checkpoint — Verify complete build and tests
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- All tests (unit and property-based) are mandatory
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties (FsCheck for .NET, fast-check for TypeScript)
- Unit tests validate specific scenarios and edge cases
- Task order respects dependencies: persistence → backend services → frontend
