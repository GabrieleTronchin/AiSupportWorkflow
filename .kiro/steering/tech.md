# Tech Stack & Build

## Runtime & Language

- .NET 10.0 (`net10.0`)
- C# with `LangVersion: latest`
- Nullable reference types enabled
- Implicit usings enabled

## Key Libraries — Backend

| Package | Version | Purpose |
|---------|---------|---------|
| Akka.NET | 1.5.67 | Actor model for agent lifecycle and message passing |
| Akka.Hosting | 1.5.67 | Actor system integration with .NET hosting |
| Microsoft.Agents.AI | 1.3.0 | AI orchestration and LLM integration |
| Microsoft.Agents.AI.OpenAI | 1.3.0 | OpenAI connector |
| Microsoft.EntityFrameworkCore | 10.0.7 | ORM and persistence abstraction |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.7 | In-memory database provider |
| Grpc.AspNetCore | 2.71.0 | gRPC server with ASP.NET Core integration |
| Grpc.AspNetCore.Web | 2.71.0 | gRPC-Web protocol support for browser clients |
| xUnit | 2.9.3 | Unit testing framework |
| FsCheck.Xunit | 3.3.3 | Property-based testing |
| NSubstitute | 5.3.0 | Mocking library for unit tests |
| Akka.TestKit.Xunit2 | 1.5.67 | Actor testing utilities |
| coverlet.collector | 10.0.0 | Code coverage |

## Key Libraries — Frontend (dashboard)

| Package | Purpose |
|---------|---------|
| React 18 | UI framework |
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

# Run all backend tests
dotnet test AiSupportWorkflow.sln

# Run unit tests only
dotnet test tests/AiSupportWorkflow.UnitTests

# Run property-based tests only
dotnet test tests/AiSupportWorkflow.PropertyTests

# Run the web API
dotnet run --project src/AiSupportWorkflow.Presentation

# Frontend — install dependencies
cd dashboard && npm install

# Frontend — run dev server
cd dashboard && npm run dev

# Frontend — run tests
cd dashboard && npx vitest --run

# Frontend — type check
cd dashboard && npx tsc --noEmit
```

## Configuration

- `OPENAI_API_KEY` environment variable required at runtime
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
