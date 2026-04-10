# Requirements Document

## Introduction

This document defines the requirements for optimizing Akka.NET usage in the AI Support Workflow project. Codebase analysis identified several anti-patterns and improvement opportunities: use of `ActorSelection` instead of direct references (`IActorRef`), indiscriminate message broadcasting in `SupervisorActor`, sequential agent queries, missing `IRequiredActor<T>` pattern from Akka.Hosting, and an overly generic supervisor strategy. The proposed optimizations follow official Akka.NET best practices and aim to improve performance, reliability, and maintainability. Additionally, a documentation requirement ensures the actor architecture is well-documented for the team.

## Glossary

- **Actor_System**: The Akka.NET `ActorSystem` instance named "SupportWorkflowSystem" configured in `Program.cs`
- **Supervisor_Actor**: The `SupervisorActor` that manages the lifecycle of child agent actors
- **Agent_Actor**: An instance of `AIAgentActor` representing a single AI agent in the system
- **Actor_Registry**: The Akka.Hosting `ActorRegistry` that maps .NET types to actor references
- **IRequiredActor**: The Akka.Hosting `IRequiredActor<T>` interface providing typed `IActorRef` injection via DI
- **Actor_Selection**: The Akka.NET `ActorSelection` mechanism that resolves actors via string path with network traversal
- **IActorRef**: A direct, cached reference to an Akka.NET actor, more efficient than Actor_Selection
- **Orchestrator**: The `Orchestrator` service that coordinates the support workflow and communicates with actors
- **Visualization_Endpoints**: The API endpoints in `VisualizationEndpoints` that query agent status
- **Supervisor_Strategy**: The `SupervisorStrategy` that defines how the supervisor handles child actor failures
- **Assign_Issue_Message**: The `AssignIssueMessage` sent to assign an issue to a specific agent
- **Agent_Status_Query**: The `AgentStatusQuery` sent to query an agent's status
- **Agent_Status_Response**: The `AgentStatusResponse` returned by an Agent_Actor with its current status
- **Actor_Documentation**: A markdown file documenting the actor architecture, message flow, and supervision strategy

## Requirements

### Requirement 1: Replace ActorSelection with IRequiredActor in Orchestrator

**User Story:** As a developer, I want the Orchestrator to use a direct actor reference via `IRequiredActor<SupervisorActor>` so that the string path resolution cost is eliminated on every request.

#### Acceptance Criteria

1. THE Orchestrator SHALL receive the Supervisor_Actor reference via IRequiredActor in its constructor, instead of injecting the entire Actor_System
2. WHEN the Orchestrator needs to send an Assign_Issue_Message to an agent, THE Orchestrator SHALL send the message to the Supervisor_Actor via a direct IActorRef, without using Actor_Selection
3. WHEN the Supervisor_Actor receives an Assign_Issue_Message, THE Supervisor_Actor SHALL route the message exclusively to the Agent_Actor matching the agent specified in the message
4. IF the Supervisor_Actor receives an Assign_Issue_Message for a non-existent agent, THEN THE Supervisor_Actor SHALL respond to the sender with an error message that includes the requested agent identifier

### Requirement 2: Eliminate broadcast in SupervisorActor

**User Story:** As a developer, I want the SupervisorActor to route messages only to the correct target agent so that all agents do not receive and process messages not intended for them.

#### Acceptance Criteria

1. WHEN the Supervisor_Actor receives an Assign_Issue_Message, THE Supervisor_Actor SHALL forward the message exclusively to the Agent_Actor whose identifier matches the target agent field in the message
2. WHEN the Supervisor_Actor receives an Agent_Status_Query with a specified agent identifier, THE Supervisor_Actor SHALL forward the query exclusively to the matching Agent_Actor
3. WHEN the Supervisor_Actor receives an Agent_Status_Query without a specified agent identifier, THE Supervisor_Actor SHALL collect status responses from all Agent_Actor instances and respond to the sender with an aggregated list
4. IF the Supervisor_Actor receives a message targeting an Agent_Actor not present in its internal registry, THEN THE Supervisor_Actor SHALL respond to the sender with an error message that includes the missing agent identifier

### Requirement 3: Parallelize agent status queries

**User Story:** As a developer, I want agent status queries to execute in parallel so that the latency of the `/api/support/agents` endpoint is reduced.

#### Acceptance Criteria

1. WHEN the Visualization_Endpoints receive a request for all agent statuses, THE Visualization_Endpoints SHALL execute all status queries in parallel using `Task.WhenAll`
2. WHEN the Visualization_Endpoints receive a request for all agent statuses, THE Visualization_Endpoints SHALL return results for all agents, including those that responded with an error
3. IF a single Agent_Actor does not respond within the configured timeout, THEN THE Visualization_Endpoints SHALL include a response with status "Unavailable" for that agent without blocking the other queries

### Requirement 4: Replace ActorSelection in VisualizationEndpoints

**User Story:** As a developer, I want the VisualizationEndpoints to use the Supervisor_Actor via IRequiredActor instead of resolving each agent individually with ActorSelection, so that communication is centralized and resolution cost is reduced.

#### Acceptance Criteria

1. THE Visualization_Endpoints SHALL obtain the Supervisor_Actor reference via IRequiredActor, without using Actor_Selection
2. WHEN the Visualization_Endpoints need to query agent statuses, THE Visualization_Endpoints SHALL send a single Agent_Status_Query to the Supervisor_Actor, delegating response collection to the Supervisor_Actor
3. THE Visualization_Endpoints SHALL receive from the Supervisor_Actor an aggregated response containing the status of all registered agents

### Requirement 5: Improve supervisor strategy

**User Story:** As a developer, I want the SupervisorActor's supervision strategy to handle exceptions differently based on type, so that unnecessary restarts are avoided for fatal errors and transient failures are handled with appropriate recovery.

#### Acceptance Criteria

1. WHEN an Agent_Actor throws a transient exception (e.g., `TimeoutException`, `HttpRequestException`), THE Supervisor_Strategy SHALL restart the Agent_Actor
2. WHEN an Agent_Actor throws a fatal exception (e.g., `OutOfMemoryException`), THE Supervisor_Strategy SHALL escalate the exception to the parent level instead of restarting the actor
3. WHEN an Agent_Actor throws an `ArgumentException` or `InvalidOperationException`, THE Supervisor_Strategy SHALL stop the Agent_Actor
4. WHEN the Supervisor_Strategy decides a directive for an exception, THE Supervisor_Strategy SHALL log the exception type, the actor identifier, and the applied directive

### Requirement 6: Update actor message protocol for targeted routing

**User Story:** As a developer, I want the actor message protocol to support targeted routing to a specific agent through the SupervisorActor, so that the need to resolve child actor paths externally is eliminated.

#### Acceptance Criteria

1. THE Assign_Issue_Message SHALL include a field that identifies the target agent to which the Supervisor_Actor must route the message
2. THE Agent_Status_Query SHALL support a variant that specifies the identifier of a single agent to query
3. THE Actor_System SHALL define an aggregated status response message that contains the list of statuses for all agents

### Requirement 7: Create actor architecture documentation

**User Story:** As a developer, I want a documentation file that explains how actors work in the project, so that the team has a clear reference for the actor architecture, message flow, and supervision strategy.

#### Acceptance Criteria

1. THE Actor_Documentation SHALL be located in a `docs/` folder at the same level as `src/` and `tests/`
2. THE Actor_Documentation SHALL describe the actor hierarchy, including the Supervisor_Actor and all Agent_Actor types
3. THE Actor_Documentation SHALL describe the message protocol, including all message types and their routing behavior
4. THE Actor_Documentation SHALL describe the Supervisor_Strategy, including exception-specific handling and recovery directives
5. THE Actor_Documentation SHALL describe how actors are registered and resolved using Akka.Hosting and IRequiredActor
6. WHEN the Actor_Documentation is created, THE project README SHALL be updated to include a reference and link to the Actor_Documentation file
