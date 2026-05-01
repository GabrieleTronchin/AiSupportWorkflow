# Requirements Document

## Introduction

Improvements to the monitoring dashboard UI. This feature covers five areas: real-time visualization of agent activity in the pipeline graph, dead code cleanup in the frontend, pre-loaded email templates in the send mail form, empty/error state handling in the Agents page with visibility on emails being processed, and a configurable sequential processing mode.

## Glossary

- **Pipeline_Visualizer**: The React component (`PipelineVisualizer.tsx`) that renders the workflow graph via ReactFlow.
- **Email_Composer**: The React component (`EmailComposer.tsx`) that provides the support email submission form.
- **Agent_Monitor**: The React component (`AgentMonitor.tsx`) that displays the status of configured agents.
- **Agents_Page**: The React page (`AgentsPage.tsx`) that hosts the Agent_Monitor.
- **Email_Template**: A predefined object containing sender, subject, and body that can be selected to pre-fill the Email_Composer form.
- **Dead_Code**: Source code, dependencies, or exports not used by any execution path in the frontend.
- **Live_Activity_Indicator**: A visual indicator in the pipeline graph that shows in real-time the current stage of an issue while an agent is processing it, with a pulsing animation and issue details.
- **Sequential_Processing_Mode**: A configurable mode (`SequentialProcessing` flag) that forces the system to process emails one at a time in sequence, allowing observation of the full pipeline progression for each individual email.
- **Current_Email_Info**: Information about the email/issue currently being processed by an agent, including subject, issueId, and current stage.

## Requirements

### Requirement 1: Real-time agent activity visualization in the Pipeline Visualizer

**User Story:** As a developer monitoring the dashboard, I want the pipeline graph to show real-time progress of agents as they process emails, so that I can see exactly which stage each issue is at and watch the progression through the pipeline live.

#### Acceptance Criteria

1. WHEN an agent is actively processing an issue, THE Pipeline_Visualizer SHALL highlight the current stage node with a pulsing Live_Activity_Indicator that reflects the actual workflow stage of that issue.
2. WHEN an issue transitions from one stage to the next, THE Pipeline_Visualizer SHALL animate the transition by moving the Live_Activity_Indicator to the new stage node within 1 second of receiving the state update.
3. THE Pipeline_Visualizer SHALL display the issueId and subject of the currently active issue as a label or tooltip near the active stage node.
4. WHEN multiple issues are being processed simultaneously (sequential mode disabled), THE Pipeline_Visualizer SHALL show all active issues in the graph, each with its own Live_Activity_Indicator at its respective stage.
5. WHEN no issue is currently being processed, THE Pipeline_Visualizer SHALL display the graph in an idle state with all nodes in a neutral color.
6. THE Pipeline_Visualizer SHALL color completed stages in green, the active stage with a blue pulsing glow, and pending stages in neutral gray, matching the existing color conventions.

### Requirement 2: Dead code cleanup and gRPC-Web client integration in the frontend

**User Story:** As a developer maintaining the codebase, I want the frontend to use the real gRPC-Web streaming client instead of the polling fallback, and I want any truly unused code removed, so that the codebase is clean and uses the intended real-time transport.

#### Acceptance Criteria

1. THE Dashboard SHALL use `@connectrpc/connect-web` to establish a gRPC-Web server streaming connection to the backend `WorkflowMonitor.SubscribeToUpdates` service instead of polling `GET /api/support/issues`.
2. THE Dashboard SHALL generate TypeScript client code from `workflow_monitor.proto` using `@bufbuild/protobuf` and `@connectrpc/connect`.
3. WHEN a hook, component, or utility module exports a symbol that is not imported by any other module, THE Dead_Code cleanup SHALL remove or flag that export.
4. THE Dashboard_Build SHALL pass TypeScript type-checking (`tsc --noEmit`) after all changes.
5. THE Dashboard_Build SHALL pass all existing tests (`vitest --run`) after all changes.

### Requirement 3: Pre-loaded email templates in the Email Composer

**User Story:** As a developer debugging the workflow, I want pre-loaded email templates in the send mail form that mirror the test scenarios from the `.http` file, so that I can quickly send known test cases without manual copy-paste.

#### Acceptance Criteria

1. THE Email_Composer SHALL display a template selector control above the form fields.
2. WHEN the user selects an Email_Template from the selector, THE Email_Composer SHALL pre-fill the sender, subject, and body fields with the template values.
3. THE Email_Composer SHALL include at least the following templates: Scenario A1 (NullReferenceException), Scenario A2 (blank total price), Scenario A3 (missing test), Scenario B1 (SQL Injection), Scenario B2 (missing null check), Scenario B3 (flaky test), Edge Case Out-of-Scope, Edge Case Ambiguous Routing, Edge Case Failed Routing, Edge Case Empty Input Validation.
4. WHEN the user selects the empty input validation template, THE Email_Composer SHALL pre-fill subject and body with empty strings to test validation behavior.
5. WHEN the user modifies a pre-filled field after template selection, THE Email_Composer SHALL retain the user's modifications without reverting to template values.
6. THE Email_Composer SHALL group templates by category (Application A, Application B, Edge Cases) in the selector for easy navigation.

### Requirement 4: Empty/error state handling and email visibility in the Agents page

**User Story:** As a user viewing the Agents page, I want clear feedback when no agents are available or when an error occurs, and I want to see which specific email/issue each agent is currently handling, so that I have full visibility on agent activity.

#### Acceptance Criteria

1. WHEN the agents API returns an empty array, THE Agents_Page SHALL display an informative empty-state message indicating that no agents are configured.
2. WHEN the agents API returns an empty array, THE Agents_Page SHALL suggest that the `EnableVisualization` configuration option may need to be enabled.
3. WHEN the agents API returns an error, THE Agents_Page SHALL display an error message with the HTTP status code and error description.
4. WHEN the agents API returns an error, THE Agents_Page SHALL provide a retry button that re-fetches the agents data.
5. WHILE the agents data is loading, THE Agent_Monitor SHALL display a skeleton or loading indicator that occupies the same layout space as the agent cards.
6. WHEN an agent has status "Working", THE Agent_Monitor SHALL display the Current_Email_Info including the issueId and the email subject that the agent is currently processing.
7. WHEN an agent has status "Working", THE Agent_Monitor SHALL display the current workflow stage of the issue being processed by that agent.
8. WHEN an agent transitions from "Working" to "Idle", THE Agent_Monitor SHALL clear the Current_Email_Info and show "No recent activity" within 2 seconds of the status change.

### Requirement 5: Configurable sequential processing mode

**User Story:** As a developer debugging the workflow, I want a configurable option (isDev-style flag) that forces the system to process emails one at a time, so that I can observe the full pipeline progression for each individual email without overlap from parallel processing.

#### Acceptance Criteria

1. THE System SHALL expose a configuration flag `SequentialProcessing` (boolean) in the `Workflow` section of `appsettings.json` that defaults to `false`.
2. WHILE `SequentialProcessing` is set to `true`, THE InboxProcessor SHALL process only one email at a time, waiting for the current issue to reach a terminal state before picking up the next message from the inbox.
3. WHILE `SequentialProcessing` is set to `true`, THE Pipeline_Visualizer SHALL show only one active issue at a time, making the full stage-by-stage progression clearly visible.
4. WHILE `SequentialProcessing` is set to `false`, THE InboxProcessor SHALL process emails as they arrive without waiting for previous issues to complete (default parallel behavior).
5. WHEN `SequentialProcessing` is enabled, THE Dashboard SHALL display a visual indicator (badge or label) in the Overview page confirming that sequential mode is active.
6. THE System SHALL allow changing the `SequentialProcessing` flag without requiring a code change, using standard configuration mechanisms (appsettings.json or environment variable).
