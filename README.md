# AI Support Workflow

![CI](https://github.com/GabrieleTronchin/AiSupportWorkflow/actions/workflows/ci.yml/badge.svg)
![Dashboard CI](https://github.com/GabrieleTronchin/AiSupportWorkflow/actions/workflows/dashboard-ci.yml/badge.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet)
![React](https://img.shields.io/badge/React-18-61dafb?logo=react)
![TypeScript](https://img.shields.io/badge/TypeScript-5-3178c6?logo=typescript&logoColor=white)
![License](https://img.shields.io/github/license/GabrieleTronchin/AiSupportWorkflow)

**A spec-driven AI experiment — built entirely by AI using [Kiro](https://kiro.dev).**

An AI-driven technical support workflow built with .NET 10. This project simulates the full lifecycle of a support request — from email intake to automated code fix generation — using LLM-powered agents orchestrated through an actor-based architecture.

> **Note:** This codebase was generated with AI.

---

## What It Does

The system automates technical support by processing incoming emails through a multi-stage AI pipeline:

1. **Email Reception** — A support email is submitted via the REST API with a sender, subject, and body.
2. **LLM Classification** — The email is analyzed by an LLM to determine whether it describes a code-related issue and to categorize it (backend bug, frontend bug, or quality/test issue). Out-of-scope emails are rejected here.
3. **Team Routing** — The email text is matched against known applications (Application A, Application B) to route the issue to the correct team.
4. **Agent Assignment** — Based on the issue category, a specialized AI agent is selected (backend developer, frontend developer, or QA engineer).
5. **Root Cause Analysis** — The assigned agent, running as an Akka.NET actor, performs LLM-powered analysis to identify the root cause and produce a resolution report.
6. **Human Approval Gate** — The resolution report is held for human review. The workflow pauses in an `AwaitingApproval` state until a human approves or rejects the proposed fix. Rejected issues move to `ManualReviewRequired`.
7. **Code Fix Generation** — Once approved, a simulated pull request is generated with the proposed fix, including affected file paths and a diff.

Each agent operates as an independent actor under a supervisor, and the full pipeline state is tracked and queryable through the API.

The system includes a **real-time monitoring dashboard** (React + gRPC-Web) that visualizes the pipeline state, agent activity, and event history. Email processing is fully asynchronous via the **Transactional Inbox pattern** — submissions return immediately (HTTP 202) while a background processor handles the workflow pipeline.

---

## Architecture

The project follows Clean Architecture with a strict inward dependency flow, combined with an actor-based workflow pipeline for processing support requests.

### Clean Architecture Layers

Dependencies flow inward — each layer only depends on the layer closer to the core. The Domain layer has zero external dependencies.

```mermaid
graph LR
    subgraph Presentation
        P[Minimal API<br/>Endpoints<br/>Program.cs<br/>gRPC Service]
    end
    subgraph Infrastructure
        I[Akka.NET Actors<br/>Agent Framework Services<br/>EF Core InMemory<br/>InboxProcessor<br/>WorkflowUpdateChannel]
    end
    subgraph Application
        A[Orchestrator<br/>EmailProcessor<br/>AgentSelector / TeamRouter<br/>UseCases]
    end
    subgraph Domain
        D[Entities / Enums<br/>Interfaces / Messages<br/>Value Objects]
    end

    P --> I --> A --> D
```

### Workflow Pipeline

Each support email flows through a multi-stage AI pipeline. Out-of-scope emails are rejected at classification; code-related issues proceed through routing, assignment, analysis, and fix generation.

```mermaid
flowchart LR
    E[📧 Email Received] --> C[🔍 LLM Classification]
    C -->|Out of Scope| OOS[⛔ Rejected]
    C -->|Code-Related| TR[🔀 Team Routing]
    TR --> AS[👤 Agent Assignment]
    AS --> R[🧠 Root Cause Analysis]
    R --> HA[✋ Human Approval Gate]
    HA -->|Approved| CG[💻 Code Fix Generation]
    HA -->|Rejected| MR[📋 Manual Review]
```

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
| `POST` | `/api/support/issues/{id:guid}/abort` | Abort a workflow, forcing it into Failed state |
| gRPC | `WorkflowMonitor.SubscribeToUpdates` | Server streaming for real-time workflow updates |

📄 [Full API reference with request/response examples →](docs/api-endpoints.md)

---

## Project Structure

```
AiSupportWorkflow/
├── backend/                                 # .NET backend (solution, source, tests)
│   ├── AiSupportWorkflow.sln               # Solution file
│   ├── src/
│   │   ├── AiSupportWorkflow.Domain/       # Pure domain layer — entities, enums, interfaces, value objects, messages
│   │   ├── AiSupportWorkflow.Application/  # Business logic — orchestrator, services, use cases, configuration
│   │   ├── AiSupportWorkflow.Infrastructure/ # External integrations — Akka.NET actors, Agent Framework, services
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
| [Actor Architecture](docs/actor-architecture.md) | Akka.NET actor system, supervision strategy, and message routing |
| [Agent Framework Integration](docs/agent-framework-integration.md) | LLM-backed services for classification, resolution, and code generation |
| [API Endpoints](docs/api-endpoints.md) | Full API reference with request/response examples |
| [Debugging](docs/debugging.md) | HTTP file for IDE-based testing and PowerShell monitor script |
| [Dashboard](docs/dashboard.md) | Real-time monitoring dashboard architecture and usage |
| [Transactional Inbox](docs/transactional-inbox.md) | Async email processing pattern and implementation |


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
