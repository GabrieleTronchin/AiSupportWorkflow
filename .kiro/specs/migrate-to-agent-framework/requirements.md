# Requirements Document

## Introduction

This document specifies the requirements for migrating the AI Support Workflow project from Microsoft Semantic Kernel to Microsoft Agent Framework. The Agent Framework (GA since April 2026, v1.2.0) is the official successor to Semantic Kernel, unifying Semantic Kernel and AutoGen into a single SDK built on the `Microsoft.Extensions.AI` standard .NET AI abstraction (`IChatClient`). The migration replaces all Semantic Kernel dependencies, updates LLM service implementations to use `IChatClient`, updates test helpers, refreshes all documentation, and leverages the provider-agnostic `IChatClient` abstraction as the standard .NET pattern for LLM integration.

## Glossary

- **Agent_Framework**: Microsoft Agent Framework — the official successor SDK to Semantic Kernel, providing unified AI agent capabilities built on `Microsoft.Extensions.AI`.
- **IChatClient**: The standard .NET AI abstraction from `Microsoft.Extensions.AI` for chat-based LLM interactions, replacing Semantic Kernel's `IChatCompletionService`.
- **ChatMessage**: A message type from `Microsoft.Extensions.AI` representing a single chat turn, replacing Semantic Kernel's `ChatHistory` entries.
- **ChatOptions**: Configuration type from `Microsoft.Extensions.AI` for LLM request parameters (temperature, etc.), replacing Semantic Kernel's `PromptExecutionSettings`.
- **ChatResponse**: The response type from `Microsoft.Extensions.AI` returned by `IChatClient.GetResponseAsync`, replacing Semantic Kernel's `ChatMessageContent`.
- **LLM_Service**: One of the three Infrastructure-layer services (`IssueClassifierService`, `BugResolverService`, `CodeChangeGeneratorService`) that interact with an LLM via chat completion.
- **DI_Setup**: The dependency injection registration code in `SemanticKernelSetup.cs` and `InfrastructureServiceExtensions.cs` that wires LLM services into the application.
- **FakeChatClient**: The test helper that implements `IChatClient` for unit testing, replacing the existing `FakeChatCompletionService`.
- **Migration_Note**: A documentation section explaining the rationale and key changes of the migration from Semantic Kernel to Agent Framework.

## Requirements

### Requirement 1: Replace Semantic Kernel NuGet Packages with Agent Framework Packages

**User Story:** As a developer, I want the project to use Microsoft Agent Framework packages instead of Semantic Kernel packages, so that the project is built on the current, supported SDK.

#### Acceptance Criteria

1. THE Infrastructure_Project SHALL reference `Microsoft.Agents.AI` and `Microsoft.Agents.AI.OpenAI` packages (version 1.2.0 or later) instead of `Microsoft.SemanticKernel` and `Microsoft.SemanticKernel.Connectors.OpenAI`.
2. THE Presentation_Project SHALL reference `Microsoft.Agents.AI` instead of `Microsoft.SemanticKernel`.
3. WHEN the solution is built, THE Build_System SHALL produce zero errors and zero warnings related to Semantic Kernel references.
4. THE Solution SHALL contain zero references to any `Microsoft.SemanticKernel` NuGet package across all project files.

### Requirement 2: Migrate LLM Service Implementations to IChatClient

**User Story:** As a developer, I want the three LLM services to use `IChatClient` from `Microsoft.Extensions.AI` instead of `IChatCompletionService` from Semantic Kernel, so that the services use the standard .NET AI abstraction.

#### Acceptance Criteria

1. THE IssueClassifierService SHALL inject `IChatClient` via constructor instead of `IChatCompletionService`.
2. THE BugResolverService SHALL inject `IChatClient` via constructor instead of `IChatCompletionService`.
3. THE CodeChangeGeneratorService SHALL inject `IChatClient` via constructor instead of `IChatCompletionService`.
4. WHEN a chat completion is requested, THE LLM_Service SHALL use `List<ChatMessage>` instead of `ChatHistory` to represent the conversation.
5. WHEN a chat completion is requested, THE LLM_Service SHALL use `ChatOptions` instead of `PromptExecutionSettings` to configure request parameters such as temperature.
6. WHEN a chat completion is requested, THE LLM_Service SHALL call `IChatClient.GetResponseAsync` and process the `ChatResponse` result instead of calling `GetChatMessageContentAsync` and processing `ChatMessageContent`.
7. THE IssueClassifierService SHALL preserve the existing classification behavior: system prompt, JSON parsing, temperature of 0.1, and fallback on error.
8. THE BugResolverService SHALL preserve the existing resolution behavior: system prompt, JSON parsing, temperature of 0.2, and fallback on error.
9. THE CodeChangeGeneratorService SHALL preserve the existing code generation behavior: system prompt, JSON parsing, temperature of 0.5, and fallback on error.

### Requirement 3: Migrate Dependency Injection Setup

**User Story:** As a developer, I want the DI registration to use Agent Framework APIs to register `IChatClient`, so that the composition root uses the new SDK.

#### Acceptance Criteria

1. THE DI_Setup SHALL register `IChatClient` using the Agent Framework's OpenAI provider instead of Semantic Kernel's `AddOpenAIChatCompletion`.
2. THE DI_Setup SHALL read the same `LlmProvider` configuration section (Provider, ModelName, ApiKey, Endpoint) without changes to the configuration schema.
3. WHEN the ApiKey is missing or empty, THE DI_Setup SHALL throw an `InvalidOperationException` with a descriptive message.
4. THE DI_Setup SHALL preserve HTTP resilience configuration with Polly (3 retries, exponential backoff with jitter).
5. WHEN the Provider value is not a supported provider, THE DI_Setup SHALL throw an `InvalidOperationException` with a descriptive message.

### Requirement 4: Rename and Relocate Infrastructure Folder

**User Story:** As a developer, I want the Infrastructure folder structure to reflect the new framework name, so that the codebase is consistent with the technology in use.

#### Acceptance Criteria

1. THE Infrastructure_Project SHALL contain an `AgentFramework` folder (or equivalent) instead of the `SemanticKernel` folder for LLM service files.
2. THE Infrastructure_Project SHALL update all namespace declarations to match the new folder structure.
3. THE DI_Setup file SHALL be renamed from `SemanticKernelSetup.cs` to a name reflecting the Agent Framework (e.g., `AgentFrameworkSetup.cs`).
4. THE InfrastructureServiceExtensions SHALL call the renamed setup method instead of `AddSemanticKernel`.

### Requirement 5: Rename SemanticKernelAgent

**User Story:** As a developer, I want the agent wrapper class to have a name that reflects the new framework, so that the codebase naming is consistent.

#### Acceptance Criteria

1. THE Agent wrapper class SHALL be renamed from `SemanticKernelAgent` to a name reflecting the Agent Framework (e.g., `AiAgent`).
2. THE Program.cs composition root SHALL reference the renamed agent class when building `IAIAgent` instances.
3. THE renamed agent class SHALL preserve the same behavior: holding agent identity and delegating to `IBugResolver`.

### Requirement 6: Migrate Test Helper to IChatClient

**User Story:** As a developer, I want the test helper to implement `IChatClient` instead of `IChatCompletionService`, so that unit tests work with the new abstraction.

#### Acceptance Criteria

1. THE FakeChatClient SHALL implement `IChatClient` from `Microsoft.Extensions.AI`.
2. THE FakeChatClient SHALL support construction with a response string (returning a successful `ChatResponse`).
3. THE FakeChatClient SHALL support construction with an `Exception` (throwing the exception when called).
4. WHEN `GetResponseAsync` is called, THE FakeChatClient SHALL return a `ChatResponse` containing the configured response content.
5. THE FakeChatClient SHALL replace `FakeChatCompletionService` in all unit test files.
6. WHEN all unit tests are executed after migration, THE Test_Suite SHALL produce the same pass/fail results as before the migration.

### Requirement 7: Update All Documentation

**User Story:** As a developer, I want all documentation to reflect the migration to Agent Framework, so that the docs are accurate and up to date.

#### Acceptance Criteria

1. THE semantic-kernel-integration.md document SHALL be renamed or replaced with a document describing the Agent Framework integration.
2. THE new integration document SHALL describe the `IChatClient` abstraction, `ChatMessage`, `ChatOptions`, and `ChatResponse` types used by the LLM services.
3. THE new integration document SHALL describe the DI registration flow using Agent Framework APIs.
4. THE new integration document SHALL preserve the documentation of temperature strategy, service behavior, and actor integration.
5. THE README.md SHALL update all references from "Semantic Kernel" to "Agent Framework" in the architecture diagram, project structure, deep-dive documentation table, and getting started section.
6. THE README.md architecture diagram SHALL reference "Agent Framework Services" instead of "Semantic Kernel Services".

### Requirement 8: Write Migration Note

**User Story:** As a developer, I want a migration note explaining why the project moved from Semantic Kernel to Agent Framework, so that future contributors understand the rationale.

#### Acceptance Criteria

1. THE Migration_Note SHALL be included in the new integration document as a dedicated section.
2. THE Migration_Note SHALL explain that Agent Framework is the official successor to Semantic Kernel.
3. THE Migration_Note SHALL explain that `IChatClient` from `Microsoft.Extensions.AI` is the standard .NET AI abstraction.
4. THE Migration_Note SHALL list the key API mapping changes (e.g., `IChatCompletionService` → `IChatClient`, `ChatHistory` → `List<ChatMessage>`).
5. THE Migration_Note SHALL state that the domain layer required zero changes due to clean architecture.

### Requirement 9: Leverage Provider-Agnostic IChatClient Patterns

**User Story:** As a developer, I want the project to take advantage of `IChatClient` being the standard .NET AI abstraction, so that the LLM integration is cleaner and more portable.

#### Acceptance Criteria

1. THE LLM_Service implementations SHALL depend only on `IChatClient` from `Microsoft.Extensions.AI`, with no direct dependency on OpenAI-specific types.
2. THE DI_Setup SHALL be the only location where the OpenAI-specific provider is referenced, keeping provider selection isolated to the composition root.
3. THE new integration document SHALL document the provider-agnostic benefit: swapping LLM providers requires changes only in the DI setup, not in any service implementation.

### Requirement 10: Ensure Build and Test Integrity

**User Story:** As a developer, I want the entire solution to build and all tests to pass after migration, so that the migration introduces no regressions.

#### Acceptance Criteria

1. WHEN `dotnet build AiSupportWorkflow.sln` is executed, THE Build_System SHALL complete with zero errors.
2. WHEN `dotnet test AiSupportWorkflow.sln` is executed, THE Test_Suite SHALL produce the same number of passing tests as before the migration.
3. THE Solution SHALL contain zero `using` directives referencing `Microsoft.SemanticKernel` namespaces in any source file.
4. THE Solution SHALL contain zero `using` directives referencing `Microsoft.SemanticKernel` namespaces in any test file.
