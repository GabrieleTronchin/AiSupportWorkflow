# Project Structure

## Architecture

Clean Architecture with strict inward dependency flow:

```
Domain ← Application ← Infrastructure ← Presentation
```

- Domain has zero external package references
- Application depends only on Domain
- Infrastructure implements Domain interfaces, integrates external services
- Presentation is the composition root (Minimal API, DI wiring, Akka.NET actor setup, gRPC service)

## Solution Layout

```
src/
├── AiSupportWorkflow.Domain/            # Pure domain layer
│   ├── Entities/                         # Records: IncomingEmail, IssueRecord, WorkflowState, BugScenario, PullRequest
│   ├── Enums/                            # AgentRole, IssueCategory, WorkflowStage
│   ├── Interfaces/                       # Contracts: IOrchestrator, IIssueClassifier, IBugResolver, IWorkflowStateTracker, etc.
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
│   ├── Agents/                           # AiAgent (LLM-backed agent)
│   ├── AgentFramework/                   # IssueClassifierService, BugResolverService, CodeChangeGeneratorService
│   ├── Persistence/                      # EF Core InMemory persistence layer
│   │   ├── Entities/                     # IssueEntity, StateTransitionEvent, InboxMessage
│   │   ├── Configurations/              # IEntityTypeConfiguration<T> for each entity
│   │   ├── WorkflowDbContext.cs         # DbContext with Issues, Events, InboxMessages DbSets
│   │   ├── EfWorkflowStateTracker.cs    # IWorkflowStateTracker implementation (replaces old ConcurrentDictionary)
│   │   └── PersistenceServiceExtensions.cs  # AddPersistence() DI registration
│   ├── Services/                         # InboxProcessor (IHostedService), WorkflowUpdateChannel
│   ├── Configuration/                    # OpenAIConfiguration
│   └── InfrastructureServiceExtensions.cs  # DI registration extension method
│
├── AiSupportWorkflow.Presentation/       # REST API & composition root
│   ├── Program.cs                        # Host builder, DI, Akka actor system, gRPC, InboxProcessor setup
│   ├── Endpoints/                        # Minimal API endpoint classes (IEndpoint pattern)
│   │   ├── SupportEmailEndpoints.cs      # POST /api/support/emails (202 + inbox)
│   │   ├── WorkflowStatusEndpoints.cs    # GET /issues, /issues/{id}, /events
│   │   ├── AgentsEndpoints.cs            # GET /agents (configured agents with status)
│   │   └── InboxEndpoints.cs             # GET /inbox (with status filter)
│   ├── Services/                         # WorkflowMonitorService (gRPC server streaming)
│   ├── Protos/                           # workflow_monitor.proto (Protobuf definition)
│   └── Responses/                        # API response DTOs

tests/
├── AiSupportWorkflow.UnitTests/          # xUnit + NSubstitute, one test class per service
│   ├── Persistence/                      # EfWorkflowStateTracker, InboxProcessor, Endpoint tests
│   └── Helpers/                          # Shared test utilities
├── AiSupportWorkflow.PropertyTests/      # FsCheck property-based tests
│   ├── PersistenceProperties.cs          # Dual-write invariant, gRPC notification
│   └── InboxProperties.cs               # FIFO order, failure handling, round-trip

dashboard/                                # React monitoring dashboard
├── src/
│   ├── api/                              # REST client, gRPC-Web client
│   ├── components/                       # PipelineVisualizer, EmailComposer, AgentMonitor, EventLog, IssuesList
│   ├── hooks/                            # useGrpcStream, useAgents, useIssues, useEvents, useInbox, useEmailSubmit
│   ├── pages/                            # OverviewPage, IssuesPage, EventLogPage, AgentsPage, InboxPage
│   ├── types/                            # Shared TypeScript types
│   └── __tests__/                        # Vitest + fast-check tests

DummyApps/
├── ApplicationA/                         # Sample app with BugScenarios.md, src/, tests/
├── ApplicationB/                         # Sample app with BugScenarios.md, src/, tests/
```

## Key Patterns

- **Actor Model**: Agents are Akka.NET actors managed by a SupervisorActor
- **Result Pattern**: `WorkflowResult`, `ClassificationResult` for explicit success/failure (no exceptions for business logic)
- **Records**: Domain entities and value objects are immutable C# records
- **Minimal APIs**: Endpoints defined as classes implementing `IEndpoint`, auto-discovered via reflection
- **DI via Extension Methods**: `AddInfrastructure()`, `AddPersistence()` in Infrastructure, service registration in `Program.cs`
- **Configuration-Driven**: Teams and agents loaded from `appsettings.json`, not hardcoded
- **Async-First**: All I/O-bound operations use `async/await` with `CancellationToken`
- **Transactional Inbox**: Email submissions saved to inbox (HTTP 202), processed asynchronously by `InboxProcessor`
- **EF Core InMemory**: Structured persistence with `WorkflowDbContext`, ready for SQL migration
- **gRPC Server Streaming**: Real-time workflow updates via `WorkflowMonitorService` + `WorkflowUpdateChannel`
- **Dual-Write**: Every state transition updates both the issue record and creates a `StateTransitionEvent` (audit log)

## Testing Conventions

- Unit tests: one test class per service, Arrange-Act-Assert, NSubstitute for mocking
- Property tests: FsCheck (.NET) and fast-check (TypeScript) for generative/invariant testing
- Unit test project uses Web SDK (`Microsoft.NET.Sdk.Web`) for integration test support
- Both test projects reference all src projects
- Frontend tests use Vitest with jsdom environment
