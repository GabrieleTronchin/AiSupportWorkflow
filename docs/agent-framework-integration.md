# Agent Framework Integration

> **📚 Navigation:** [← Back to README](../README.md) | [Clean Architecture](clean-architecture.md) | [Actor Architecture](actor-architecture.md)

This document describes how [Microsoft Agent Framework](https://learn.microsoft.com/en-us/microsoft-cloud/dev/ai/agent-framework-overview) is used in the AI Support Workflow project to power LLM-backed services. The project uses the `IChatClient` abstraction from `Microsoft.Extensions.AI` — the standard .NET AI interface — to interact with OpenAI models across three services, each handling a distinct stage of the support workflow pipeline.

## Overview

The Agent Framework provides the AI integration layer through the `Microsoft.Extensions.AI` standard .NET abstraction. Instead of framework-specific types, the project depends on four core types from `Microsoft.Extensions.AI`:

| Type | Purpose |
|------|---------|
| `IChatClient` | Standard .NET interface for chat-based LLM interactions (analogous to `ILogger` for logging) |
| `ChatMessage` | Represents a single chat turn with a role (`System`, `User`, `Assistant`) and content |
| `ChatOptions` | Typed configuration for LLM request parameters such as `Temperature` |
| `ChatResponse` | The response returned by `IChatClient.GetResponseAsync`, with a `.Text` property for content |

The three LLM services depend only on `IChatClient` — they have no direct dependency on OpenAI-specific types. The OpenAI provider is referenced only in the DI setup, keeping provider selection isolated to the composition root.

## Configuration

### LlmProviderConfiguration

The `LlmProviderConfiguration` class (`src/AiSupportWorkflow.Infrastructure/Configuration/`) holds the LLM connection settings:

| Property | Default | Description |
|----------|---------|-------------|
| `Provider` | `"OpenAI"` | LLM provider identifier |
| `ModelName` | `"gpt-4o-mini"` | Model to use for chat completions |
| `ApiKey` | `""` | Required — must be set in `appsettings.Development.json` |
| `Endpoint` | `null` | Optional custom endpoint URL |

Configuration is bound from the `LlmProvider` section in `appsettings.json` / `appsettings.Development.json`.

### ChatClientSetup

The `ChatClientSetup.AddChatClient()` extension method (`src/AiSupportWorkflow.Infrastructure/AgentFramework/`) handles DI registration:

1. Reads `LlmProvider` configuration from `IConfiguration`
2. Validates the API key is present (throws `InvalidOperationException` if missing)
3. Creates an `OpenAIClient` with the API key, then obtains an `IChatClient` via `GetChatClient(modelName).AsIChatClient()`
4. Registers the `IChatClient` as a singleton in the DI container
5. Configures HTTP resilience with Polly: 3 retries, exponential backoff with jitter

The provider is resolved via a `switch` on `config.Provider`, currently supporting `"openai"` only. Unsupported providers throw `InvalidOperationException`.

## LLM-Backed Services

All three services follow the same pattern:

- Inject `IChatClient` via constructor
- Define a `SystemPrompt` constant with strict JSON response format instructions
- Build a `List<ChatMessage>` with system + user messages
- Configure `ChatOptions` with a tuned `Temperature` property
- Call `IChatClient.GetResponseAsync` and parse the `ChatResponse.Text` with `JsonDocument`
- Return a typed result, falling back gracefully on LLM or parse errors

### IssueClassifierService

**Implements**: `IIssueClassifier` (Domain interface)

Classifies support emails into issue categories using the LLM.

| Setting | Value |
|---------|-------|
| Temperature | `0.1f` (deterministic) |
| Input | `IssueRecord` (subject + body) |
| Output | `ClassificationResult` — `IsCodeRelated`, `IssueCategory`, `ConfidenceScore`, `Reasoning` |
| Fallback | Returns `OutOfScope` with zero confidence on error |

The system prompt instructs the model to respond with a JSON object containing `category`, `confidence`, and `reasoning`. Categories map to the `IssueCategory` enum: `BackendBug`, `FrontendBug`, `QualityTestIssue`, `OutOfScope`.

### BugResolverService

**Implements**: `IBugResolver` (Domain interface)

Performs root cause analysis for classified issues.

| Setting | Value |
|---------|-------|
| Temperature | `0.2f` (low creativity) |
| Input | `IssueRecord` + `AgentAssignment` (agent context) |
| Output | `ResolutionReport` — `RootCauseDescription`, `AffectedComponent`, `SeverityAssessment`, `ProposedFixSummary`, `RequiresEscalation`, `EscalationReason` |
| Fallback | Returns an escalated report with the error message |

### CodeChangeGeneratorService

**Implements**: `ICodeChangeGenerator` (Domain interface)

Generates simulated pull requests with code diffs from a resolution report.

| Setting | Value |
|---------|-------|
| Temperature | `0.5f` (balanced creativity) |
| Input | `ResolutionReport` |
| Output | `PullRequest` — `Title`, `Description`, `AffectedFilePaths`, `SimulatedDiff` |
| Fallback | Returns a minimal PR with a simulated diff |

File paths in the generated output are constrained to `DummyApps/ApplicationA/` or `DummyApps/ApplicationB/` via the system prompt.

## Temperature Strategy

Each service uses a different temperature tuned to its task:

```
Classification (0.1) → Resolution (0.2) → Code Generation (0.5)
```

Lower temperatures produce more deterministic output for classification, while code generation benefits from slightly higher creativity. Temperatures are set via the typed `ChatOptions.Temperature` property rather than a dictionary hack.

## AiAgent

The `AiAgent` class (`src/AiSupportWorkflow.Infrastructure/Agents/`) implements the `IAIAgent` Domain interface. It is a lightweight wrapper that:

- Holds agent identity: `AgentId`, `TeamName`, `Role`
- Delegates `AnalyzeAndResolveAsync` to `IBugResolver.ResolveAsync`, passing the agent's assignment context

Agent instances are built from configuration in `Program.cs` using the `Workflow:Teams` section. Each agent is created as `new AiAgent(agentId, teamName, role, bugResolver)` with an ID format of `{TeamName}_{Role}`.

## Dependency Injection Flow

```
InfrastructureServiceExtensions.AddInfrastructure()
  ├── ChatClientSetup.AddChatClient()          → registers IChatClient (singleton)
  ├── IIssueClassifier    → IssueClassifierService    (singleton)
  ├── IBugResolver        → BugResolverService         (singleton)
  ├── ICodeChangeGenerator → CodeChangeGeneratorService (singleton)
  └── IWorkflowStateTracker → WorkflowStateTracker     (singleton)

Program.cs
  └── IEnumerable<IAIAgent> → AiAgent instances (from config)
```

All three LLM services receive `IChatClient` via constructor injection, registered by `ChatClientSetup.AddChatClient()`.

## Provider-Agnostic Design

The `IChatClient` interface from `Microsoft.Extensions.AI` is the standard .NET AI abstraction — analogous to `ILogger` for logging or `HttpClient` for HTTP. This gives the project a provider-agnostic architecture:

- **Services** depend only on `IChatClient` — no OpenAI-specific types
- **DI setup** is the only location where the OpenAI provider is referenced
- **Swapping providers** (e.g., to Azure OpenAI, Anthropic, or a local model) requires changes only in `ChatClientSetup.cs`, not in any service implementation

This isolation is enforced by the clean architecture boundary: the Infrastructure layer's `AgentFramework/` folder contains the services, while the provider-specific setup is confined to `ChatClientSetup.cs`.

## Integration with Akka.NET Actors

Each `AiAgent` is wrapped in an `AIAgentActor` managed by the `SupervisorActor`. When an issue is assigned:

1. The `Orchestrator` sends an `AssignIssueMessage` to the supervisor
2. The supervisor routes it to the correct `AIAgentActor` by agent ID
3. The actor calls `AiAgent.AnalyzeAndResolveAsync`
4. The agent delegates to `BugResolverService`, which calls the LLM via `IChatClient`
5. The resolution report flows back through the actor system

## Migration Note

This project was migrated from Microsoft Semantic Kernel (v1.74.0) to Microsoft Agent Framework (v1.2.0).

**Why the migration?** The Agent Framework is the official successor to Semantic Kernel, unifying Semantic Kernel and AutoGen into a single SDK. It is built on `Microsoft.Extensions.AI`, which provides `IChatClient` as the standard .NET AI abstraction — a first-class .NET interface that replaces framework-specific abstractions with a platform-standard one.

**Key API mapping changes:**

| Semantic Kernel | Agent Framework / Microsoft.Extensions.AI |
|---|---|
| `IChatCompletionService` | `IChatClient` |
| `ChatHistory` | `List<ChatMessage>` |
| `PromptExecutionSettings` with `ExtensionData` dictionary | `ChatOptions` with typed `Temperature` property |
| `ChatMessageContent` | `ChatResponse` |
| `GetChatMessageContentAsync(history, settings)` | `GetResponseAsync(messages, options)` |
| `response.Content` | `response.Text` |
| `AddOpenAIChatCompletion(model, apiKey)` | `OpenAIClient` → `GetChatClient(model).AsIChatClient()` |

**What didn't change:** The Domain and Application layers required zero changes. All domain entities, interfaces, value objects, and business logic remained identical — a direct benefit of the clean architecture approach. The migration was confined entirely to the Infrastructure layer, Presentation layer, and test projects.

## References

- [Microsoft Agent Framework Overview](https://learn.microsoft.com/en-us/microsoft-cloud/dev/ai/agent-framework-overview)
- [Microsoft.Extensions.AI Documentation](https://learn.microsoft.com/en-us/dotnet/ai/ai-extensions)
- [OpenAI API Documentation](https://platform.openai.com/docs)
