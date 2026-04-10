# Tech Stack & Build

## Runtime & Language

- .NET 10.0 (`net10.0`)
- C# with `LangVersion: latest`
- Nullable reference types enabled
- Implicit usings enabled

## Key Libraries

| Package | Version | Purpose |
|---------|---------|---------|
| Akka.NET | 1.5.64 | Actor model for agent lifecycle and message passing |
| Akka.Hosting | 1.5.64 | Actor system integration with .NET hosting |
| Microsoft.SemanticKernel | 1.74.0 | AI orchestration and LLM integration |
| Microsoft.SemanticKernel.Connectors.OpenAI | 1.74.0 | OpenAI ChatGPT connector |
| xUnit | 2.9.3 | Unit testing framework |
| FsCheck.Xunit | 3.3.2 | Property-based testing |
| NSubstitute | 5.3.0 | Mocking library for unit tests |
| Akka.TestKit.Xunit2 | 1.5.64 | Actor testing utilities |
| coverlet.collector | 8.0.1 | Code coverage |

## Package Source

Single source: nuget.org (configured in `nuget.config`)

## Common Commands

```bash
# Build the entire solution
dotnet build AiSupportWorkflow.sln

# Run all tests
dotnet test AiSupportWorkflow.sln

# Run unit tests only
dotnet test tests/AiSupportWorkflow.UnitTests

# Run property-based tests only
dotnet test tests/AiSupportWorkflow.PropertyTests

# Run the web API
dotnet run --project src/AiSupportWorkflow.Presentation
```

## Configuration

- `OPENAI_API_KEY` environment variable required at runtime
- Model name configurable in `appsettings.json` (defaults to `gpt-4o-mini`)
- Teams, agents, personas, and visualization toggle in `appsettings.json` under `Workflow` section

## Code Style (.editorconfig enforced)

- File-scoped namespaces (enforced as error)
- 4-space indentation, LF line endings, final newline required
- PascalCase for public members, `_camelCase` for private fields
- Prefer `var` when type is apparent
- Prefer pattern matching, switch expressions, and expression-bodied members
- Prefer target-typed `new()` when type is apparent
- Prefer collection expressions over traditional syntax
