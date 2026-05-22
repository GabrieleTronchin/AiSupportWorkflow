# AI Support Workflow

![CI](https://github.com/GabrieleTronchin/AiSupportWorkflow/actions/workflows/ci.yml/badge.svg)
![Dashboard CI](https://github.com/GabrieleTronchin/AiSupportWorkflow/actions/workflows/dashboard-ci.yml/badge.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet)
![React](https://img.shields.io/badge/React-18-61dafb?logo=react)
![TypeScript](https://img.shields.io/badge/TypeScript-5-3178c6?logo=typescript&logoColor=white)
![License](https://img.shields.io/github/license/GabrieleTronchin/AiSupportWorkflow)

**A spec-driven AI experiment.**

An AI-driven technical support workflow built with .NET 10. This project simulates the full lifecycle of a support request — from email intake to automated code fix generation — using LLM-powered agents orchestrated through **Microsoft Agent Framework Workflows** with a human-in-the-loop approval gate.

---

## What It Does

The system automates technical support by processing incoming emails through a multi-stage AI pipeline:

1. **Email Reception** — A support email is submitted via the REST API with a sender, subject, and body.
2. **LLM Classification** — The email is analyzed by an LLM to determine whether it describes a code-related issue and to categorize it (backend bug, frontend bug, or quality/test issue). Out-of-scope emails are rejected here.
3. **Team Routing** — The email text is matched against known applications (Application A, Application B) to route the issue to the correct team.
4. **Agent Assignment** — Based on the issue category, a specialized AI agent is selected (backend developer, frontend developer, or QA engineer).
5. **Root Cause Analysis** — The assigned agent performs LLM-powered analysis to identify the root cause and produce a resolution report.
6. **Human Approval Gate** — The resolution report is held for human review. The workflow pauses in an `AwaitingApproval` state until a human approves or rejects the proposed fix via the dashboard. Rejected issues move to `ManualReviewRequired`.
7. **Code Fix Generation** — Once approved, a simulated pull request is generated with the proposed fix, including affected file paths and a diff.

Each stage is implemented as a discrete **Executor** in the Microsoft Agent Framework Workflows graph. The pipeline is defined declaratively using `WorkflowBuilder` with typed edges and conditions, replacing the previous Akka.NET actor hierarchy.

The system includes a **real-time monitoring dashboard** (React + gRPC-Web) that visualizes the pipeline state, agent activity, and event history. Email processing is fully asynchronous via the **Transactional Inbox pattern** — submissions return immediately (HTTP 202) while a background processor handles the workflow pipeline.

---

## Architecture

The project follows Clean Architecture with a strict inward dependency flow, combined with a **declarative workflow graph** (Microsoft Agent Framework) for processing support requests.

### Clean Architecture Layers

Dependencies flow inward — each layer only depends on the layer closer to the core. The Domain layer has zero external dependencies.

```mermaid
graph LR
    subgraph Presentation
        P[Minimal API<br/>Endpoints<br/>Program.cs<br/>gRPC Service]
    end
    subgraph Infrastructure
        I[Workflow Engine<br/>Executors<br/>Agent Framework Services<br/>EF Core InMemory<br/>InboxProcessor]
    end
    subgraph Application
        A[EmailProcessor<br/>AgentSelector / TeamRouter<br/>UseCases]
    end
    subgraph Domain
        D[Entities / Enums<br/>Interfaces<br/>Value Objects]
    end

    P --> I --> A --> D
```

### Workflow Pipeline (Microsoft Agent Framework)

The orchestration uses `WorkflowBuilder` to define a directed graph of **Executors** connected by typed **Edges**. Each executor handles a single stage, receives typed input, and returns typed output that the framework routes to the next executor.

```mermaid
flowchart LR
    E[📧 Email Received] --> C[🔍 Classification<br/>Executor]
    C -->|Out of Scope| OOS[⛔ Rejected]
    C -->|Code-Related| TR[🔀 TeamAssignment<br/>Executor]
    TR --> AS[👤 AgentAssignment<br/>Executor]
    AS --> R[🧠 Resolution<br/>Executor]
    R --> HA[✋ HumanApproval<br/>Gate]
    HA -->|Approved| CG[💻 CodeGeneration<br/>Executor]
    HA -->|Rejected| MR[📋 Manual Review]
```

### Human-in-the-Loop Approval

The `HumanApprovalGateExecutor` pauses the workflow after resolution and waits for an external decision:

1. The resolution report (root cause, severity, proposed fix) is exposed via `GET /api/support/approvals/pending`
2. The dashboard shows a notification banner and the Approvals page with full details
3. A human reviews and clicks **Approve** or **Reject**
4. On approval → the workflow resumes and generates the code fix
5. On rejection → the workflow transitions to `ManualReviewRequired` (terminal state)
6. Stuck workflows can be force-aborted via the **Abort** button (`POST /api/support/issues/{id}/abort`)

This gate ensures no automated code changes are generated without human oversight.

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10, C# latest |
| Orchestration | Microsoft Agent Framework Workflows 1.3.0 (`WorkflowBuilder`, `Executor`, typed edges) |
| LLM Integration | Microsoft.Extensions.AI structured output (`GetResponseAsync<T>`) |
| Persistence | EF Core 10 InMemory (ready for SQL migration) |
| Real-time | gRPC server streaming + gRPC-Web |
| Frontend | React 18, TypeScript, Vite, Tailwind CSS, ReactFlow |
| Testing | xUnit, FsCheck (property-based), NSubstitute, Vitest, fast-check |

---

## DummyApps & Test Scenarios

The `DummyApps/` folder contains two sample applications — **ApplicationA** and **ApplicationB** — that serve as test fixtures for the AI workflow. Each application includes source code with intentional bugs and a `BugScenarios.md` file documenting three predefined scenarios (one per issue category).

### Bug Categories

| Category | Description | Example |
|----------|-------------|---------|
| **BackendBug** | Server-side logic errors (null references, SQL injection) | App A: `NullReferenceException` in `GetOrderSummary`; App B: SQL injection in `SearchUsers` |
| **FrontendBug** | UI/component rendering issues (wrong bindings, missing null checks) | App A: incorrect property binding in `OrderSummary.razor`; App B: missing null check on avatar URL |
| **QualityTestIssue** | Missing or flaky tests that let bugs slip through | App A: missing test for empty order edge case; App B: flaky test with hardcoded date |

### Scenario Files

- [`DummyApps/ApplicationA/BugScenarios.md`](DummyApps/ApplicationA/BugScenarios.md) — Three scenarios (A1–A3) covering an order management system
- [`DummyApps/ApplicationB/BugScenarios.md`](DummyApps/ApplicationB/BugScenarios.md) — Three scenarios (B1–B3) covering a user management system


---

## API Endpoints

All endpoints are served under the `/api/support` base path.

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/support/emails` | Submit a support email (async — returns 202 Accepted) |
| `GET` | `/api/support/issues/{id:guid}` | Get workflow state by issue ID |
| `GET` | `/api/support/issues` | List all processed issues |
| `GET` | `/api/support/events` | List state transition events (persistent audit log, max 200) |
| `GET` | `/api/support/agents` | All configured agents with current status (Idle/Working) |
| `GET` | `/api/support/inbox` | Inbox queue messages with optional status filter |
| `GET` | `/api/support/approvals/pending` | List workflows awaiting human approval |
| `POST` | `/api/support/approvals/{id:guid}/approve` | Approve a workflow — resumes to code generation |
| `POST` | `/api/support/approvals/{id:guid}/reject` | Reject a workflow — moves to ManualReviewRequired |
| `POST` | `/api/support/issues/{id:guid}/abort` | Abort a workflow, forcing it into Failed state |
| `GET` | `/api/support/agents/{agentId}/telemetry` | Agent-specific LLM telemetry (tokens, latency) |
| `GET` | `/api/support/telemetry/summary` | Global LLM usage statistics |
| gRPC | `WorkflowMonitor.SubscribeToUpdates` | Server streaming for real-time workflow updates |

📄 [Full API reference with request/response examples →](docs/api-endpoints.md)

---

## Project Structure

```
AiSupportWorkflow/
├── backend/                                 # .NET backend (solution, source, tests)
│   ├── AiSupportWorkflow.sln               # Solution file
│   ├── src/
│   │   ├── AiSupportWorkflow.Domain/       # Pure domain layer — entities, enums, interfaces, value objects
│   │   ├── AiSupportWorkflow.Application/  # Business logic — services, use cases, configuration
│   │   ├── AiSupportWorkflow.Infrastructure/ # External integrations — Workflow Engine, Agent Framework, EF Core
│   │   │   ├── WorkflowEngine/             # Executors, SupportWorkflowFactory, WorkflowOrchestrator
│   │   │   ├── AgentFramework/             # ChatClientAgentFactory, LLM Telemetry Middleware
│   │   │   ├── Persistence/               # EF Core InMemory, StateTracker, CheckpointStore
│   │   │   └── Services/                  # InboxProcessor, WorkflowApprovalService
│   │   └── AiSupportWorkflow.Presentation/ # REST API & composition root — Minimal API endpoints, Program.cs
│   └── tests/
│       ├── AiSupportWorkflow.UnitTests/    # xUnit + NSubstitute unit tests
│       └── AiSupportWorkflow.PropertyTests/ # FsCheck property-based tests
│
├── dashboard/                               # React monitoring dashboard (Vite + TypeScript + Tailwind)
│
├── DummyApps/
│   ├── ApplicationA/                        # Sample app with predefined bug scenarios
│   └── ApplicationB/                        # Sample app with predefined bug scenarios
│
├── docs/                                    # In-depth documentation
└── README.md
```

---

## Deep-Dive Documentation

| Document | Description |
|----------|-------------|
| [Clean Architecture](docs/clean-architecture.md) | Four-layer structure, dependency rules, and compliance verification |
| [Workflow Engine](docs/agent-framework-integration.md) | Microsoft Agent Framework Workflows — executors, edges, and structured output |
| [Human Approval Gate](docs/human-approval-gate.md) | Human-in-the-loop design, approval API, and dashboard integration |
| [API Endpoints](docs/api-endpoints.md) | Full API reference with request/response examples |
| [Dashboard](docs/dashboard.md) | Real-time monitoring dashboard architecture and usage |
| [Transactional Inbox](docs/transactional-inbox.md) | Async email processing pattern and implementation |
| [Debugging](docs/debugging.md) | HTTP file for IDE-based testing and PowerShell monitor script |


---

## Getting Started

1. **Clone the repository:**

   ```bash
   git clone https://github.com/your-username/AiSupportWorkflow.git
   cd AiSupportWorkflow
   ```

2. **Configure your OpenAI API key:**

   Create the file `backend/src/AiSupportWorkflow.Presentation/appsettings.Development.json`:

   ```json
   {
     "LlmProvider": {
       "ApiKey": "YOUR_API_KEY_HERE",
       "Provider": "OpenAI",
       "ModelName": "gpt-4o-mini"
     }
   }
   ```

   This file is git-ignored and will not be committed.

3. **Run the project:**

   ```bash
   dotnet run --project backend/src/AiSupportWorkflow.Presentation
   ```

   The API will be available at `http://localhost:5000` (or the port configured in `launchSettings.json`).

4. **Start the dashboard (optional):**

   ```bash
   cd dashboard
   npm install
   npm run dev
   ```

   The dashboard will be available at `http://localhost:5173`.

### Configuration

The `Workflow` section in `appsettings.json` controls runtime behavior:

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `EnableVisualization` | `bool` | `true` | Enables gRPC streaming and agents endpoint |
| `SequentialProcessing` | `bool` | `false` | When true, processes one inbox message per cycle and waits for the previous issue to reach a terminal state before processing the next |
| `InboxPollingIntervalSeconds` | `int` | `5` | Polling interval for the inbox processor background service |
| `Teams` | `array` | — | Team and agent configuration |

### Verbose Logging

Set the `AiSupportWorkflow` log level to `Debug` in `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "AiSupportWorkflow": "Debug"
    }
  }
}
```

---

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
