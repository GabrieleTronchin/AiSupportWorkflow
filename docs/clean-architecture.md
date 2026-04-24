# Clean Architecture

> **📚 Navigation:** [← Back to README](../README.md) | [Actor Architecture](actor-architecture.md) | [Agent Framework Integration](agent-framework-integration.md)

This document describes how [Clean Architecture](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures#clean-architecture) is applied in the AI Support Workflow project.

## Four-Layer Structure

The solution is organized into four layers, each represented by a dedicated project under `src/`:

| Layer | Project | Path |
|-------|---------|------|
| Domain | `AiSupportWorkflow.Domain` | `src/AiSupportWorkflow.Domain/` |
| Application | `AiSupportWorkflow.Application` | `src/AiSupportWorkflow.Application/` |
| Infrastructure | `AiSupportWorkflow.Infrastructure` | `src/AiSupportWorkflow.Infrastructure/` |
| Presentation | `AiSupportWorkflow.Presentation` | `src/AiSupportWorkflow.Presentation/` |

### Domain

The innermost layer. Contains entities, enums, value objects, actor messages, and interface contracts. It has no knowledge of any other layer and no external package references.

- `Entities/` — Immutable records: `IncomingEmail`, `IssueRecord`, `WorkflowState`, `BugScenario`, `PullRequest`
- `Enums/` — `AgentRole`, `IssueCategory`, `WorkflowStage`
- `Interfaces/` — Contracts such as `IOrchestrator`, `IIssueClassifier`, `IBugResolver`, `IEmailProcessor`
- `Messages/` — Akka actor messages

### Application

Contains business logic, use cases, and service implementations that depend only on Domain abstractions.

- `Services/` — `Orchestrator`, `EmailProcessor`, `AgentSelector`, `TeamRouter`
- `UseCases/` — `ProcessSupportEmailUseCase`
- `Configuration/` — `WorkflowConfiguration`
- `Interfaces/` — `IPipelineStage<TIn, TOut>`

### Infrastructure

Implements Domain interfaces using external libraries (Akka.NET, Agent Framework, OpenAI). This is where third-party integrations live.

- `Actors/` — `AIAgentActor`, `SupervisorActor` (Akka.NET)
- `Agents/` — `AiAgent` (LLM-backed agent)
- `AgentFramework/` — `IssueClassifierService`, `BugResolverService`, `CodeChangeGeneratorService`
- `Services/` — `WorkflowStateTracker`
- `InfrastructureServiceExtensions.cs` — DI registration

### Presentation

The composition root. Hosts the Minimal API, wires up dependency injection, and configures the Akka.NET actor system.

- `Program.cs` — Host builder, DI, middleware pipeline
- `Endpoints/` — Minimal API endpoint classes implementing `IEndpoint`
- `Responses/` — API response DTOs

## Inward Dependency Rule

Dependencies flow strictly inward. Outer layers depend on inner layers, never the reverse.

```
Presentation → Infrastructure → Application → Domain
```

### Project Reference Mapping

| Project | References |
|---------|-----------|
| `AiSupportWorkflow.Domain` | _(none)_ |
| `AiSupportWorkflow.Application` | `Domain` |
| `AiSupportWorkflow.Infrastructure` | `Domain`, `Application` |
| `AiSupportWorkflow.Presentation` | `Domain`, `Application`, `Infrastructure` |

Domain defines interfaces; Infrastructure provides implementations. The Presentation layer (composition root) wires everything together via dependency injection. No inner layer ever references an outer layer.

## Compliance Verification

The project's `.csproj` files were inspected to confirm the dependency flow:

- **Domain** (`AiSupportWorkflow.Domain.csproj`): Zero `<ProjectReference>` entries and zero `<PackageReference>` entries. Fully independent. ✅
- **Application** (`AiSupportWorkflow.Application.csproj`): References only `AiSupportWorkflow.Domain`. Uses minimal infrastructure-agnostic packages (`Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options`). ✅
- **Infrastructure** (`AiSupportWorkflow.Infrastructure.csproj`): References `AiSupportWorkflow.Domain` and `AiSupportWorkflow.Application`. All external integrations (Akka.NET, Agent Framework, OpenAI) are confined to this layer. ✅
- **Presentation** (`AiSupportWorkflow.Presentation.csproj`): References `AiSupportWorkflow.Infrastructure`, `AiSupportWorkflow.Application`, and `AiSupportWorkflow.Domain`. Acts as the composition root with `Akka.Hosting` and `Microsoft.Agents.AI` for DI wiring. ✅

**Result**: The project fully complies with clean architecture dependency rules. No deviations found.

## NuGet Package License Verification

All NuGet packages used in this solution are free and open-source. No paid or proprietary licenses are present.

| Package | License | Free for OSS |
|---------|---------|:------------:|
| Akka.NET / Akka.Hosting / Akka.TestKit.Xunit2 | Apache-2.0 | ✅ |
| Microsoft.Agents.AI / Microsoft.Agents.AI.OpenAI | MIT | ✅ |
| Microsoft.Extensions.* (Options, Logging, Http.Resilience, Options.ConfigurationExtensions) | MIT | ✅ |
| xUnit / xunit.runner.visualstudio | Apache-2.0 | ✅ |
| FsCheck.Xunit | BSD-3-Clause | ✅ |
| NSubstitute | BSD-3-Clause | ✅ |
| coverlet.collector | MIT | ✅ |
| Microsoft.NET.Test.Sdk | MIT | ✅ |
| Microsoft.AspNetCore.Mvc.Testing | MIT | ✅ |
| Microsoft.Extensions.Http.Resilience | MIT | ✅ |

All licenses (Apache-2.0, MIT, BSD-3-Clause) are permissive open-source licenses compatible with the project's MIT license.

## References

- [Microsoft — Common Web Application Architectures: Clean Architecture](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures#clean-architecture)
