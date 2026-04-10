# Implementation Plan: Akka.NET Optimization

## Overview

Refactor the Akka.NET actor integration to replace `ActorSelection` with direct `IActorRef` via `IRequiredActor<SupervisorActor>`, eliminate broadcast messaging, centralize agent status collection, improve the supervisor strategy, and document the actor architecture. The implementation follows Clean Architecture: changes flow from Domain (new interface + updated messages) → Application (Orchestrator refactor) → Infrastructure (new bridge + actor changes) → Presentation (DI wiring + endpoint refactor), with test updates and documentation at the end.

## Tasks

- [x] 1. Update the actor message protocol
  - [x] 1.1 Update `ActorMessages.cs` in Domain layer
    - Modify `AssignIssueMessage` record to add `TargetAgentId` as the first parameter: `AssignIssueMessage(string TargetAgentId, IssueRecord Issue, IssueCategory Category)`
    - Modify `AgentStatusQuery` record to accept an optional target: `AgentStatusQuery(string? TargetAgentId)`
    - Add new record `AggregatedAgentStatusResponse(List<AgentStatusResponse> Statuses)`
    - Add new record `AgentNotFoundMessage(string AgentId)`
    - File: `src/AiSupportWorkflow.Domain/Messages/ActorMessages.cs`
    - _Requirements: 6.1, 6.2, 6.3_

- [x] 2. Create ISupervisorActorBridge interface and implementation
  - [x] 2.1 Create `ISupervisorActorBridge` interface in Domain layer
    - Define `Task<ResolutionReport> AssignIssueAsync(string agentId, IssueRecord issue, IssueCategory category, TimeSpan timeout, CancellationToken ct)` method
    - File: `src/AiSupportWorkflow.Domain/Interfaces/ISupervisorActorBridge.cs`
    - _Requirements: 1.1, 1.2_
  - [x] 2.2 Create `SupervisorActorBridge` implementation in Infrastructure layer
    - Implement `ISupervisorActorBridge` using `IRequiredActor<SupervisorActor>` to obtain the supervisor `IActorRef`
    - Use `_supervisor.Ask<ResolutionCompleteMessage>` to send targeted `AssignIssueMessage` and await the response
    - File: `src/AiSupportWorkflow.Infrastructure/Actors/SupervisorActorBridge.cs`
    - _Requirements: 1.1, 1.2_

- [x] 3. Modify SupervisorActor for targeted routing and improved strategy
  - [x] 3.1 Update `SupervisorActor` constructor to accept `ILogger<SupervisorActor>`
    - Add `_logger` field and update constructor signature
    - File: `src/AiSupportWorkflow.Infrastructure/Actors/SupervisorActor.cs`
    - _Requirements: 5.4_
  - [x] 3.2 Replace broadcast in `HandleAssignIssue` with targeted routing
    - Look up the agent by `message.TargetAgentId` in `_agentActors` dictionary
    - Forward to the matching agent only; respond with `AgentNotFoundMessage` if not found
    - _Requirements: 1.3, 1.4, 2.1, 2.4_
  - [x] 3.3 Implement aggregated status query in `HandleStatusQuery`
    - Change handler to `ReceiveAsync<AgentStatusQuery>`
    - If `TargetAgentId` is specified, forward to that single agent (or respond with `AgentNotFoundMessage`)
    - If `TargetAgentId` is null, query all agents in parallel via `Task.WhenAll` and respond with `AggregatedAgentStatusResponse`
    - Include "Unavailable" fallback for agents that fail to respond within timeout
    - _Requirements: 2.2, 2.3, 2.4, 3.1, 3.3_
  - [x] 3.4 Implement exception-type-aware supervisor strategy with logging
    - `TimeoutException`, `HttpRequestException` → `Directive.Restart`
    - `ArgumentException`, `InvalidOperationException` → `Directive.Stop`
    - `OutOfMemoryException` → `Directive.Escalate`
    - Default → `Directive.Restart`
    - Log exception type, actor identifier, and applied directive via `_logger.LogWarning`
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

- [x] 4. Modify Orchestrator to use ISupervisorActorBridge
  - [x] 4.1 Refactor `Orchestrator` constructor and `ResolveWithActorAsync`
    - Replace `ActorSystem actorSystem` parameter with `ISupervisorActorBridge supervisorBridge`
    - Replace `ResolveWithActorAsync` body: call `supervisorBridge.AssignIssueAsync(agent.AgentId, issue, category, TimeSpan.FromMinutes(2), ct)` instead of `ActorSelection` + `Ask`
    - Remove `using Akka.Actor;` import
    - File: `src/AiSupportWorkflow.Application/Services/Orchestrator.cs`
    - _Requirements: 1.1, 1.2_
  - [x] 4.2 Remove Akka package reference from Application.csproj
    - Remove `<PackageReference Include="Akka" Version="1.5.64" />` from `src/AiSupportWorkflow.Application/AiSupportWorkflow.Application.csproj`
    - _Requirements: 1.1_

- [~] 5. Checkpoint
  - Build the solution with `dotnet build AiSupportWorkflow.sln` to verify all compile errors from the refactor are resolved before proceeding. Ensure all tests pass, ask the user if questions arise.

- [ ] 6. Modify VisualizationEndpoints to use IRequiredActor
  - [ ] 6.1 Refactor `/api/support/agents` endpoint
    - Replace `ActorSystem actorSystem` parameter with `IRequiredActor<SupervisorActor> supervisorActor` and `IOptions<WorkflowConfiguration> config`
    - Send a single `AgentStatusQuery(null)` to the supervisor via `supervisorActor.ActorRef.Ask<AggregatedAgentStatusResponse>`
    - Return `response.Statuses` directly instead of iterating agents
    - Remove `QueryAgentStatusesAsync` and `QuerySingleAgentStatusAsync` private methods
    - Add required `using Akka.Hosting;` and `using AiSupportWorkflow.Infrastructure.Actors;` imports
    - File: `src/AiSupportWorkflow.Presentation/Endpoints/VisualizationEndpoints.cs`
    - _Requirements: 3.1, 3.2, 3.3, 4.1, 4.2, 4.3_

- [ ] 7. Update Program.cs DI wiring
  - [ ] 7.1 Update Akka actor setup and register SupervisorActorBridge
    - Resolve `ILogger<SupervisorActor>` in the `WithActors` callback and pass it to `SupervisorActor` constructor
    - Register `ISupervisorActorBridge` → `SupervisorActorBridge` as singleton after the Akka setup
    - File: `src/AiSupportWorkflow.Presentation/Program.cs`
    - _Requirements: 1.1, 5.4_

- [ ] 8. Checkpoint
  - Build the solution and run all existing tests with `dotnet test AiSupportWorkflow.sln`. Ensure all tests pass, ask the user if questions arise.

- [ ] 9. Update existing unit tests for new interfaces
  - [ ] 9.1 Update `OrchestratorTests` to use `ISupervisorActorBridge`
    - Replace `ActorSystem` (`Sys`) dependency with a mocked `ISupervisorActorBridge` via NSubstitute
    - Remove `TestKit` base class inheritance (no longer needed since Orchestrator doesn't use ActorSystem)
    - Update `CreateSut()` to pass the mocked bridge instead of `Sys`
    - Update `SetupFullPipeline` to configure `ISupervisorActorBridge.AssignIssueAsync` mock return instead of creating stub actors
    - Remove `StubSupervisorActor` and `StubAgentActor` inner classes
    - File: `tests/AiSupportWorkflow.UnitTests/OrchestratorTests.cs`
    - _Requirements: 1.1, 1.2_
  - [ ] 9.2 Write unit tests for `SupervisorActorBridge`
    - Test that `AssignIssueAsync` sends the correct `AssignIssueMessage` with `TargetAgentId` to the supervisor and returns the `ResolutionReport` from the response
    - Use `Akka.TestKit.Xunit2` to create a test probe as the supervisor
    - File: `tests/AiSupportWorkflow.UnitTests/SupervisorActorBridgeTests.cs`
    - _Requirements: 1.2_
  - [ ] 9.3 Write unit tests for `SupervisorActor` targeted routing and strategy
    - Test targeted `AssignIssueMessage` routes to the correct agent only
    - Test `AssignIssueMessage` for unknown agent returns `AgentNotFoundMessage`
    - Test `AgentStatusQuery(null)` returns `AggregatedAgentStatusResponse` with all agents
    - Test `AgentStatusQuery("specific_id")` forwards to that agent only
    - Test supervisor strategy: `TimeoutException` → Restart, `ArgumentException` → Stop, `OutOfMemoryException` → Escalate
    - Use `Akka.TestKit.Xunit2` with test probes
    - File: `tests/AiSupportWorkflow.UnitTests/SupervisorActorTests.cs`
    - _Requirements: 1.3, 1.4, 2.1, 2.2, 2.3, 2.4, 5.1, 5.2, 5.3, 5.4_

- [ ] 10. Checkpoint
  - Run all tests with `dotnet test AiSupportWorkflow.sln`. Ensure all tests pass, ask the user if questions arise.

- [ ] 11. Create actor architecture documentation
  - [ ] 11.1 Create `docs/actor-architecture.md`
    - Document the actor hierarchy (SupervisorActor → AIAgentActor children)
    - Document the message protocol (all message types, their fields, and routing behavior)
    - Document the supervisor strategy (exception-type mapping to directives)
    - Document Akka.Hosting integration (actor registration, `IRequiredActor<T>` pattern, DI wiring)
    - Document the `ISupervisorActorBridge` abstraction and Clean Architecture rationale
    - File: `docs/actor-architecture.md`
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_
  - [ ] 11.2 Update `README.md` with link to actor documentation
    - Add a reference and link to `docs/actor-architecture.md` in the README
    - File: `README.md`
    - _Requirements: 7.6_

- [ ] 12. Final checkpoint
  - Build the solution and run all tests with `dotnet test AiSupportWorkflow.sln`. Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation after each major phase
- The design has no Correctness Properties section, so property-based tests are not included
- Unit tests validate specific examples and edge cases for the new components
