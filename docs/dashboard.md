# Dashboard

> **📚 Navigation:** [← Back to README](../README.md)

## Overview

The AI Support Workflow includes a real-time monitoring dashboard that provides visibility into the workflow pipeline. It visualizes issue progression through stages, agent activity, the event audit log, and the inbox queue — all updating in real-time via gRPC-Web streaming and REST polling.

---

## Tech Stack

| Technology | Purpose |
|------------|---------|
| React 18 | UI framework |
| TypeScript | Type-safe development |
| Vite | Build tool and dev server |
| Tailwind CSS | Utility-first styling |
| ReactFlow (@xyflow/react) | Pipeline graph visualization |
| gRPC-Web (@connectrpc/connect-web) | Real-time streaming from the backend |

---

## Pages

### Overview

The main dashboard page combining:

- **Pipeline Graph** — A fixed ReactFlow visualization showing the workflow stages. Nodes animate and change color as issues progress through the pipeline.
- **Email Composer** — A form to submit test emails directly from the dashboard. Submissions are asynchronous (HTTP 202) and progress is visible immediately in the graph.
- **Summary Statistics** — Total issues, active agents, and recent failures displayed at the top.

### Issues

A filterable table showing all processed issues with their current state:

- Issue ID, current stage, detail, and last update timestamp
- Filter by workflow stage

### Event Log

A persistent audit log of all state transition events (max 200 entries):

- Issue ID, previous stage, new stage, timestamp, and detail
- Reverse chronological order (most recent first)
- Data persists across page refreshes (backed by the Events table)

### Agents

Displays all configured agents from `appsettings.json` with their current status:

- Agent identifier, team, role, and status (Idle or Working)
- Updates via periodic polling

### Inbox

Email queue monitoring page (replaces the old "/emails" page):

- Table of all inbox messages with ID, sender, subject, status, timestamps
- Status badges: Queued (amber), Processed (green), Failed (red)
- Filter by status (Queued, Processed, Failed, All)
- Summary counters at the top

---

## Backend Connection

The dashboard connects to the .NET backend through two channels:

1. **gRPC-Web Streaming** — Real-time workflow state updates via `WorkflowMonitor.SubscribeToUpdates` server streaming RPC. The client automatically reconnects on disconnection and shows a status indicator.

2. **REST Polling** — Periodic fetches to REST endpoints for agents, issues, events, and inbox data. Used for data that doesn't require sub-second updates.

---

## Getting Started

### Prerequisites

- Node.js 18+
- The .NET backend running (see [main README](../README.md))

### Installation and Development

```bash
cd dashboard
npm install
npm run dev
```

The dashboard will be available at `http://localhost:5173`.

The Vite dev server proxies API requests to the backend at `http://localhost:5000`.

---

## Project Structure

```
dashboard/
├── index.html                    # HTML entry point
├── package.json                  # Dependencies and scripts
├── vite.config.ts                # Vite configuration with API proxy
├── tailwind.config.ts            # Tailwind CSS configuration
├── tsconfig.json                 # TypeScript configuration
├── postcss.config.js             # PostCSS configuration
├── vitest.config.ts              # Test configuration
│
├── src/
│   ├── main.tsx                  # React entry point
│   ├── App.tsx                   # Router and layout
│   ├── index.css                 # Global styles (Tailwind imports)
│   │
│   ├── api/
│   │   ├── client.ts             # REST API client
│   │   └── grpc-client.ts        # gRPC-Web client setup
│   │
│   ├── components/
│   │   ├── PipelineVisualizer.tsx  # ReactFlow pipeline graph
│   │   ├── EmailComposer.tsx       # Email submission form
│   │   ├── AgentMonitor.tsx        # Agent status display
│   │   ├── EventLog.tsx            # Event log table
│   │   ├── IssuesList.tsx          # Issues table
│   │   └── layout/                 # Navigation and layout components
│   │
│   ├── hooks/
│   │   ├── useGrpcStream.ts      # gRPC-Web streaming hook
│   │   ├── useAgents.ts          # Agents polling hook
│   │   ├── useEvents.ts          # Events polling hook
│   │   ├── useInbox.ts           # Inbox polling hook
│   │   ├── useIssues.ts          # Issues polling hook
│   │   └── useEmailSubmit.ts     # Email submission hook
│   │
│   ├── pages/
│   │   ├── OverviewPage.tsx      # Overview with graph + email form
│   │   ├── IssuesPage.tsx        # Issues table page
│   │   ├── EventLogPage.tsx      # Event log page
│   │   ├── AgentsPage.tsx        # Agents status page
│   │   └── InboxPage.tsx         # Inbox queue page
│   │
│   ├── types/
│   │   └── index.ts              # Shared TypeScript types
│   │
│   └── __tests__/                # Unit and property-based tests
│       ├── properties.test.ts    # fast-check property tests
│       └── *.test.ts(x)          # Component and hook tests
│
└── dist/                         # Production build output
```
