# Requirements Document

## Introduction

The Dashboard UI is a standalone internal React web application for monitoring and interacting with the AI Support Workflow system. It provides real-time visibility into the workflow pipeline, displays agent activity, lists processed issues, streams workflow events, and allows operators to submit test emails to trigger the pipeline. The application lives in a `dashboard/` directory at the repository root, independent from the .NET backend, and communicates with the backend exclusively through its existing REST and SSE API endpoints.

## Glossary

- **Dashboard**: The standalone React web application described by this specification
- **Pipeline_Visualizer**: An interactive diagram component that renders workflow stages as connected nodes and highlights the current stage for each issue
- **Email_Composer**: A form component that allows operators to compose and submit test emails to the backend API
- **Issues_List**: A table component displaying all processed issues with their current workflow stage, detail, and timestamp
- **Agent_Monitor**: A card-based component displaying the status of each AI agent in the system
- **Event_Log**: A real-time feed component displaying workflow state updates received via SSE
- **SSE_Client**: A client-side module that establishes and maintains a Server-Sent Events connection to the backend stream endpoint
- **API_Client**: A typed HTTP client module responsible for all REST communication with the backend
- **WorkflowStage**: One of: Received, Classified, ClassifiedOutOfScope, TeamAssigned, AgentAssigned, Resolving, Resolved, CodeChangeGenerated, Failed, ManualReviewRequired
- **WorkflowState**: An object containing issueId (GUID), stage (WorkflowStage), lastUpdated (ISO timestamp), and detail (optional string)
- **AgentStatus**: An object containing agentId (string), status (string), and lastAction (optional string)
- **IncomingEmail**: An object containing sender (string), subject (string), and body (string)
- **Vite_Proxy**: The Vite development server proxy configuration that forwards API requests to the backend at localhost:5080

## Requirements

### Requirement 1: Project Scaffolding and Build Configuration

**User Story:** As a developer, I want the dashboard to be a standalone Vite + React + TypeScript project in the `dashboard/` directory, so that it can be developed, built, and deployed independently from the .NET backend.

#### Acceptance Criteria

1. THE Dashboard SHALL be a standalone project located in the `dashboard/` directory at the repository root with its own `package.json`, `vite.config.ts`, and `tsconfig.json`
2. THE Dashboard SHALL use React 18, TypeScript, Vite as the build tool, Tailwind CSS for styling, and Vitest as the test runner
3. WHEN running in development mode, THE Vite_Proxy SHALL forward all requests matching `/api/**` to `http://localhost:5080`
4. THE Dashboard SHALL produce a static production build via `npm run build` that outputs to `dashboard/dist/`

### Requirement 2: Typed API Client

**User Story:** As a developer, I want a typed API client module, so that all backend communication is centralized, type-safe, and easy to mock in tests.

#### Acceptance Criteria

1. THE API_Client SHALL expose a function to submit an email by sending a POST request to `/api/support/emails` with an IncomingEmail payload and returning the response
2. THE API_Client SHALL expose a function to fetch all issues by sending a GET request to `/api/support/issues` and returning an array of WorkflowState objects
3. THE API_Client SHALL expose a function to fetch a single issue by sending a GET request to `/api/support/issues/{id}` and returning a WorkflowState object
4. THE API_Client SHALL expose a function to fetch all agent statuses by sending a GET request to `/api/support/agents` and returning an array of AgentStatus objects
5. IF the backend returns an HTTP error status, THEN THE API_Client SHALL throw a typed error containing the status code and error message from the response body

### Requirement 3: SSE Connection Management

**User Story:** As a developer, I want a reusable SSE client hook, so that components can subscribe to real-time workflow updates without managing connection lifecycle directly.

#### Acceptance Criteria

1. THE SSE_Client SHALL establish an EventSource connection to `/api/support/stream` when the hook mounts
2. WHEN the SSE_Client receives a message event, THE SSE_Client SHALL parse the JSON data as an array of WorkflowState objects and provide the latest state to subscribers
3. IF the SSE connection is lost, THEN THE SSE_Client SHALL attempt to reconnect automatically using the browser's native EventSource reconnection behavior
4. WHEN the consuming component unmounts, THE SSE_Client SHALL close the EventSource connection to prevent resource leaks

### Requirement 4: Pipeline Visualizer

**User Story:** As an operator, I want to see an interactive diagram of the workflow pipeline stages, so that I can understand the flow and see where each issue currently sits.

#### Acceptance Criteria

1. THE Pipeline_Visualizer SHALL render the workflow stages as connected nodes in a directed graph using React Flow: Received → Classified → TeamAssigned → AgentAssigned → Resolving → Resolved → CodeChangeGenerated
2. THE Pipeline_Visualizer SHALL render terminal/error stages (ClassifiedOutOfScope, Failed, ManualReviewRequired) as branching nodes from their respective decision points
3. WHEN a WorkflowState is selected or provided, THE Pipeline_Visualizer SHALL visually highlight the node corresponding to the current stage of that issue
4. THE Pipeline_Visualizer SHALL use distinct visual styling (color or border) to differentiate active stages, completed stages, and error/terminal stages

### Requirement 5: Email Composer

**User Story:** As an operator, I want to compose and submit test emails through the dashboard, so that I can trigger the workflow pipeline without using external tools.

#### Acceptance Criteria

1. THE Email_Composer SHALL provide input fields for sender, subject, and body
2. WHEN the operator submits the form with valid data, THE Email_Composer SHALL send the IncomingEmail payload to the API_Client submit function
3. THE Email_Composer SHALL disable the submit button and display a loading indicator while the request is in flight
4. WHEN the API_Client returns a successful response, THE Email_Composer SHALL display a success notification and reset the form fields
5. IF the API_Client returns an error, THEN THE Email_Composer SHALL display the error message to the operator without resetting the form
6. THE Email_Composer SHALL validate that subject and body fields are non-empty before allowing submission

### Requirement 6: Issues List

**User Story:** As an operator, I want to see a table of all processed issues with their current stage and details, so that I can monitor the overall system activity.

#### Acceptance Criteria

1. THE Issues_List SHALL display a table with columns: Issue ID, Stage, Detail, and Last Updated
2. WHEN the Dashboard loads, THE Issues_List SHALL fetch the initial list of issues from the API_Client
3. WHILE the SSE_Client is connected, THE Issues_List SHALL update issue rows in real-time as new WorkflowState events arrive
4. WHEN a new issue appears in the SSE stream that is not in the current list, THE Issues_List SHALL add it to the table
5. THE Issues_List SHALL display the Last Updated timestamp in a human-readable relative format (e.g., "2 minutes ago")
6. THE Issues_List SHALL visually distinguish terminal stages (Failed, ClassifiedOutOfScope) from active stages using color coding

### Requirement 7: Agent Monitor

**User Story:** As an operator, I want to see the current status of each AI agent, so that I can understand agent availability and recent activity.

#### Acceptance Criteria

1. THE Agent_Monitor SHALL display a card for each agent showing: agent ID, current status, and last action
2. WHEN the Dashboard loads, THE Agent_Monitor SHALL fetch agent statuses from the API_Client
3. THE Agent_Monitor SHALL visually distinguish agent statuses using color-coded badges (e.g., idle vs. working)
4. THE Agent_Monitor SHALL refresh agent statuses by polling the API_Client at a configurable interval (default: 5 seconds)

### Requirement 8: Event Log

**User Story:** As an operator, I want a real-time scrolling feed of workflow events, so that I can observe the system processing issues as they happen.

#### Acceptance Criteria

1. THE Event_Log SHALL display a chronological list of workflow state change events received from the SSE_Client
2. WHEN a new event arrives from the SSE_Client, THE Event_Log SHALL prepend it to the top of the feed
3. THE Event_Log SHALL display each event with: issue ID (truncated), stage, detail (if present), and timestamp
4. THE Event_Log SHALL limit the displayed events to the most recent 100 entries to maintain rendering performance
5. THE Event_Log SHALL auto-scroll to show the newest event unless the operator has manually scrolled up

### Requirement 9: Application Layout and Navigation

**User Story:** As an operator, I want a multi-page layout with sidebar navigation and dark theme, so that each section has full screen space and the interface is comfortable for extended monitoring sessions.

#### Acceptance Criteria

1. THE Dashboard SHALL render a fixed sidebar on the left with navigation links to: Overview, Emails, Issues, Agents, and Event Log pages
2. THE Dashboard SHALL use React Router for client-side navigation between pages without full page reloads
3. THE Dashboard SHALL apply a dark color theme (dark background, light text) globally using Tailwind CSS dark mode utilities
4. THE Sidebar SHALL display the application title "AI Support Workflow" at the top and navigation items with icons below
5. THE Sidebar SHALL visually highlight the currently active navigation item
6. THE Sidebar SHALL be collapsible to icon-only mode to maximize content area
7. THE Dashboard SHALL be responsive and usable at viewport widths from 1024px to 1920px
8. THE Overview page SHALL display the Pipeline Visualizer and summary cards (total issues, active agents, recent failures)
9. THE Emails page SHALL display the Email Composer form
10. THE Issues page SHALL display the Issues List table with detail panel
11. THE Agents page SHALL display the Agent Monitor cards
12. THE Event Log page SHALL display the real-time event feed

### Requirement 10: CI/CD Pipeline

**User Story:** As a developer, I want a separate CI pipeline for the dashboard, so that frontend changes are validated independently without triggering backend builds.

#### Acceptance Criteria

1. THE Dashboard SHALL have a GitHub Actions workflow file at `.github/workflows/dashboard-ci.yml` that triggers on pushes and pull requests affecting `dashboard/**` paths
2. THE Dashboard CI pipeline SHALL run lint, type-check, and test steps
3. THE Dashboard CI pipeline SHALL run `npm run build` to verify the production build succeeds
4. THE existing backend CI pipeline SHALL be updated with a path filter to trigger only on changes to `src/**` and `tests/**`

### Requirement 11: Hook-Based Architecture

**User Story:** As a developer, I want all business logic encapsulated in custom React hooks, so that components remain purely presentational and logic is independently testable.

#### Acceptance Criteria

1. THE Dashboard SHALL implement a `useIssues` hook that manages fetching, caching, and real-time updating of the issues list
2. THE Dashboard SHALL implement a `useAgents` hook that manages fetching and polling of agent statuses
3. THE Dashboard SHALL implement a `useSSE` hook that manages the SSE connection lifecycle and exposes the latest event data
4. THE Dashboard SHALL implement a `useEmailSubmit` hook that manages form submission state (loading, success, error) and calls the API_Client
5. THE Dashboard components SHALL receive all data and callbacks from hooks and contain no direct API calls or business logic
