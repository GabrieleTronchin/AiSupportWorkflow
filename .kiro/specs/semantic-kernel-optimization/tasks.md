# Implementation Plan: Semantic Kernel Optimization

## Overview

Refactor the Semantic Kernel integration to fix the captive dependency antipattern, add provider-agnostic prompt execution settings, add HTTP-level retry/resilience, switch to direct `IChatCompletionService` injection, and abstract provider configuration. Foundational changes (config, DI, resilience) come first, then service refactoring, then test updates, then property tests.

## Tasks

- [x] 1. Create provider-agnostic configuration model and update config binding
  - [x] 1.1 Create `LlmProviderConfiguration` class replacing `OpenAIConfiguration`
    - Create `src/AiSupportWorkflow.Infrastructure/Configuration/LlmProviderConfiguration.cs` with properties: `Provider` (default `"OpenAI"`), `ModelName` (default `"gpt-4o-mini"`), `ApiKey` (default `""`), `Endpoint` (nullable string)
    - Delete or keep `OpenAIConfiguration.cs` for backward compatibility during transition
    - _Requirements: 5.1, 5.4_

  - [x] 1.2 Update `appsettings.json` to use `LlmProvider` section
    - Replace the `"OpenAI"` section with `"LlmProvider": { "Provider": "OpenAI", "ModelName": "gpt-4o-mini" }`
    - _Requirements: 5.5_

  - [x] 1.3 Update `InfrastructureServiceExtensions` to bind `LlmProviderConfiguration`
    - Change `services.Configure<OpenAIConfiguration>(...)` to `services.Configure<LlmProviderConfiguration>(configuration.GetSection("LlmProvider"))`
    - _Requirements: 5.4_

- [x] 2. Refactor `SemanticKernelSetup` with provider switch and resilience pipeline
  - [x] 2.1 Refactor `SemanticKernelSetup.AddSemanticKernel` to read `LlmProviderConfiguration`
    - Read config from `"LlmProvider"` section instead of `"OpenAI"`
    - Add `switch` on `config.Provider.ToLowerInvariant()` with `"openai"` case calling `AddOpenAIChatCompletion`
    - Throw `InvalidOperationException` for unsupported provider names
    - _Requirements: 5.2, 5.3, 5.6_

  - [x] 2.2 Add `Microsoft.Extensions.Http.Resilience` NuGet package to Infrastructure project
    - Add `<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.6.0" />` to `AiSupportWorkflow.Infrastructure.csproj`
    - _Requirements: 3.4_

  - [x] 2.3 Add resilience pipeline to `SemanticKernelSetup`
    - Call `services.ConfigureHttpClientDefaults` with `AddStandardResilienceHandler` configured for max 3 retries, exponential backoff with jitter
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.6_

- [x] 3. Checkpoint
  - Ensure the solution builds successfully with `dotnet build AiSupportWorkflow.sln`. Ask the user if questions arise.

- [x] 4. Refactor LLM services to use direct `IChatCompletionService` injection with `PromptExecutionSettings`
  - [x] 4.1 Refactor `IssueClassifierService`
    - Replace constructor parameter `Kernel kernel` with `IChatCompletionService chatService`
    - Remove `kernel.GetRequiredService<IChatCompletionService>()` call, use `chatService` directly
    - Add static `PromptExecutionSettings` field with temperature `0.1` via `ExtensionData`
    - Pass settings to `GetChatMessageContentAsync`
    - Remove `using Microsoft.SemanticKernel;` (Kernel namespace) if no longer needed
    - _Requirements: 1.1, 1.2, 2.1, 2.4, 2.5, 4.1, 4.2, 5.7_

  - [x] 4.2 Refactor `BugResolverService`
    - Same pattern as 4.1: replace `Kernel` with `IChatCompletionService`, add static settings with temperature `0.2`
    - _Requirements: 1.1, 1.2, 2.2, 2.4, 2.5, 4.1, 4.2, 5.7_

  - [x] 4.3 Refactor `CodeChangeGeneratorService`
    - Same pattern as 4.1: replace `Kernel` with `IChatCompletionService`, add static settings with temperature `0.5`
    - _Requirements: 1.1, 1.2, 2.3, 2.4, 2.5, 4.1, 4.2, 5.7_

- [x] 5. Update unit tests to inject `IChatCompletionService` directly
  - [x] 5.1 Update `IssueClassifierTests`
    - Simplify `CreateSut` to `new IssueClassifierService(chatService, logger)` — remove `Kernel.CreateBuilder()` usage
    - Remove `using Microsoft.SemanticKernel;` import
    - _Requirements: 4.4_

  - [x] 5.2 Update `BugResolverTests`
    - Same simplification as 5.1
    - _Requirements: 4.4_

  - [x] 5.3 Update `CodeChangeGeneratorTests`
    - Same simplification as 5.1
    - _Requirements: 4.4_

- [x] 6. Checkpoint
  - Ensure all existing tests pass with `dotnet test AiSupportWorkflow.sln`. Ask the user if questions arise.

- [ ]* 7. Write property tests for correctness properties
  - [ ]* 7.1 Write property test: Deterministic services use low temperature
    - **Property 1: Deterministic services use low temperature**
    - Create a capturing `IChatCompletionService` fake that records the `PromptExecutionSettings` passed to it
    - For any valid `IssueRecord`, verify `IssueClassifierService` passes temperature in [0.0, 0.3]
    - For any valid `IssueRecord` and `AgentAssignment`, verify `BugResolverService` passes temperature in [0.0, 0.3]
    - Add to `tests/AiSupportWorkflow.PropertyTests/SemanticKernelOptimizationProperties.cs`
    - **Validates: Requirements 2.1, 2.2**

  - [ ]* 7.2 Write property test: Creative service uses moderate temperature
    - **Property 2: Creative service uses moderate temperature**
    - For any valid `ResolutionReport`, verify `CodeChangeGeneratorService` passes temperature in [0.3, 0.7]
    - **Validates: Requirements 2.3**

  - [ ]* 7.3 Write property test: LLM service fallback on exception
    - **Property 3: LLM service fallback on exception**
    - For any valid input and any exception type, verify each LLM service returns a well-formed fallback result (not throws)
    - **Validates: Requirements 3.5, 4.5**

  - [ ]* 7.4 Write property test: Behavioral equivalence — classification
    - **Property 4: Behavioral equivalence — classification**
    - For any valid `IssueRecord` and any JSON response string, verify the refactored `IssueClassifierService` produces the same `ClassificationResult` as expected from the parsing logic
    - **Validates: Requirements 1.3, 4.5**

  - [ ]* 7.5 Write property test: Behavioral equivalence — bug resolution
    - **Property 5: Behavioral equivalence — bug resolution**
    - For any valid `IssueRecord`, `AgentAssignment`, and any JSON response string, verify the refactored `BugResolverService` produces the expected `ResolutionReport`
    - **Validates: Requirements 1.3, 4.5**

  - [ ]* 7.6 Write property test: Behavioral equivalence — code change generation
    - **Property 6: Behavioral equivalence — code change generation**
    - For any valid `ResolutionReport` and any JSON response string, verify the refactored `CodeChangeGeneratorService` produces a `PullRequest` with correct field mapping
    - **Validates: Requirements 1.3, 4.5**

  - [ ]* 7.7 Write property test: Provider configuration round trip
    - **Property 7: Provider configuration round trip**
    - For any valid `LlmProviderConfiguration`, serializing to JSON and deserializing back produces an equivalent object
    - **Validates: Requirements 5.1**

  - [ ]* 7.8 Write property test: Unsupported provider rejection
    - **Property 8: Unsupported provider rejection**
    - For any provider string not in `{"openai"}`, calling `AddSemanticKernel` throws `InvalidOperationException`
    - **Validates: Requirements 5.2, 5.6**

- [x] 8. Final checkpoint
  - Ensure all tests pass with `dotnet test AiSupportWorkflow.sln`. Ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The existing `FakeChatCompletionService` in `tests/AiSupportWorkflow.UnitTests/Helpers/` will be extended with a capturing variant for property tests that need to verify `PromptExecutionSettings`
