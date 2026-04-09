# Project Structure

## Architecture

Clean Architecture with strict inward dependency flow:

```
Domain ← Application ← Infrastructure ← Presentation
```

- Domain has zero external package references
- Application depends only on Domain
- Infrastructure implements Domain interfaces, integrates external services
- Presentation is the composition root (Minimal API, DI wiring, Akka.NET actor setup)

## Solution Layout

```
src/
├── AiSupportWorkflow.Domain/            # Pure domain layer
│   ├── Entities/                         # Records: IncomingEmail, IssueRecord, WorkflowState, BugScenario, PullRequest
│   ├── Enums/                            # AgentRole, IssueCategory, WorkflowStage
│   ├── Interfaces/                       # Contracts: IOrchestrator, IIssueClassifier, IBugResolver, etc.
│   ├── ValueObjects/                     # Immutable results: WorkflowResult, ClassificationResult, ResolutionReport, etc.
│   └── Messages/                         # Akka actor messages: AssignIssueMessage, ResolutionCompleteMessage, etc.
│
├── AiSupportWorkflow.Application/        # Business logic
│   ├── Services/                         # Orchestrator, EmailProcessor, AgentSelector, TeamRouter
│   ├── UseCases/                         # ProcessSupportEmailUseCase
│   ├── Configuration/                    # WorkflowConfiguration, TeamConfiguration, AgentRoleConfiguration
│   └── Interfaces/                       # IPipelineStage<TIn, TOut>
│
├── AiSupportWorkflow.Infrastructure/     # External integrations
│   ├── Actors/                           # AIAgentActor, SupervisorActor (Akka.NET)
│   ├── Agents/                           # SemanticKernelAgent (LLM-backed agent)
│   ├── SemanticKernel/                   # IssueClassifierService, BugResolverService, CodeChangeGeneratorService
│   ├── Services/                         # WorkflowStateTracker
│   ├── Configuration/                    # OpenAIConfiguration
│   └── InfrastructureServiceExtensions.cs  # DI registration extension method
│
├── AiSupportWorkflow.Presentation/       # REST API & composition root
│   ├── Program.cs                        # Host builder, DI, Akka actor system setup
│   ├── Endpoints/                        # Minimal API endpoint mapping (extension methods)
│   └── Responses/                        # API response DTOs

tests/
├── AiSupportWorkflow.UnitTests/          # xUnit + NSubstitute, one test class per service
│   └── Helpers/                          # Shared test utilities
├── AiSupportWorkflow.PropertyTests/      # FsCheck property-based tests

DummyApps/
├── ApplicationA/                         # Sample app with BugScenarios.md, src/, tests/
├── ApplicationB/                         # Sample app with BugScenarios.md, src/, tests/
```

## Key Patterns

- **Actor Model**: Agents are Akka.NET actors managed by a SupervisorActor
- **Result Pattern**: `WorkflowResult`, `ClassificationResult` for explicit success/failure (no exceptions for business logic)
- **Records**: Domain entities and value objects are immutable C# records
- **Minimal APIs**: Endpoints defined as static extension methods, no controllers
- **DI via Extension Methods**: `AddInfrastructure()` in Infrastructure, service registration in `Program.cs`
- **Configuration-Driven**: Teams and agents loaded from `appsettings.json`, not hardcoded
- **Async-First**: All I/O-bound operations use `async/await` with `CancellationToken`

## Testing Conventions

- Unit tests: one test class per service, Arrange-Act-Assert, NSubstitute for mocking
- Property tests: FsCheck for generative/invariant testing
- Unit test project uses Web SDK (`Microsoft.NET.Sdk.Web`) for integration test support
- Both test projects reference all src projects
