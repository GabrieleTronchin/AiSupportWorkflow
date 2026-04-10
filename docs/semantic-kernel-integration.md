# Semantic Kernel Integration

This document describes how [Microsoft Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/overview/) is used in the AI Support Workflow project to power LLM-backed services.

## Overview

Semantic Kernel provides the AI orchestration layer. The project uses its `IChatCompletionService` abstraction to interact with OpenAI models across three services, each handling a distinct stage of the support workflow pipeline.

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

### SemanticKernelSetup

The `SemanticKernelSetup.AddSemanticKernel()` extension method (`src/AiSupportWorkflow.Infrastructure/SemanticKernel/`) handles DI registration:

1. Reads `LlmProvider` configuration from `IConfiguration`
2. Validates the API key is present (throws `InvalidOperationException` if missing)
3. Registers `IChatCompletionService` via `AddOpenAIChatCompletion(modelName, apiKey)`
4. Configures HTTP resilience with Polly: 3 retries, exponential backoff with jitter

The provider is resolved via a `switch` on `config.Provider`, currently supporting `"openai"` only.

## LLM-Backed Services

All three services follow the same pattern:

- Inject `IChatCompletionService` via constructor
- Define a `SystemPrompt` constant with strict JSON response format instructions
- Use `ChatHistory` with system + user messages
- Configure `PromptExecutionSettings` with a tuned temperature
- Parse the JSON response with `JsonDocument` and return a typed result
- Fall back gracefully on LLM or parse errors

### IssueClassifierService

**Implements**: `IIssueClassifier` (Domain interface)

Classifies support emails into issue categories using the LLM.

| Setting | Value |
|---------|-------|
| Temperature | `0.1` (deterministic) |
| Input | `IssueRecord` (subject + body) |
| Output | `ClassificationResult` — `IsCodeRelated`, `IssueCategory`, `ConfidenceScore`, `Reasoning` |
| Fallback | Returns `OutOfScope` with zero confidence on error |

The system prompt instructs the model to respond with a JSON object containing `category`, `confidence`, and `reasoning`. Categories map to the `IssueCategory` enum: `BackendBug`, `FrontendBug`, `QualityTestIssue`, `OutOfScope`.

### BugResolverService

**Implements**: `IBugResolver` (Domain interface)

Performs root cause analysis for classified issues.

| Setting | Value |
|---------|-------|
| Temperature | `0.2` (low creativity) |
| Input | `IssueRecord` + `AgentAssignment` (agent context) |
| Output | `ResolutionReport` — `RootCauseDescription`, `AffectedComponent`, `SeverityAssessment`, `ProposedFixSummary`, `RequiresEscalation`, `EscalationReason` |
| Fallback | Returns an escalated report with the error message |

### CodeChangeGeneratorService

**Implements**: `ICodeChangeGenerator` (Domain interface)

Generates simulated pull requests with code diffs from a resolution report.

| Setting | Value |
|---------|-------|
| Temperature | `0.5` (balanced creativity) |
| Input | `ResolutionReport` |
| Output | `PullRequest` — `Title`, `Description`, `AffectedFilePaths`, `SimulatedDiff` |
| Fallback | Returns a minimal PR with a simulated diff |

File paths in the generated output are constrained to `DummyApps/ApplicationA/` or `DummyApps/ApplicationB/` via the system prompt.

## Temperature Strategy

Each service uses a different temperature tuned to its task:

```
Classification (0.1) → Resolution (0.2) → Code Generation (0.5)
```

Lower temperatures produce more deterministic output for classification, while code generation benefits from slightly higher creativity.

## SemanticKernelAgent

The `SemanticKernelAgent` class (`src/AiSupportWorkflow.Infrastructure/Agents/`) implements the `IAIAgent` Domain interface. It is a lightweight wrapper that:

- Holds agent identity: `AgentId`, `TeamName`, `Role`
- Delegates `AnalyzeAndResolveAsync` to `IBugResolver.ResolveAsync`, passing the agent's assignment context

Agent instances are built from configuration in `Program.cs` using the `Workflow:Teams` section. Each agent is created as `new SemanticKernelAgent(agentId, teamName, role, bugResolver)` with an ID format of `{TeamName}_{Role}`.

## Dependency Injection Flow

```
InfrastructureServiceExtensions.AddInfrastructure()
  ├── SemanticKernelSetup.AddSemanticKernel()     → registers IChatCompletionService
  ├── IIssueClassifier    → IssueClassifierService    (singleton)
  ├── IBugResolver        → BugResolverService         (singleton)
  ├── ICodeChangeGenerator → CodeChangeGeneratorService (singleton)
  └── IWorkflowStateTracker → WorkflowStateTracker     (singleton)

Program.cs
  └── IEnumerable<IAIAgent> → SemanticKernelAgent instances (from config)
```

All three LLM services receive `IChatCompletionService` via constructor injection, registered by Semantic Kernel's `AddOpenAIChatCompletion`.

## Integration with Akka.NET Actors

Each `SemanticKernelAgent` is wrapped in an `AIAgentActor` managed by the `SupervisorActor`. When an issue is assigned:

1. The `Orchestrator` sends an `AssignIssueMessage` to the supervisor
2. The supervisor routes it to the correct `AIAgentActor` by agent ID
3. The actor calls `SemanticKernelAgent.AnalyzeAndResolveAsync`
4. The agent delegates to `BugResolverService`, which calls the LLM
5. The resolution report flows back through the actor system

## References

- [Microsoft Semantic Kernel Overview](https://learn.microsoft.com/en-us/semantic-kernel/overview/)
- [Semantic Kernel Chat Completion](https://learn.microsoft.com/en-us/semantic-kernel/concepts/ai-services/chat-completion/)
- [OpenAI API Documentation](https://platform.openai.com/docs)
