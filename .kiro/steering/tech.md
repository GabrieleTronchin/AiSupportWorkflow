# Tech Stack & Build

## Runtime & Language

- .NET 10.0 (`net10.0`)
- C# with `LangVersion: latest`
- Nullable reference types enabled
- Implicit usings enabled

## Key Libraries — Backend

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Agents.AI | 1.27.0 | AI orchestration and LLM integration |
| Microsoft.Agents.AI.OpenAI | 1.27.0 | OpenAI connector |
| Microsoft.Agents.AI.Workflows | 1.27.0 | Workflow orchestration (WorkflowBuilder, Executors) |
| Microsoft.EntityFrameworkCore | 10.0.14 | ORM and persistence abstraction |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.14 | In-memory database provider |
| Grpc.AspNetCore | 2.85.0 | gRPC server with ASP.NET Core integration |
| Grpc.AspNetCore.Web | 2.85.0 | gRPC-Web protocol support for browser clients |
| xunit.v3 | 4.0.2 | Unit testing framework (runs on Microsoft.Testing.Platform, see root `global.json`) |
| FsCheck.Xunit.v3 | 3.6.0 | Property-based testing |
| NSubstitute | 6.2.0 | Mocking library for unit tests |
| coverlet.MTP | 10.1.0 | Code coverage |

## Key Libraries — Frontend (dashboard)

Node.js >= 20.19 required.

| Package | Purpose |
|---------|---------|
| React 19 | UI framework |
| TypeScript | Type-safe development |
| Vite | Build tool and dev server |
| Tailwind CSS | Utility-first styling |
| @xyflow/react (ReactFlow) | Pipeline graph visualization |
| @connectrpc/connect-web | gRPC-Web client for real-time streaming |
| fast-check | Property-based testing (frontend) |
| Vitest | Test runner |

## Package Source

Single source: nuget.org (configured in `nuget.config`)

## Common Commands

```bash
# Build the entire solution
dotnet build AiSupportWorkflow.sln

# Run all backend tests (Microsoft.Testing.Platform mode)
dotnet test --solution AiSupportWorkflow.sln

# Run unit tests only
dotnet test --project tests/AiSupportWorkflow.UnitTests

# Run property-based tests only
dotnet test --project tests/AiSupportWorkflow.PropertyTests

# Run the web API
dotnet run --project src/AiSupportWorkflow.Presentation

# Frontend — install dependencies
cd dashboard && npm install

# Frontend — run dev server
cd dashboard && npm run dev

# Frontend — run tests
cd dashboard && npm run test -- --run

# Frontend — type check
cd dashboard && npm run typecheck
```

## Configuration

- LLM settings under `LlmProvider` (`Provider`: `Demo` | `OpenAI`, `ApiKey`, `ModelName`, optional `Endpoint` for any OpenAI-compatible server, `DemoLatencyMs`); the API key goes in the git-ignored `appsettings.Development.json`
- `Provider` defaults to `Demo` (in code and `appsettings.json`): `DemoChatClient` simulates LLM responses, no API key needed. Override with `OpenAI` to use a real model. See `docs/demo-mode.md`
- `GET /api/support/config` exposes `demoMode`, `llmProvider`, `llmModel` (never the API key/endpoint); the dashboard shows them as a badge
- Model name configurable in `appsettings.json` (defaults to `gpt-4o-mini`)
- Teams, agents, personas, and visualization toggle in `appsettings.json` under `Workflow` section
- `InboxPollingIntervalSeconds` controls the inbox processor polling frequency (default: 5)
- `EnableVisualization` enables gRPC streaming and agents endpoint

## Code Style (.editorconfig enforced)

- File-scoped namespaces (enforced as error)
- 4-space indentation, LF line endings, final newline required
- PascalCase for public members, `_camelCase` for private fields
- Prefer `var` when type is apparent
- Prefer pattern matching, switch expressions, and expression-bodied members
- Prefer target-typed `new()` when type is apparent
- Prefer collection expressions over traditional syntax
