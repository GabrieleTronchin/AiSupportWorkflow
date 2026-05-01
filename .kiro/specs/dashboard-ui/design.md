# Design Document

## Overview

The Dashboard UI is a standalone React 18 + TypeScript single-page application built with Vite, located in `dashboard/` at the repository root. It communicates with the existing .NET backend API via REST and SSE to provide real-time monitoring of the AI Support Workflow pipeline.

The architecture follows a strict separation: custom hooks encapsulate all business logic and data fetching, while components are purely presentational. This enables independent testability of logic and UI.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    App (Layout)                          │
├─────────────────────────────────────────────────────────┤
│  ┌──────────┐ ┌──────────┐ ┌────────┐ ┌─────────────┐  │
│  │ Pipeline │ │  Email   │ │ Issues │ │    Agent    │  │
│  │Visualizer│ │ Composer │ │  List  │ │   Monitor   │  │
│  └────┬─────┘ └────┬─────┘ └───┬────┘ └──────┬──────┘  │
│       │             │           │              │         │
│  ┌────┴─────────────┴───────────┴──────────────┴──────┐  │
│  │              Custom Hooks Layer                     │  │
│  │  useIssues | useAgents | useSSE | useEmailSubmit   │  │
│  └────────────────────────┬───────────────────────────┘  │
│                           │                              │
│  ┌────────────────────────┴───────────────────────────┐  │
│  │              API Client + SSE Client               │  │
│  └────────────────────────┬───────────────────────────┘  │
└───────────────────────────┼──────────────────────────────┘
                            │ HTTP / SSE
                            ▼
              ┌──────────────────────────┐
              │   .NET Backend (5080)    │
              │  /api/support/*          │
              └──────────────────────────┘
```

## Project Structure

```
dashboard/
├── src/
│   ├── api/
│   │   ├── client.ts          # Typed fetch wrapper with error handling
│   │   └── sse.ts             # EventSource wrapper
│   ├── components/
│   │   ├── Layout.tsx         # App shell with header and sections
│   │   ├── PipelineVisualizer.tsx  # React Flow diagram
│   │   ├── EmailComposer.tsx  # Email submission form
│   │   ├── IssuesList.tsx     # Issues table
│   │   ├── AgentMonitor.tsx   # Agent status cards
│   │   └── EventLog.tsx       # Real-time event feed
│   ├── hooks/
│   │   ├── useIssues.ts       # Issues state + SSE updates
│   │   ├── useAgents.ts       # Agent polling
│   │   ├── useSSE.ts          # SSE connection lifecycle
│   │   └── useEmailSubmit.ts  # Email form submission logic
│   ├── types/
│   │   └── index.ts           # TypeScript interfaces matching backend DTOs
│   ├── __tests__/
│   │   ├── client.test.ts     # API client tests
│   │   ├── useIssues.test.ts  # Hook tests
│   │   ├── useAgents.test.ts
│   │   ├── useSSE.test.ts
│   │   ├── useEmailSubmit.test.ts
│   │   ├── EmailComposer.test.tsx
│   │   ├── IssuesList.test.tsx
│   │   ├── AgentMonitor.test.tsx
│   │   └── EventLog.test.tsx
│   ├── App.tsx                # Root component composing all sections
│   └── main.tsx               # Entry point
├── index.html
├── vite.config.ts
├── tsconfig.json
├── tailwind.config.ts
├── postcss.config.js
├── package.json
└── vitest.config.ts
```

## Component Specifications

### TypeScript Types (`src/types/index.ts`)

```typescript
export type WorkflowStage =
  | 'Received'
  | 'Classified'
  | 'ClassifiedOutOfScope'
  | 'TeamAssigned'
  | 'AgentAssigned'
  | 'Resolving'
  | 'Resolved'
  | 'CodeChangeGenerated'
  | 'Failed'
  | 'ManualReviewRequired';

export interface WorkflowState {
  issueId: string;
  stage: WorkflowStage;
  lastUpdated: string; // ISO 8601
  detail: string | null;
}

export interface AgentStatus {
  agentId: string;
  status: string;
  lastAction: string | null;
}

export interface IncomingEmail {
  sender: string;
  subject: string;
  body: string;
}

export interface ApiError {
  statusCode: number;
  message: string;
}
```

### API Client (`src/api/client.ts`)

A thin typed wrapper around `fetch`:

- `submitEmail(email: IncomingEmail): Promise<unknown>` — POST `/api/support/emails`
- `fetchIssues(): Promise<WorkflowState[]>` — GET `/api/support/issues`
- `fetchIssue(id: string): Promise<WorkflowState>` — GET `/api/support/issues/{id}`
- `fetchAgents(): Promise<AgentStatus[]>` — GET `/api/support/agents`

On non-2xx responses, throws an `ApiError` with status code and parsed error message.

### SSE Client (`src/api/sse.ts`)

A factory function that creates an `EventSource` connection:

- `createSSEConnection(url: string, onMessage: (states: WorkflowState[]) => void, onError?: (event: Event) => void): { close: () => void }`

Parses each `data:` line as `JSON` into `WorkflowState[]`.

### Hooks

#### `useSSE(url: string)`
- Manages EventSource lifecycle (connect on mount, close on unmount)
- Returns `{ latestStates: WorkflowState[], isConnected: boolean }`
- Internally uses `createSSEConnection`

#### `useIssues()`
- Fetches initial issues via `fetchIssues()` on mount
- Subscribes to SSE via `useSSE('/api/support/stream')`
- Merges SSE updates into local state (upsert by issueId)
- Returns `{ issues: WorkflowState[], isLoading: boolean, error: ApiError | null }`

#### `useAgents(pollInterval?: number)`
- Fetches agent statuses on mount and polls every `pollInterval` ms (default 5000)
- Returns `{ agents: AgentStatus[], isLoading: boolean, error: ApiError | null }`
- Cleans up interval on unmount

#### `useEmailSubmit()`
- Manages submission state machine: idle → submitting → success/error
- Returns `{ submit: (email: IncomingEmail) => Promise<void>, isSubmitting: boolean, isSuccess: boolean, error: ApiError | null, reset: () => void }`

### Components

All components are presentational — they receive data from hooks and render UI.

#### `Layout`
- Renders header with title "AI Support Workflow Dashboard"
- Renders children in a responsive grid layout (CSS Grid / Tailwind)

#### `PipelineVisualizer`
- Props: `{ selectedIssue?: WorkflowState }`
- Uses React Flow with pre-defined node positions for the workflow DAG
- Nodes: one per WorkflowStage, edges connecting the flow
- Highlights the node matching `selectedIssue.stage`
- Color scheme: gray (inactive), blue (active/current), green (completed), red (error/terminal)

#### `EmailComposer`
- Uses `useEmailSubmit()` hook internally
- Three controlled inputs (sender, subject, body)
- Client-side validation: subject and body must be non-empty
- Shows inline validation errors, loading spinner on submit, success/error toast

#### `IssuesList`
- Props: `{ issues: WorkflowState[], onSelectIssue: (issue: WorkflowState) => void }`
- Renders a table with sortable columns
- Clicking a row calls `onSelectIssue` (used to highlight in Pipeline Visualizer)
- Formats `lastUpdated` as relative time (e.g., "3 min ago")
- Color-codes stage badges: red for Failed/ClassifiedOutOfScope, green for CodeChangeGenerated, blue for in-progress

#### `AgentMonitor`
- Props: `{ agents: AgentStatus[], isLoading: boolean }`
- Renders a card grid, one card per agent
- Badge color: green for "Idle", yellow for "Working", gray for unknown

#### `EventLog`
- Props: `{ events: WorkflowState[] }`
- Renders a scrollable list, newest first
- Caps display at 100 items
- Shows truncated issueId (first 8 chars), stage badge, detail, and timestamp
- Auto-scrolls to top on new event unless user has scrolled down

## Data Flow

1. **Initial Load**: `useIssues` fetches `/api/support/issues`, `useAgents` fetches `/api/support/agents`
2. **Real-time Updates**: `useSSE` connects to `/api/support/stream`. Each SSE message contains the full current state array. `useIssues` merges this into its state.
3. **Email Submission**: `useEmailSubmit` POSTs to `/api/support/emails`. The resulting workflow state changes arrive via SSE automatically.
4. **Agent Polling**: `useAgents` polls `/api/support/agents` every 5 seconds to refresh agent cards.

## Vite Configuration

```typescript
// vite.config.ts
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: 'dist',
  },
});
```

## CI/CD Design

### `dashboard-ci.yml`
```yaml
name: Dashboard CI
on:
  push:
    paths: ['dashboard/**']
  pull_request:
    paths: ['dashboard/**']
jobs:
  build:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: dashboard
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: npm
          cache-dependency-path: dashboard/package-lock.json
      - run: npm ci
      - run: npm run lint
      - run: npm run typecheck
      - run: npm run test -- --run
      - run: npm run build
```

## Testing Strategy

### Unit Tests (Vitest)
- **API Client**: Mock `fetch`, verify correct URLs, methods, headers, error handling
- **Hooks**: Use `@testing-library/react-hooks` (`renderHook`), mock API client and EventSource
- **Components**: Use React Testing Library, pass props directly, verify rendered output

### Property-Based Tests (fast-check)
- **Event Log cap**: For any number of events pushed, displayed list never exceeds 100
- **Issues merge**: For any sequence of SSE updates, all unique issueIds are present in the final state
- **Email validation**: For any string inputs, validation correctly identifies empty vs non-empty subject/body

## Correctness Properties

1. **Event Log invariant (Req 8.4)**: For all sequences of N events (N > 100) pushed to the Event Log, the rendered list length equals exactly 100 and contains the 100 most recent events.

2. **Issues merge idempotence (Req 6.3, 6.4)**: For any WorkflowState[] received via SSE, merging the same array twice produces the same result as merging it once (idempotent upsert by issueId).

3. **Email validation completeness (Req 5.6)**: For all possible string pairs (subject, body), the form allows submission if and only if both `subject.trim().length > 0` AND `body.trim().length > 0`.

## Dependencies

```json
{
  "dependencies": {
    "react": "^18.3.0",
    "react-dom": "^18.3.0",
    "reactflow": "^11.11.0",
    "@reactflow/core": "^11.11.0"
  },
  "devDependencies": {
    "@types/react": "^18.3.0",
    "@types/react-dom": "^18.3.0",
    "@vitejs/plugin-react": "^4.3.0",
    "autoprefixer": "^10.4.0",
    "postcss": "^8.4.0",
    "tailwindcss": "^3.4.0",
    "typescript": "^5.5.0",
    "vite": "^5.4.0",
    "vitest": "^2.1.0",
    "@testing-library/react": "^16.0.0",
    "@testing-library/jest-dom": "^6.5.0",
    "jsdom": "^25.0.0",
    "fast-check": "^3.22.0"
  }
}
```
