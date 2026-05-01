# Tasks

## Task 1: Project Scaffolding and Configuration

- [ ] 1.1 Initialize the `dashboard/` project with `package.json` containing all required dependencies (React 18, TypeScript, Vite, Tailwind CSS, Vitest, React Testing Library, React Flow, fast-check)
- [ ] 1.2 Create `vite.config.ts` with React plugin and dev proxy for `/api` to `http://localhost:5080`
- [ ] 1.3 Create `tsconfig.json` with strict TypeScript configuration for React
- [ ] 1.4 Create Tailwind CSS configuration (`tailwind.config.ts`, `postcss.config.js`, and base CSS file)
- [ ] 1.5 Create `vitest.config.ts` with jsdom environment and setup file for testing-library
- [ ] 1.6 Create `index.html` entry point and `src/main.tsx` with React root render
- [ ] 1.7 Add npm scripts: `dev`, `build`, `preview`, `test`, `lint`, `typecheck`

## Task 2: TypeScript Types

- [ ] 2.1 Create `src/types/index.ts` with `WorkflowStage`, `WorkflowState`, `AgentStatus`, `IncomingEmail`, and `ApiError` type definitions matching the backend DTOs

## Task 3: API Client

- [ ] 3.1 Create `src/api/client.ts` with typed functions: `submitEmail`, `fetchIssues`, `fetchIssue`, `fetchAgents`
- [ ] 3.2 Implement error handling that throws `ApiError` on non-2xx responses
- [ ] 3.3 Create `src/__tests__/client.test.ts` with unit tests for all API client functions (success and error cases)

## Task 4: SSE Client

- [ ] 4.1 Create `src/api/sse.ts` with `createSSEConnection` factory function that wraps EventSource and parses JSON messages
- [ ] 4.2 Create `src/hooks/useSSE.ts` hook that manages EventSource lifecycle (connect on mount, close on unmount) and exposes latest states and connection status
- [ ] 4.3 Create `src/__tests__/useSSE.test.ts` with tests for connection lifecycle and message parsing

## Task 5: Custom Hooks — Issues and Agents

- [ ] 5.1 Create `src/hooks/useIssues.ts` that fetches initial issues, subscribes to SSE, and merges updates by issueId
- [ ] 5.2 Create `src/hooks/useAgents.ts` that fetches agent statuses on mount and polls at a configurable interval
- [ ] 5.3 Create `src/hooks/useEmailSubmit.ts` that manages submission state (idle, submitting, success, error) and calls the API client
- [ ] 5.4 Create `src/__tests__/useIssues.test.ts` with tests for initial fetch, SSE merge, and error handling
- [ ] 5.5 Create `src/__tests__/useAgents.test.ts` with tests for initial fetch, polling, and cleanup
- [ ] 5.6 Create `src/__tests__/useEmailSubmit.test.ts` with tests for submission flow, success, and error states

## Task 6: Pipeline Visualizer Component

- [ ] 6.1 Create `src/components/PipelineVisualizer.tsx` using React Flow with pre-defined nodes for each WorkflowStage and edges representing the flow
- [ ] 6.2 Implement stage highlighting based on a selected WorkflowState prop (active = blue, completed = green, error = red, inactive = gray)
- [ ] 6.3 Add terminal/error stage nodes (ClassifiedOutOfScope, Failed, ManualReviewRequired) as branching paths

## Task 7: Email Composer Component

- [ ] 7.1 Create `src/components/EmailComposer.tsx` with controlled inputs for sender, subject, and body
- [ ] 7.2 Implement client-side validation (subject and body non-empty) with inline error messages
- [ ] 7.3 Integrate `useEmailSubmit` hook for submission with loading state, success notification, and error display
- [ ] 7.4 Create `src/__tests__/EmailComposer.test.tsx` with tests for validation, submission, success, and error states

## Task 8: Issues List Component

- [ ] 8.1 Create `src/components/IssuesList.tsx` with table displaying Issue ID, Stage, Detail, and Last Updated columns
- [ ] 8.2 Implement relative time formatting for the Last Updated column
- [ ] 8.3 Add color-coded stage badges (red for terminal, green for completed, blue for in-progress)
- [ ] 8.4 Add row click handler that calls `onSelectIssue` callback
- [ ] 8.5 Create `src/__tests__/IssuesList.test.tsx` with tests for rendering, stage coloring, and row selection

## Task 9: Agent Monitor Component

- [ ] 9.1 Create `src/components/AgentMonitor.tsx` with card grid displaying agent ID, status badge, and last action
- [ ] 9.2 Implement color-coded status badges (green = Idle, yellow = Working, gray = unknown)
- [ ] 9.3 Create `src/__tests__/AgentMonitor.test.tsx` with tests for rendering and status badge colors

## Task 10: Event Log Component

- [ ] 10.1 Create `src/components/EventLog.tsx` with scrollable list showing truncated issue ID, stage, detail, and timestamp
- [ ] 10.2 Implement 100-event cap (newest first, drop oldest when exceeding limit)
- [ ] 10.3 Implement auto-scroll to top on new events unless user has scrolled down
- [ ] 10.4 Create `src/__tests__/EventLog.test.tsx` with tests for rendering, ordering, and event cap

## Task 11: Application Layout and Root Component

- [ ] 11.1 Create `src/components/Layout.tsx` with header ("AI Support Workflow Dashboard") and responsive grid sections
- [ ] 11.2 Create `src/App.tsx` composing all components with hooks, wiring data flow between Pipeline Visualizer and Issues List (selected issue)
- [ ] 11.3 Apply Tailwind responsive utilities for 1024px–1920px viewport support

## Task 12: CI/CD Pipeline

- [ ] 12.1 Create `.github/workflows/dashboard-ci.yml` with path filter on `dashboard/**`, running lint, typecheck, test, and build steps
- [ ] 12.2 Update existing backend CI workflow to add path filter on `src/**` and `tests/**`

## Task 13: Property-Based Tests

- [ ] 13.1 Create property test: Event Log cap invariant — for any number of events > 100, displayed list length is exactly 100 and contains the most recent events
- [ ] 13.2 Create property test: Issues merge idempotence — merging the same SSE update array twice produces identical state to merging once
- [ ] 13.3 Create property test: Email validation completeness — form allows submission iff both subject.trim() and body.trim() are non-empty
