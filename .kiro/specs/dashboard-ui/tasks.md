# Tasks

## Task 1: Project Scaffolding and Configuration

- [x] 1.1 Initialize the `dashboard/` project with `package.json` containing all required dependencies (React 18, TypeScript, Vite, Tailwind CSS, Vitest, React Testing Library, React Flow, React Router, lucide-react, fast-check)
- [x] 1.2 Create `vite.config.ts` with React plugin and dev proxy for `/api` to `http://localhost:5080`
- [x] 1.3 Create `tsconfig.json` with strict TypeScript configuration for React
- [x] 1.4 Create Tailwind CSS configuration (`tailwind.config.ts`, `postcss.config.js`, and base CSS file with dark theme as default)
- [x] 1.5 Create `vitest.config.ts` with jsdom environment and setup file for testing-library
- [x] 1.6 Create `index.html` entry point and `src/main.tsx` with React root render and BrowserRouter
- [x] 1.7 Add npm scripts: `dev`, `build`, `preview`, `test`, `lint`, `typecheck`

## Task 2: TypeScript Types

- [x] 2.1 Create `src/types/index.ts` with `WorkflowStage`, `WorkflowState`, `AgentStatus`, `IncomingEmail`, and `ApiError` type definitions matching the backend DTOs

## Task 3: API Client

- [x] 3.1 Create `src/api/client.ts` with typed functions: `submitEmail`, `fetchIssues`, `fetchIssue`, `fetchAgents`
- [x] 3.2 Implement error handling that throws `ApiError` on non-2xx responses
- [x] 3.3 Create `src/__tests__/client.test.ts` with unit tests for all API client functions (success and error cases)

## Task 4: SSE Client

- [x] 4.1 Create `src/api/sse.ts` with `createSSEConnection` factory function that wraps EventSource and parses JSON messages
- [x] 4.2 Create `src/hooks/useSSE.ts` hook that manages EventSource lifecycle (connect on mount, close on unmount) and exposes latest states and connection status
- [x] 4.3 Create `src/__tests__/useSSE.test.ts` with tests for connection lifecycle and message parsing

## Task 5: Custom Hooks — Issues and Agents

- [x] 5.1 Create `src/hooks/useIssues.ts` that fetches initial issues, subscribes to SSE, and merges updates by issueId
- [x] 5.2 Create `src/hooks/useAgents.ts` that fetches agent statuses on mount and polls at a configurable interval
- [x] 5.3 Create `src/hooks/useEmailSubmit.ts` that manages submission state (idle, submitting, success, error) and calls the API client
- [x] 5.4 Create `src/__tests__/useIssues.test.ts` with tests for initial fetch, SSE merge, and error handling
- [x] 5.5 Create `src/__tests__/useAgents.test.ts` with tests for initial fetch, polling, and cleanup
- [x] 5.6 Create `src/__tests__/useEmailSubmit.test.ts` with tests for submission flow, success, and error states

## Task 6: Pipeline Visualizer Component

- [x] 6.1 Create `src/components/PipelineVisualizer.tsx` using React Flow with pre-defined nodes for each WorkflowStage and edges representing the flow
- [x] 6.2 Implement stage highlighting based on a selected WorkflowState prop (active = blue, completed = green, error = red, inactive = gray)
- [x] 6.3 Add terminal/error stage nodes (ClassifiedOutOfScope, Failed, ManualReviewRequired) as branching paths

## Task 7: Email Composer Component

- [x] 7.1 Create `src/components/EmailComposer.tsx` with controlled inputs for sender, subject, and body
- [x] 7.2 Implement client-side validation (subject and body non-empty) with inline error messages
- [x] 7.3 Integrate `useEmailSubmit` hook for submission with loading state, success notification, and error display
- [x] 7.4 Create `src/__tests__/EmailComposer.test.tsx` with tests for validation, submission, success, and error states

## Task 8: Issues List Component

- [x] 8.1 Create `src/components/IssuesList.tsx` with table displaying Issue ID, Stage, Detail, and Last Updated columns
- [x] 8.2 Implement relative time formatting for the Last Updated column
- [x] 8.3 Add color-coded stage badges (red for terminal, green for completed, blue for in-progress)
- [x] 8.4 Add row click handler that calls `onSelectIssue` callback
- [x] 8.5 Create `src/__tests__/IssuesList.test.tsx` with tests for rendering, stage coloring, and row selection

## Task 9: Agent Monitor Component

- [x] 9.1 Create `src/components/AgentMonitor.tsx` with card grid displaying agent ID, status badge, and last action
- [x] 9.2 Implement color-coded status badges (green = Idle, yellow = Working, gray = unknown)
- [x] 9.3 Create `src/__tests__/AgentMonitor.test.tsx` with tests for rendering and status badge colors

## Task 10: Event Log Component

- [x] 10.1 Create `src/components/EventLog.tsx` with scrollable list showing truncated issue ID, stage, detail, and timestamp
- [x] 10.2 Implement 100-event cap (newest first, drop oldest when exceeding limit)
- [x] 10.3 Implement auto-scroll to top on new events unless user has scrolled down
- [x] 10.4 Create `src/__tests__/EventLog.test.tsx` with tests for rendering, ordering, and event cap

## Task 11: Application Layout, Sidebar, and Routing

- [x] 11.1 Create `src/components/layout/Sidebar.tsx` with fixed dark sidebar, navigation items (Overview, Emails, Issues, Agents, Event Log) with lucide-react icons, active route highlighting via NavLink, and collapse toggle
- [x] 11.2 Create `src/components/layout/AppLayout.tsx` as shell component with Sidebar + React Router `<Outlet />`
- [x] 11.3 Create page components: `src/pages/OverviewPage.tsx` (Pipeline Visualizer + summary cards), `src/pages/EmailsPage.tsx`, `src/pages/IssuesPage.tsx`, `src/pages/AgentsPage.tsx`, `src/pages/EventLogPage.tsx`
- [x] 11.4 Create `src/App.tsx` with React Router routes: `/` (Overview), `/emails`, `/issues`, `/agents`, `/events`, all wrapped in AppLayout
- [x] 11.5 Apply dark theme globally via Tailwind (zinc-900 background, zinc-100 text) and configure shadcn/ui dark mode CSS variables
- [x] 11.6 Apply Tailwind responsive utilities for 1024px–1920px viewport support

## Task 12: CI/CD Pipeline

- [x] 12.1 Create `.github/workflows/dashboard-ci.yml` with path filter on `dashboard/**`, running lint, typecheck, test, and build steps
- [x] 12.2 Update existing backend CI workflow to add path filter on `src/**` and `tests/**`

## Task 13: Property-Based Tests

- [x] 13.1 Create property test: Event Log cap invariant — for any number of events > 100, displayed list length is exactly 100 and contains the most recent events
- [x] 13.2 Create property test: Issues merge idempotence — merging the same SSE update array twice produces identical state to merging once
- [x] 13.3 Create property test: Email validation completeness — form allows submission iff both subject.trim() and body.trim() are non-empty
