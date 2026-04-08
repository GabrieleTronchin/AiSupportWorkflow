# Tasks: AI Support Workflow

## Task 1: Project Scaffolding and Configuration

- [x] 1.1 Create .NET 10 solution with Clean Architecture project structure: `AiSupportWorkflow.Domain`, `AiSupportWorkflow.Application`, `AiSupportWorkflow.Infrastructure`, `AiSupportWorkflow.Presentation`, `AiSupportWorkflow.UnitTests`, `AiSupportWorkflow.PropertyTests`
- [x] 1.2 Configure all `.csproj` files with `net10.0` target, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<LangVersion>latest</LangVersion>`
- [x] 1.3 Create `.editorconfig` at repository root enforcing all C# code style rules from Requirement 15
- [x] 1.4 Add NuGet package references: `Microsoft.SemanticKernel`, `Akka.NET`, `Akka.Hosting`, `FsCheck.Xunit`, `xUnit`, `OpenAI` connector for Semantic Kernel
- [x] 1.5 Create folder structure within each layer: Domain (`Entities/`, `Enums/`, `Interfaces/`, `ValueObjects/`), Application (`Services/`, `Interfaces/`, `UseCases/`), Infrastructure (`Actors/`, `SemanticKernel/`, `Services/`, `Configuration/`), Presentation (`Endpoints/`)

## Task 2: Domain Layer — Entities, Enums, Interfaces, and Value Objects

- [x] 2.1 Create domain entities as records: `IncomingEmail`, `IssueRecord`, `PullRequest`, `WorkflowState`, `BugScenario` in `Domain/Entities/`
- [x] 2.2 Create domain enums: `IssueCategory`, `AgentRole`, `WorkflowStage` in `Domain/Enums/`
- [x] 2.3 Create domain value objects as records: `ClassificationResult`, `TeamAssignment`, `AgentAssignment`, `ResolutionReport`, `Result<T>` in `Domain/ValueObjects/`
- [x] 2.4 Create domain interfaces: `IEmailProcessor`, `IIssueClassifier`, `ITeamRouter`, `IAgentSelector`, `IBugResolver`, `ICodeChangeGenerator`, `IOrchestrator`, `IWorkflowStateTracker` in `Domain/Interfaces/`
- [x] 2.5 Create `IAIAgent` interface in `Domain/Interfaces/`

## Task 3: Application Layer — Services and Use Cases

- [x] 3.1 Create `IPipelineStage<TInput, TOutput>` interface in `Application/Interfaces/`
- [x] 3.2 Implement `EmailProcessor` service in `Application/Services/` — parse incoming email, validate subject/body non-empty, assign unique ID, return `Result<IssueRecord>`
- [x] 3.3 Implement `TeamRouter` service in `Application/Services/` — map ApplicationA→TeamA, ApplicationB→TeamB, flag ambiguous cases for manual review
- [x] 3.4 Implement `AgentSelector` service in `Application/Services/` — map BackendBug→BackendDeveloper, FrontendBug→FrontendDeveloper, QualityTestIssue→QAEngineer using configuration
- [x] 3.5 Implement `ProcessSupportEmailUseCase` in `Application/UseCases/` — orchestrate the full pipeline calling each service in sequence

## Task 4: Infrastructure Layer — Semantic Kernel Integration

- [x] 4.1 Implement `SemanticKernelSetup` in `Infrastructure/SemanticKernel/` — configure Semantic Kernel with OpenAI ChatGPT (non-Azure), load API key from environment config, model name from `appsettings.json`
- [x] 4.2 Implement `IssueClassifierService` in `Infrastructure/SemanticKernel/` — use Semantic Kernel to classify emails as code-related (backend/frontend/QA) or out-of-scope, include confidence score, handle LLM errors
- [x] 4.3 Implement `BugResolverService` in `Infrastructure/SemanticKernel/` — use Semantic Kernel to generate root cause analysis, resolution report with severity and proposed fix, handle escalation when root cause cannot be determined
- [x] 4.4 Implement `CodeChangeGeneratorService` in `Infrastructure/SemanticKernel/` — generate simulated code fix and PullRequest record referencing dummy application files

## Task 5: Infrastructure Layer — Akka.NET Actor System

- [x] 5.1 Define actor messages as records in `Infrastructure/Actors/ActorMessages.cs`: `AssignIssueMessage`, `ResolutionCompleteMessage`, `AgentStatusQuery`, `AgentStatusResponse`
- [x] 5.2 Implement `AIAgentActor` in `Infrastructure/Actors/` — Akka.NET actor wrapping `IAIAgent`, handles `AssignIssueMessage`, invokes Semantic Kernel for reasoning, returns `ResolutionCompleteMessage`
- [x] 5.3 Implement `SupervisorActor` in `Infrastructure/Actors/` — supervises all 6 AI agent actors (BE/FE/QA × TeamA/TeamB), restart strategy with backoff on failure
- [x] 5.4 Implement `WorkflowStateTracker` in `Infrastructure/Services/` — track workflow state transitions per issue, support concurrent access

## Task 6: Infrastructure Layer — Configuration

- [x] 6.1 Create configuration models in `Infrastructure/Configuration/`: `TeamConfiguration`, `AgentRoleConfiguration`, `OpenAIConfiguration`, `WorkflowConfiguration`
- [x] 6.2 Create `appsettings.json` with team definitions (TeamA/TeamB with 3 agents each), OpenAI model config, visualization toggle
- [x] 6.3 Wire configuration binding in DI composition root

## Task 7: Presentation Layer — Minimal API Endpoints

- [x] 7.1 Implement `POST /api/support/emails` endpoint in `Presentation/Endpoints/SupportEmailEndpoints.cs` — accept email DTO, invoke orchestrator, return workflow result or validation error (400)
- [x] 7.2 Implement `GET /api/support/issues/{id}` endpoint in `Presentation/Endpoints/WorkflowStatusEndpoints.cs` — return workflow state for a given issue ID
- [x] 7.3 Implement `GET /api/support/issues` endpoint in `Presentation/Endpoints/WorkflowStatusEndpoints.cs` — list all processed issues
- [x] 7.4 Implement `GET /api/support/stream` SSE endpoint in `Presentation/Endpoints/VisualizationEndpoints.cs` — stream real-time workflow state updates (conditional on visualization feature flag)
- [x] 7.5 Implement `GET /api/support/agents` endpoint in `Presentation/Endpoints/VisualizationEndpoints.cs` — return current state of all AI agents

## Task 8: Presentation Layer — Program.cs and DI Composition Root

- [x] 8.1 Implement `Program.cs` — register all services, configure Akka.NET actor system, configure Semantic Kernel, map all Minimal API endpoints, wire dependency injection

## Task 9: Orchestrator Implementation

- [x] 9.1 Implement `Orchestrator` in `Application/Services/` — coordinate full pipeline: email processing → classification → team routing → agent selection → bug resolution → code change generation
- [x] 9.2 Implement workflow state tracking within the Orchestrator — transition states at each pipeline stage, record failures, support concurrent issue processing
- [x] 9.3 Implement inter-agent communication through Akka.NET actor system message protocol — Orchestrator interacts with agents exclusively via actor messages

## Task 10: Dummy Applications

- [x] 10.1 Create `DummyApps/ApplicationA/` with sample source files containing at least 3 predefined bug scenarios (1 backend, 1 frontend, 1 QA/test)
- [x] 10.2 Create `DummyApps/ApplicationB/` with sample source files containing at least 3 predefined bug scenarios (1 backend, 1 frontend, 1 QA/test)
- [x] 10.3 Document each bug scenario with description, affected file, line range, buggy code, and expected fix

## Task 11: Visualization Layer (Optional Feature)

- [x] 11.1 Implement decision-point logging — log classification result, team assignment, and agent selection with Semantic Kernel reasoning output at each decision point
- [x] 11.2 Ensure the system functions correctly when visualization is disabled (feature flag off)

## Task 12: Unit Tests

- [x] 12.1 Write unit tests for `EmailProcessor` — valid email parsing, empty subject rejection, empty body rejection, unique ID assignment
- [x] 12.2 Write unit tests for `TeamRouter` — ApplicationA→TeamA mapping, ApplicationB→TeamB mapping, ambiguous application handling
- [x] 12.3 Write unit tests for `AgentSelector` — category-to-role mapping for all 3 categories, correct team assignment
- [x] 12.4 Write unit tests for `IssueClassifier` — mock Semantic Kernel, test code-related vs out-of-scope classification, LLM error handling, manual review fallback
- [x] 12.5 Write unit tests for `BugResolver` — mock Semantic Kernel, test resolution report generation, escalation on failure
- [x] 12.6 Write unit tests for `CodeChangeGenerator` — mock Semantic Kernel, test PR creation with valid fields, issue ID traceability
- [x] 12.7 Write unit tests for `Orchestrator` — end-to-end pipeline with mocks, failure at each stage, concurrent processing
- [x] 12.8 Write unit tests for `WorkflowStateTracker` — state transitions, invalid transitions, concurrent access
- [x] 12.9 Write unit tests for Minimal API endpoints — correct routing, validation error responses (400), successful responses

## Task 13: Property-Based Tests

- [x] 13.1 Implement FsCheck generators in `PropertyTests/Generators/`: `EmailGenerators`, `ClassificationGenerators`, `WorkflowGenerators` — generate random valid/invalid emails, classification results, workflow states
- [x] 13.2 Implement Property 1 (Email processing round trip) in `EmailProcessingProperties.cs` — for any valid email, processed IssueRecord preserves sender/subject/body with unique ID
- [x] 13.3 Implement Property 2 (Invalid email rejection) in `EmailProcessingProperties.cs` — for any email with empty/whitespace subject or body, EmailProcessor rejects
- [x] 13.4 Implement Property 3 (Classification result consistency) in `ClassificationProperties.cs` — IsCodeRelated↔Category consistency, ConfidenceScore in [0.0, 1.0]
- [x] 13.5 Implement Property 4 (Application-to-team mapping) in `RoutingProperties.cs` — ApplicationA↔TeamA, ApplicationB↔TeamB
- [x] 13.6 Implement Property 5 (Category-to-role mapping) in `RoutingProperties.cs` — BackendBug↔BackendDeveloper, FrontendBug↔FrontendDeveloper, QualityTestIssue↔QAEngineer
- [x] 13.7 Implement Property 6 (Resolution report completeness) in `ResolutionProperties.cs` — non-escalated reports have all required fields non-empty
- [x] 13.8 Implement Property 7 (PR completeness and traceability) in `ResolutionProperties.cs` — PR fields non-empty, IssueId matches original
- [x] 13.9 Implement Property 8 (Workflow state transition ordering) in `WorkflowProperties.cs` — transitions follow valid pipeline paths, no skips or revisits
- [x] 13.10 Implement Property 9 (Concurrent issue independence) in `WorkflowProperties.cs` — N concurrent issues produce independent results with unique IDs
- [x] 13.11 Implement Property 10 (Fix references valid dummy app files) in `ResolutionProperties.cs` — PR file paths exist in the corresponding dummy application
- [x] 13.12 Implement Property 11 (Configuration-driven team instantiation) in `WorkflowProperties.cs` — agents match configuration exactly
- [x] 13.13 Implement Property 12 (Visualization decision logging) in `WorkflowProperties.cs` — each decision point produces a log entry with SK reasoning

## Task 14: Final Refactor — Code Quality, SOLID Compliance, and Organization

- [x] 14.1 Review and refactor all methods exceeding ~20 lines — extract into smaller, well-named helper methods with single responsibility
- [x] 14.2 Replace all complex `for`/`foreach` loops and manual dictionary manipulation with LINQ expressions where readability improves
- [x] 14.3 Verify SOLID compliance across all layers — ensure no god classes, no interface pollution, all dependencies flow inward through interfaces, Open/Closed principle respected for extensibility points
- [x] 14.4 Verify project folder structure — confirm each layer has proper subfolder separation (Services/, Entities/, Interfaces/, Actors/, SemanticKernel/, etc.)
- [x] 14.5 Review naming conventions and self-documenting code — ensure meaningful names, remove comments that restate code, add *why* comments where non-obvious decisions exist
- [x] 14.6 Run full test suite (unit + property tests) to confirm refactoring introduced no regressions
