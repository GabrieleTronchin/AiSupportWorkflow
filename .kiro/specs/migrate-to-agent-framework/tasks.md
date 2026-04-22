# Implementation Plan: Migrate to Microsoft Agent Framework

## Overview

Migrate the AI Support Workflow project from Microsoft Semantic Kernel (v1.74.0) to Microsoft Agent Framework (v1.2.0+). All changes are confined to the Infrastructure layer, Presentation layer, test projects, and documentation — the Domain and Application layers require zero changes thanks to clean architecture. Each task builds incrementally: NuGet packages first, then DI setup, then service migrations, then agent rename, then test helper and test updates, then property tests, then documentation, with checkpoints to verify the build and tests at key stages.

## Tasks

- [ ] 1. Swap NuGet packages from Semantic Kernel to Agent Framework
  - [ ] 1.1 Update Infrastructure .csproj packages
    - In `src/AiSupportWorkflow.Infrastructure/AiSupportWorkflow.Infrastructure.csproj`, remove `Microsoft.SemanticKernel` (1.74.0) and `Microsoft.SemanticKernel.Connectors.OpenAI` (1.74.0)
    - Add `Microsoft.Agents.AI` (1.2.0) and `Microsoft.Agents.AI.OpenAI` (1.2.0)
    - _Requirements: 1.1, 1.4_
  - [ ] 1.2 Update Presentation .csproj package
    - In `src/AiSupportWorkflow.Presentation/AiSupportWorkflow.Presentation.csproj`, remove `Microsoft.SemanticKernel` (1.74.0)
    - Add `Microsoft.Agents.AI` (1.2.0)
    - _Requirements: 1.2, 1.4_

- [ ] 2. Rewrite DI setup and create AgentFramework folder
  - [ ] 2.1 Create `ChatClientSetup.cs` in the new AgentFramework folder
    - Create `src/AiSupportWorkflow.Infrastructure/AgentFramework/ChatClientSetup.cs`
    - Namespace: `AiSupportWorkflow.Infrastructure.AgentFramework`
    - Implement `AddChatClient` extension method: read `LlmProvider` config, validate API key, create `OpenAIClient` + `OpenAIChatClient`, register as `IChatClient` singleton, configure Polly resilience (3 retries, exponential backoff, jitter)
    - Throw `InvalidOperationException` for missing API key or unsupported provider
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.3, 9.2_
  - [ ] 2.2 Update `InfrastructureServiceExtensions.cs` to use new setup
    - Change `using` from `AiSupportWorkflow.Infrastructure.SemanticKernel` to `AiSupportWorkflow.Infrastructure.AgentFramework`
    - Replace `services.AddSemanticKernel(configuration)` with `services.AddChatClient(configuration)`
    - _Requirements: 4.4_

- [ ] 3. Migrate the three LLM services to IChatClient
  - [ ] 3.1 Migrate `IssueClassifierService`
    - Create `src/AiSupportWorkflow.Infrastructure/AgentFramework/IssueClassifierService.cs`
    - Change constructor injection from `IChatCompletionService` to `IChatClient`
    - Replace `PromptExecutionSettings` with `ChatOptions { Temperature = 0.1f }`
    - Replace `ChatHistory` with `List<ChatMessage>` (system + user messages)
    - Replace `GetChatMessageContentAsync` with `GetResponseAsync`, use `response.Text`
    - Preserve system prompt, JSON parsing logic, and error fallback behavior
    - _Requirements: 2.1, 2.4, 2.5, 2.6, 2.7, 9.1_
  - [ ] 3.2 Migrate `BugResolverService`
    - Create `src/AiSupportWorkflow.Infrastructure/AgentFramework/BugResolverService.cs`
    - Same pattern as IssueClassifierService: `IChatClient`, `ChatOptions { Temperature = 0.2f }`, `List<ChatMessage>`, `GetResponseAsync`
    - Preserve system prompt, JSON parsing logic, and escalation fallback behavior
    - _Requirements: 2.2, 2.4, 2.5, 2.6, 2.8, 9.1_
  - [ ] 3.3 Migrate `CodeChangeGeneratorService`
    - Create `src/AiSupportWorkflow.Infrastructure/AgentFramework/CodeChangeGeneratorService.cs`
    - Same pattern: `IChatClient`, `ChatOptions { Temperature = 0.5f }`, `List<ChatMessage>`, `GetResponseAsync`
    - Preserve system prompt, JSON parsing logic, and fallback PR behavior
    - _Requirements: 2.3, 2.4, 2.5, 2.6, 2.9, 9.1_
  - [ ] 3.4 Delete the old `SemanticKernel` folder
    - Remove `src/AiSupportWorkflow.Infrastructure/SemanticKernel/` directory and all files within it (`SemanticKernelSetup.cs`, `IssueClassifierService.cs`, `BugResolverService.cs`, `CodeChangeGeneratorService.cs`)
    - _Requirements: 4.1, 4.2_

- [ ] 4. Rename SemanticKernelAgent to AiAgent
  - [ ] 4.1 Create `AiAgent.cs` and remove `SemanticKernelAgent.cs`
    - Create `src/AiSupportWorkflow.Infrastructure/Agents/AiAgent.cs` with identical behavior (holds agent identity, delegates to `IBugResolver`)
    - Delete `src/AiSupportWorkflow.Infrastructure/Agents/SemanticKernelAgent.cs`
    - _Requirements: 5.1, 5.3_
  - [ ] 4.2 Update `Program.cs` to reference `AiAgent`
    - Replace `new SemanticKernelAgent(...)` with `new AiAgent(...)` in the agent factory lambda
    - _Requirements: 5.2_

- [ ] 5. Checkpoint — Verify solution builds
  - Ensure `dotnet build AiSupportWorkflow.sln` completes with zero errors. Ask the user if questions arise.
  - _Requirements: 1.3, 10.1_

- [ ] 6. Migrate test helper and update unit tests
  - [ ] 6.1 Create `FakeChatClient` test helper
    - Create `tests/AiSupportWorkflow.UnitTests/Helpers/FakeChatClient.cs`
    - Implement `IChatClient` from `Microsoft.Extensions.AI`
    - Support construction with a response string (returns `ChatResponse` with assistant message) and with an `Exception` (throws on call)
    - Implement `GetResponseAsync`, minimal `GetStreamingResponseAsync`, and `Dispose`
    - Delete `tests/AiSupportWorkflow.UnitTests/Helpers/FakeChatCompletionService.cs`
    - _Requirements: 6.1, 6.2, 6.3, 6.4_
  - [ ] 6.2 Update `IssueClassifierTests.cs`
    - Replace `using Microsoft.SemanticKernel.ChatCompletion` and `using AiSupportWorkflow.Infrastructure.SemanticKernel` with `using AiSupportWorkflow.Infrastructure.AgentFramework`
    - Replace `IChatCompletionService` with `IChatClient` in `CreateSut`
    - Replace `FakeChatCompletionService` with `FakeChatClient`
    - _Requirements: 6.5, 10.3, 10.4_
  - [ ] 6.3 Update `BugResolverTests.cs`
    - Same using/type replacements as IssueClassifierTests
    - _Requirements: 6.5, 10.3, 10.4_
  - [ ] 6.4 Update `CodeChangeGeneratorTests.cs`
    - Same using/type replacements as IssueClassifierTests
    - _Requirements: 6.5, 10.3, 10.4_

- [ ] 7. Checkpoint — Verify all existing tests pass
  - Run `dotnet test AiSupportWorkflow.sln` and ensure all tests pass with the same results as before migration. Ask the user if questions arise.
  - _Requirements: 6.6, 10.2_

- [ ] 8. Write property-based tests for migration correctness
  - [ ] 8.1 Create property test file for migration properties
    - Create `tests/AiSupportWorkflow.PropertyTests/MigrationCorrectnessTests.cs`
    - Add necessary project references if missing (Infrastructure project is already referenced)
    - Use FsCheck.Xunit with `MaxTest = 100`
    - _Requirements: 10.2_
  - [ ]* 8.2 Write property test: ChatOptions temperature preservation
    - **Property 1: ChatOptions temperature preservation**
    - For any float in [0.0, 2.0], constructing `ChatOptions` with that temperature and reading it back returns the same value
    - **Validates: Requirements 2.5**
  - [ ]* 8.3 Write property test: Classification JSON parsing round-trip
    - **Property 2: Classification JSON parsing round-trip**
    - For any valid category, confidence in [0,1], and reasoning string, build JSON → parse via `IssueClassifierService` parser → verify matching `ClassificationResult`
    - **Validates: Requirements 2.7**
  - [ ]* 8.4 Write property test: Resolution JSON parsing round-trip
    - **Property 3: Resolution JSON parsing round-trip**
    - For any valid resolution JSON fields, build JSON → parse via `BugResolverService` parser → verify matching `ResolutionReport`
    - **Validates: Requirements 2.8**
  - [ ]* 8.5 Write property test: Pull request JSON parsing round-trip
    - **Property 4: Pull request JSON parsing round-trip**
    - For any valid PR JSON fields, build JSON → parse via `CodeChangeGeneratorService` parser → verify matching `PullRequest`
    - **Validates: Requirements 2.9**
  - [ ]* 8.6 Write property test: Unsupported provider rejection
    - **Property 5: Unsupported provider rejection**
    - For any provider string not equal to "openai" (case-insensitive), DI setup throws `InvalidOperationException`
    - **Validates: Requirements 3.5**
  - [ ]* 8.7 Write property test: Agent identity preservation and delegation
    - **Property 6: Agent identity preservation and delegation**
    - For any agentId, teamName, and AgentRole, constructing `AiAgent` preserves identity properties and delegates `AnalyzeAndResolveAsync` to `IBugResolver`
    - **Validates: Requirements 5.3**
  - [ ]* 8.8 Write property test: FakeChatClient response round-trip
    - **Property 7: FakeChatClient response round-trip**
    - For any non-null string, constructing `FakeChatClient` and calling `GetResponseAsync` returns a `ChatResponse` with matching text
    - **Validates: Requirements 6.4**

- [ ] 9. Checkpoint — Verify all tests pass including property tests
  - Run `dotnet test AiSupportWorkflow.sln` and ensure all tests pass. Ask the user if questions arise.
  - _Requirements: 10.2_

- [ ] 10. Update documentation
  - [ ] 10.1 Replace `semantic-kernel-integration.md` with `agent-framework-integration.md`
    - Create `docs/agent-framework-integration.md` describing the Agent Framework integration: `IChatClient` abstraction, `ChatMessage`, `ChatOptions`, `ChatResponse`, DI registration flow, temperature strategy, service behavior, actor integration, and provider-agnostic benefits
    - Include a Migration Note section explaining: Agent Framework is the successor to Semantic Kernel, `IChatClient` is the standard .NET AI abstraction, key API mapping changes, and domain layer required zero changes
    - Delete `docs/semantic-kernel-integration.md`
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 8.1, 8.2, 8.3, 8.4, 8.5, 9.3_
  - [ ] 10.2 Update `README.md`
    - Replace all "Semantic Kernel" references with "Agent Framework"
    - Update architecture diagram to reference "Agent Framework Services" instead of "Semantic Kernel Services"
    - Update project structure section (AgentFramework folder instead of SemanticKernel, AiAgent instead of SemanticKernelAgent)
    - Update deep-dive documentation table to link to `agent-framework-integration.md`
    - Update Getting Started section and NuGet package references
    - _Requirements: 7.5, 7.6_

- [ ] 11. Final checkpoint — Full build and test verification
  - Run `dotnet build AiSupportWorkflow.sln` (zero errors) and `dotnet test AiSupportWorkflow.sln` (all tests pass)
  - Verify zero references to `Microsoft.SemanticKernel` in any .csproj or .cs file
  - Verify `AgentFramework/` folder exists and `SemanticKernel/` folder is removed
  - Ask the user if questions arise.
  - _Requirements: 1.3, 1.4, 10.1, 10.2, 10.3, 10.4_

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation after each major phase
- Property tests validate the 7 correctness properties defined in the design document
- Unit tests validate specific examples and edge cases
- The Domain and Application layers require zero changes — all modifications are in Infrastructure, Presentation, and test projects
