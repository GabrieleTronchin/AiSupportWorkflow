# Requirements Document

## Introduction

This document defines the requirements for improving the user interface of the AI Support workflow monitoring dashboard and the related architectural changes to the backend. The changes cover: pipeline graph locking, email form integration in the Overview page, Agents page improvements, differentiation between Issues and Event Log, graph animation during processing, migration from SSE to gRPC streaming, Transactional Inbox pattern for asynchronous email processing, and introduction of a persistence layer with EF Core InMemory.

## Glossary

- **Pipeline_Graph**: The ReactFlow component (`PipelineVisualizer`) that visualizes the workflow stages as a directed graph on the Overview page.
- **Overview_Page**: The main dashboard page that shows summary statistics, the Pipeline_Graph, and the email submission form.
- **Email_Composer**: The form component for sending test emails to the support system.
- **Agents_Page**: The page that shows the status of AI agents configured in the system.
- **Issues_Page**: The page that shows the table of processed issues with their current status.
- **Event_Log_Page**: The page that shows the chronological log of all workflow state transition events.
- **gRPC_Stream**: The gRPC service with server streaming that replaces the SSE endpoint to send real-time workflow state updates to the client.
- **gRPC_Service**: The server-side gRPC service (.NET) that exposes RPCs for workflow monitoring.
- **gRPC_Web_Client**: The gRPC-Web client in the React dashboard that connects to the gRPC_Service via the gRPC-Web protocol.
- **Configured_Agent**: An agent defined in the `appsettings.json` configuration under `Workflow.Teams[].Agents[]`, regardless of whether it is currently active as an Akka actor.
- **Active_Stage**: The workflow stage where an issue currently resides during processing.
- **State_Transition_Event**: A single stage change of an issue in the workflow, recorded as an individual persistent entry in the events table.
- **Inbox_Message**: A record in the inbox table representing a received email waiting to be processed by the workflow.
- **Inbox_Processor**: A background service (`IHostedService`) that polls the inbox table and processes queued messages by starting the workflow for each one.
- **Workflow_DbContext**: The EF Core DbContext that manages persistence of issues, events, and inbox messages, configured with the InMemory provider.
- **Events_Table**: The table in the Workflow_DbContext that stores each State_Transition_Event as a separate record, serving as a persistent audit log.
- **Inbox_Page**: The dashboard page (replacing the current "/emails" page) dedicated to monitoring the inbox queue status: pending, processed, and failed messages.

## Requirements

### Requirement 1: Fixed Pipeline Graph (No Panning Interaction)

**User Story:** As a dashboard user, I want the pipeline graph to be fixed and always visible on the Overview page, so that I cannot accidentally move it off-screen.

#### Acceptance Criteria

1. THE Pipeline_Graph SHALL disable panning (view dragging) so that the user cannot move the graph.
2. THE Pipeline_Graph SHALL disable zoom so that the user cannot zoom in or out of the view.
3. THE Pipeline_Graph SHALL disable node selection so that the user cannot select graph elements.
4. THE Pipeline_Graph SHALL use `fitView` mode to automatically fit the available container, ensuring that all nodes are always visible.
5. THE Pipeline_Graph SHALL disable mouse scroll on the graph view so that page scrolling is not intercepted by the component.

### Requirement 2: Email Form Integrated in the Overview Page

**User Story:** As a dashboard user, I want to be able to send a test email directly from the Overview page next to the pipeline graph, so that I can immediately see the workflow progress in the visualization.

#### Acceptance Criteria

1. THE Overview_Page SHALL display the Email_Composer component on the same page as the Pipeline_Graph.
2. THE Overview_Page SHALL position the Email_Composer so that it is visible simultaneously with the Pipeline_Graph without needing to scroll.
3. WHEN an email is successfully sent via the Email_Composer, THE Overview_Page SHALL update the Pipeline_Graph in real-time via the gRPC_Stream to show the progress of the new issue.
4. THE Overview_Page SHALL maintain the summary statistics (Total Issues, Active Agents, Recent Failures) at the top of the page.

### Requirement 3: Agents Page with Configured Agents

**User Story:** As a dashboard user, I want to see all agents configured in the system with their current status (Idle or Working), so that I have a complete view of available resources even when they are not actively processing.

#### Acceptance Criteria

1. THE Agents_Page SHALL display all Configured_Agent defined in the system configuration, regardless of whether they are currently active as Akka actors.
2. WHEN a Configured_Agent is not currently active as an actor, THE Agents_Page SHALL display its status as "Idle".
3. WHEN a Configured_Agent is currently active and processing, THE Agents_Page SHALL display its status as "Working".
4. THE Agents_Page SHALL display for each agent: the agent identifier, the team it belongs to, the role, and the current status.
5. THE Agents_Page SHALL update agent statuses via periodic polling without requiring a manual page refresh.
6. IF the agents API endpoint is not reachable, THEN THE Agents_Page SHALL display an informative error message to the user.

### Requirement 4: Differentiation Between Issues and Event Log with Persistence

**User Story:** As a dashboard user, I want the Issues page to show a tabular view of issues with their current status (like a ticket tracker), and the Event Log page to show a persistent chronological log of all state transitions (like an audit log), so that I can clearly distinguish between the two views and not lose history on refresh.

#### Acceptance Criteria

1. THE Issues_Page SHALL display each issue as a single row in the table, representing the current (most recent) state of that issue.
2. THE Issues_Page SHALL display for each issue: ID, current stage, detail, and last update timestamp.
3. THE Issues_Page SHALL allow the user to filter issues by current stage.
4. THE Event_Log_Page SHALL display each State_Transition_Event as a separate entry in the log, in reverse chronological order (most recent at the top).
5. THE Event_Log_Page SHALL read events from the persistent Events_Table, so that history is available even after a page refresh.
6. THE Event_Log_Page SHALL display for each event: issue ID, previous stage (if available), new stage, timestamp, and detail.
7. WHEN an issue changes stage, THE Workflow_DbContext SHALL save a new record in the Events_Table with the previous stage, new stage, timestamp, and detail.
8. THE Event_Log_Page SHALL limit the number of displayed entries to a maximum of 200 to maintain rendering performance.

### Requirement 5: Pipeline Graph Animation During Processing

**User Story:** As a dashboard user, I want the pipeline graph to visually animate when a workflow is in progress, so that I can see at a glance which stage is active and which ones have been completed.

#### Acceptance Criteria

1. WHILE an issue is being actively processed, THE Pipeline_Graph SHALL highlight the current stage node (Active_Stage) with a pulsing or glowing visual effect.
2. WHILE an issue is being actively processed, THE Pipeline_Graph SHALL color the nodes of already completed stages in the main flow green.
3. WHILE an issue is being actively processed, THE Pipeline_Graph SHALL animate the edges between completed stages and the current stage to indicate the flow direction.
4. WHEN an issue reaches a terminal error stage (Failed, ClassifiedOutOfScope, ManualReviewRequired), THE Pipeline_Graph SHALL highlight the terminal node in red.
5. WHEN an issue reaches the final stage successfully (CodeChangeGenerated), THE Pipeline_Graph SHALL color all nodes in the completed path green.
6. WHEN no issue is selected or being processed, THE Pipeline_Graph SHALL display all nodes in an inactive state (neutral gray color) without animations.
7. THE Pipeline_Graph SHALL automatically select the most recent issue received via gRPC_Stream to visualize its progress in real-time.

### Requirement 6: Migration from SSE to gRPC Streaming

**User Story:** As a developer, I want to replace the SSE endpoint with a gRPC service with server streaming, so that I have more efficient, typed, and bidirectional communication between the backend and dashboard.

#### Acceptance Criteria

1. THE gRPC_Service SHALL expose a server streaming RPC that sends WorkflowState updates to the client in real-time.
2. THE gRPC_Service SHALL define Protobuf messages for WorkflowState (issueId, stage, lastUpdated, detail) and for the stream subscription request.
3. THE gRPC_Service SHALL send an update to the client every time an issue changes stage, without fixed-interval polling.
4. THE gRPC_Web_Client SHALL connect to the gRPC_Service using the gRPC-Web protocol, compatible with browsers.
5. THE gRPC_Web_Client SHALL automatically reconnect to the gRPC_Service in case of disconnection.
6. IF the gRPC_Service is not reachable, THEN THE gRPC_Web_Client SHALL display a disconnected status indicator in the dashboard.
7. THE gRPC_Service SHALL support stream cancellation via CancellationToken when the client disconnects.
8. THE gRPC_Service SHALL be configurable via `appsettings.json` (enable/disable, like the current `EnableVisualization` flag).

### Requirement 7: Transactional Inbox Pattern for Asynchronous Email Processing

**User Story:** As a dashboard user, I want email submission to be asynchronous (immediate response to the client, background processing), so that I don't have to wait for the entire workflow to complete before receiving confirmation.

#### Acceptance Criteria

1. WHEN an email is sent via the POST `/api/support/emails` endpoint, THE gRPC_Service SHALL save the message as an Inbox_Message in the inbox table of the Workflow_DbContext and immediately return an acceptance response (HTTP 202 Accepted) with the message ID.
2. THE Inbox_Message SHALL contain: Id (GUID), MessageType (message type), Payload (email serialized as JSON), ReceivedAt (reception timestamp), ProcessedAt (processing timestamp, null if not yet processed), Error (possible processing error).
3. THE Inbox_Processor SHALL be implemented as an `IHostedService` with a polling loop that periodically checks the inbox table for unprocessed messages.
4. WHEN the Inbox_Processor finds an unprocessed Inbox_Message, THE Inbox_Processor SHALL deserialize the payload, start the processing workflow, and update the ProcessedAt field upon completion.
5. IF processing of an Inbox_Message fails, THEN THE Inbox_Processor SHALL record the error in the Error field of the message and set ProcessedAt to prevent infinite retries.
6. THE Inbox_Processor SHALL process messages in ReceivedAt order (FIFO).
7. THE Inbox_Processor SHALL be configurable for the polling interval via `appsettings.json`.

### Requirement 8: Inbox Page — Email Queue Monitoring

**User Story:** As a dashboard user, I want to see the inbox queue status (pending, processed, and failed emails), so that I can monitor the asynchronous processing flow of submitted emails.

#### Acceptance Criteria

1. THE Inbox_Page SHALL replace the current "/emails" page and display a table with all Inbox_Message present in the system.
2. THE Inbox_Page SHALL display for each message: ID, sender (extracted from payload), subject (extracted from payload), status (Queued, Processed, Failed), reception timestamp, processing timestamp.
3. WHEN an Inbox_Message has ProcessedAt equal to null, THE Inbox_Page SHALL display its status as "Queued" with a yellow/amber colored badge.
4. WHEN an Inbox_Message has ProcessedAt set and Error equal to null, THE Inbox_Page SHALL display its status as "Processed" with a green colored badge.
5. WHEN an Inbox_Message has the Error field set, THE Inbox_Page SHALL display its status as "Failed" with a red colored badge and the error message visible.
6. THE Inbox_Page SHALL allow the user to filter messages by status (Queued, Processed, Failed, All).
7. THE Inbox_Page SHALL update the list via periodic polling to reflect status changes in real-time.
8. THE Inbox_Page SHALL display summary counters at the top: number of queued, processed, and failed messages.

### Requirement 9: Persistence Layer with EF Core InMemory

**User Story:** As a developer, I want to introduce a persistence layer with EF Core and InMemory provider, so that I have a structured data infrastructure that is easily replaceable with a real database in the future.

#### Acceptance Criteria

1. THE Workflow_DbContext SHALL be configured with the EF Core InMemory provider for data storage.
2. THE Workflow_DbContext SHALL expose a DbSet for issues (current state of each processed issue).
3. THE Workflow_DbContext SHALL expose a DbSet for the Events_Table (history of all state transitions).
4. THE Workflow_DbContext SHALL expose a DbSet for Inbox_Message (emails queued for processing).
5. THE Workflow_DbContext SHALL be registered in the DI container via an `AddPersistence()` extension method in the Infrastructure layer.
6. WHEN an issue changes stage, THE Workflow_DbContext SHALL update both the current state record in the issues table and add a new record in the Events_Table.
7. THE Workflow_DbContext SHALL replace the current `ConcurrentDictionary` in the `WorkflowStateTracker` as the storage mechanism.
8. THE Workflow_DbContext SHALL configure entities via `IEntityTypeConfiguration<T>` to define primary keys, indexes, and constraints.
