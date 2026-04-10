# Requirements Document

## Introduction

This specification defines the optimization requirements for the Semantic Kernel integration in the AI Support Workflow project. The current implementation has several antipatterns and missing best practices that reduce reliability, testability, and provider portability. This spec addresses five specific areas: service lifetime mismatches (captive dependency), missing prompt execution settings, absent retry/resilience logic for LLM calls, suboptimal Kernel resolution patterns, and OpenAI-specific configuration that hinders provider switching.

The overarching priority is maximum provider portability — all changes must use provider-agnostic Semantic Kernel APIs exclusively (`PromptExecutionSettings` base class, `IChatCompletionService` interface). No OpenAI-specific types (e.g., `OpenAIPromptExecutionSettings`, `ResponseFormat`) may be introduced or retained in service code.

This spec is scoped exclusively to Semantic Kernel optimizations. Akka.NET, code style, and other improvements are out of scope.

## Glossary

- **Semantic_Kernel**: Microsoft's AI orchestration framework used to integrate LLM providers into the application.
- **IChatCompletionService**: The provider-agnostic Semantic Kernel interface for chat-based LLM interactions.
- **PromptExecutionSettings**: The provider-agnostic base class in Semantic Kernel for controlling LLM behavior (temperature, max tokens, etc.). Subclasses like `OpenAIPromptExecutionSettings` are provider-specific and must not be used in service code.
- **Captive_Dependency**: A dependency injection antipattern where a shorter-lived (Transient) service is captured by a longer-lived (Singleton) consumer, causing the transient instance to live beyond its intended scope.
- **LLM_Service**: Any of the three Semantic Kernel-backed services: `IssueClassifierService`, `BugResolverService`, `CodeChangeGeneratorService`.
- **Kernel**: The Semantic Kernel `Kernel` object that acts as a service locator for AI services. Registered as Transient by `AddKernel()`.
- **Resilience_Pipeline**: A retry and timeout strategy applied to outbound LLM HTTP calls to handle transient failures (rate limits, network timeouts) before falling back to error handling.
- **Provider_Configuration**: An abstracted configuration model that supports multiple LLM providers (OpenAI, Azure OpenAI, Anthropic, Ollama, etc.) through a provider selector pattern, replacing the current OpenAI-specific `OpenAIConfiguration`.

## Requirements

### Requirement 1: Fix Service Lifetime Mismatch (Captive Dependency)

**User Story:** As a developer, I want the Semantic Kernel service lifetimes to be correctly aligned, so that transient Kernel instances are not captured by singleton consumers and each LLM call uses a properly scoped Kernel.

#### Acceptance Criteria

1. THE DI_Registration SHALL register `IssueClassifierService`, `BugResolverService`, and `CodeChangeGeneratorService` with a lifetime that does not capture a Transient `Kernel` instance in a Singleton consumer.
2. WHEN an LLM_Service method is invoked, THE LLM_Service SHALL resolve a fresh `Kernel` or `IChatCompletionService` instance per call rather than reusing a constructor-captured Transient instance.
3. THE DI_Registration SHALL maintain the existing public interface contracts (`IIssueClassifier`, `IBugResolver`, `ICodeChangeGenerator`) without breaking changes.
4. IF the LLM_Service is registered as Singleton for performance reasons, THEN THE LLM_Service SHALL use `IServiceProvider` or a factory to resolve the `IChatCompletionService` per invocation instead of accepting `Kernel` via constructor injection.

### Requirement 2: Add Provider-Agnostic Prompt Execution Settings

**User Story:** As a developer, I want each LLM call to include explicit prompt execution settings using the provider-agnostic base class, so that temperature, max tokens, and other parameters are tuned per task and the system remains portable across LLM providers.

#### Acceptance Criteria

1. WHEN the `IssueClassifierService` calls the LLM, THE `IssueClassifierService` SHALL pass a `PromptExecutionSettings` instance with a temperature between 0.0 and 0.3 to produce deterministic classification results.
2. WHEN the `BugResolverService` calls the LLM, THE `BugResolverService` SHALL pass a `PromptExecutionSettings` instance with a temperature between 0.0 and 0.3 to produce consistent root cause analysis.
3. WHEN the `CodeChangeGeneratorService` calls the LLM, THE `CodeChangeGeneratorService` SHALL pass a `PromptExecutionSettings` instance with a temperature between 0.3 and 0.7 to allow moderate creativity in code fix generation.
4. THE LLM_Services SHALL use only the `PromptExecutionSettings` base class from `Microsoft.SemanticKernel` and SHALL NOT use provider-specific subclasses such as `OpenAIPromptExecutionSettings`.
5. THE `PromptExecutionSettings` instances SHALL be configurable or defined as constants within each service, allowing future adjustment without code changes to the calling logic.

### Requirement 3: Add Retry and Resilience for LLM Calls

**User Story:** As a developer, I want transient LLM failures (rate limits, timeouts, temporary network errors) to be retried automatically, so that the system does not fall back to error results on recoverable failures.

#### Acceptance Criteria

1. WHEN an LLM call fails due to a transient error (HTTP 429 rate limit, HTTP 5xx server error, or network timeout), THE Resilience_Pipeline SHALL retry the call before the LLM_Service falls back to error handling.
2. THE Resilience_Pipeline SHALL use exponential backoff between retry attempts to avoid overwhelming the LLM provider.
3. THE Resilience_Pipeline SHALL limit retries to a maximum of 3 attempts per LLM call.
4. THE Resilience_Pipeline SHALL be configured at the `HttpClient` level used by Semantic Kernel, so that all LLM_Services benefit from the same resilience strategy without per-service retry code.
5. IF all retry attempts are exhausted, THEN THE LLM_Service SHALL proceed with the existing error handling logic (logging the error and returning a fallback result).
6. THE Resilience_Pipeline SHALL log each retry attempt at the Warning log level, including the attempt number and the error that triggered the retry.

### Requirement 4: Use Direct IChatCompletionService Injection

**User Story:** As a developer, I want the LLM services to receive `IChatCompletionService` directly instead of resolving it from `Kernel` at each call site, so that the dependency is explicit, testable, and provider-agnostic.

#### Acceptance Criteria

1. THE LLM_Services SHALL depend on `IChatCompletionService` rather than on `Kernel` for obtaining chat completion capabilities.
2. THE LLM_Services SHALL NOT call `kernel.GetRequiredService<IChatCompletionService>()` inside method bodies.
3. THE DI_Registration SHALL register `IChatCompletionService` in the service container so that it can be injected directly into LLM_Services.
4. WHEN a unit test mocks the LLM dependency, THE test SHALL provide a mock `IChatCompletionService` directly without needing to construct a full `Kernel` instance.
5. THE refactored LLM_Services SHALL produce identical functional behavior (same prompts, same response parsing, same error handling) as the current implementation.

### Requirement 5: Abstract Provider Configuration for Portability

**User Story:** As a developer, I want the LLM provider configuration to be abstracted behind a provider-agnostic model, so that switching from OpenAI to another provider (Azure OpenAI, Anthropic, Ollama) requires only configuration changes and a new provider registration, not service code modifications.

#### Acceptance Criteria

1. THE Provider_Configuration SHALL define a provider-agnostic configuration model that includes: provider name, model name, API key, and optional endpoint URL.
2. THE Provider_Configuration SHALL support a provider selector field that determines which LLM provider connector to register with Semantic Kernel (e.g., "OpenAI", "AzureOpenAI", "Ollama").
3. WHEN the provider selector is set to "OpenAI", THE SemanticKernelSetup SHALL register the OpenAI chat completion connector using the configured model name and API key.
4. THE Provider_Configuration SHALL replace the current `OpenAIConfiguration` class as the configuration source for LLM provider settings.
5. THE `appsettings.json` configuration section SHALL be restructured to use the new provider-agnostic model while maintaining backward compatibility with the existing `OpenAI` section during a transition period.
6. WHEN a new provider is added in the future, THE SemanticKernelSetup SHALL require only a new case in the provider registration logic and the corresponding Semantic Kernel connector package, with zero changes to LLM_Service code.
7. THE LLM_Service code SHALL contain zero references to OpenAI-specific types, namespaces, or configuration classes.
