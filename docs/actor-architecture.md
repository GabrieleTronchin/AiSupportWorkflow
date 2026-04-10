# Actor Architecture

This document describes the Akka.NET actor system used in the AI Support Workflow project, including the actor hierarchy, message protocol, supervision strategy, and integration with .NET dependency injection via Akka.Hosting.

## Actor Hierarchy

```
ActorSystem ("SupportWorkflowSystem")
└── SupervisorActor ("/user/supervisor")
    ├── AIAgentActor ("{TeamName}_{Role}")   e.g. "TeamA_BackendDeveloper"
    ├── AIAgentActor ("{TeamName}_{Role}")   e.g. "TeamA_FrontendDeveloper"
    ├── AIAgentActor ("{TeamName}_{Role}")   e.g. "TeamA_QAEngineer"
    ├── AIAgentActor ("{TeamName}_{Role}")   e.g. "TeamB_BackendDeveloper"
    ├── AIAgentActor ("{TeamName}_{Role}")   e.g. "TeamB_FrontendDeveloper"
    └── AIAgentActor ("{TeamName}_{Role}")   e.g. "TeamB_QAEngineer"
```

### SupervisorActor

- **Type:** `AiSupportWorkflow.Infrastructure.Actors.SupervisorActor`
- **Path:** `/user/supervisor`
- **Role:** Parent actor that creates and manages all `AIAgentActor` children. Routes incoming messages to the correct child by agent ID. Aggregates status responses when queried for all agents. Defines the supervision strategy for child failures.
- **Constructor:** `SupervisorActor(IEnumerable<IAIAgent> agents, ILogger<SupervisorActor> logger)`
- **Internal state:** `Dictionary<string, IActorRef> _agentActors` — maps agent ID strings to child actor references.

### AIAgentActor

- **Type:** `AiSupportWorkflow.Infrastructure.Actors.AIAgentActor`
- **Path:** `/user/supervisor/{agentId}`
- **Role:** Represents a single AI agent. Handles issue assignment by delegating to the underlying `IAIAgent` for LLM-powered analysis, and responds to status queries with its current state.
- **Constructor:** `AIAgentActor(IAIAgent agent)`
- **Internal state:** `_status` (string, defaults to `"Idle"`) and `_lastAction` (nullable string).

## Message Protocol

All messages are immutable C# records defined in `src/AiSupportWorkflow.Domain/Messages/ActorMessages.cs`.

### AssignIssueMessage

```csharp
public record AssignIssueMessage(string TargetAgentId, IssueRecord Issue, IssueCategory Category);
```

| Field | Type | Description |
|-------|------|-------------|
| `TargetAgentId` | `string` | ID of the agent the supervisor should route this message to |
| `Issue` | `IssueRecord` | The support issue to resolve |
| `Category` | `IssueCategory` | Classification category (BackendBug, FrontendBug, QualityTestIssue) |

**Routing:** The `SupervisorActor` looks up `TargetAgentId` in its `_agentActors` dictionary and forwards the message to the matching `AIAgentActor`. If no match is found, it replies with `AgentNotFoundMessage`.

### ResolutionCompleteMessage

```csharp
public record ResolutionCompleteMessage(Guid IssueId, ResolutionReport Report);
```

**Direction:** `AIAgentActor` → sender (via `Tell`). Sent after the agent completes issue analysis.

### AgentStatusQuery

```csharp
public record AgentStatusQuery(string? TargetAgentId);
```

| Field | Type | Description |
|-------|------|-------------|
| `TargetAgentId` | `string?` | If non-null, query only that agent. If null, query all agents. |

**Routing:**
- `TargetAgentId` is non-null → `SupervisorActor` forwards to the matching agent (or replies with `AgentNotFoundMessage`).
- `TargetAgentId` is null → `SupervisorActor` queries all children in parallel via `Task.WhenAll` and replies with `AggregatedAgentStatusResponse`.

### AgentStatusResponse

```csharp
public record AgentStatusResponse(string AgentId, string Status, string? LastAction);
```

**Direction:** `AIAgentActor` → sender. Returned in response to an `AgentStatusQuery`.

### AggregatedAgentStatusResponse

```csharp
public record AggregatedAgentStatusResponse(List<AgentStatusResponse> Statuses);
```

**Direction:** `SupervisorActor` → sender. Contains the collected status of all registered agents. Agents that fail to respond within 5 seconds are included with status `"Unavailable"`.

### AgentNotFoundMessage

```csharp
public record AgentNotFoundMessage(string AgentId);
```

**Direction:** `SupervisorActor` → sender. Returned when a message targets an agent ID not present in the supervisor's registry.

## Supervision Strategy

The `SupervisorActor` uses a `OneForOneStrategy` (only the failing child is affected) with the following parameters:

- **Max retries:** 3
- **Time window:** 1 minute

### Exception-to-Directive Mapping

| Exception Type | Directive | Rationale |
|----------------|-----------|-----------|
| `TimeoutException` | `Restart` | Transient network/LLM timeout — retry is appropriate |
| `HttpRequestException` | `Restart` | Transient HTTP failure — retry is appropriate |
| `ArgumentException` | `Stop` | Programming error — restarting won't help |
| `InvalidOperationException` | `Stop` | Invalid state — restarting won't help |
| `OutOfMemoryException` | `Escalate` | Fatal — escalate to parent for system-level handling |
| All other exceptions | `Restart` | Default recovery behavior |

Every decision is logged at `Warning` level with structured fields:

```
Supervisor decision: Actor={Actor}, Exception={ExceptionType}, Directive={Directive}
```

## Akka.Hosting Integration

### Actor Registration (Program.cs)

The actor system is configured using `AddAkka` from Akka.Hosting. Inside the `WithActors` callback, the `SupervisorActor` is created and registered in the actor registry:

```csharp
builder.Services.AddAkka("SupportWorkflowSystem", (akkaBuilder, sp) =>
{
    akkaBuilder.WithActors((system, registry, resolver) =>
    {
        var agents = resolver.GetService<IEnumerable<IAIAgent>>()
            ?? Enumerable.Empty<IAIAgent>();
        var logger = resolver.GetService<ILogger<SupervisorActor>>()!;

        var supervisorProps = Props.Create(() => new SupervisorActor(agents, logger));
        var supervisor = system.ActorOf(supervisorProps, "supervisor");
        registry.Register<SupervisorActor>(supervisor);
    });
});
```

### IRequiredActor&lt;T&gt; Pattern

`IRequiredActor<SupervisorActor>` is the Akka.Hosting mechanism for injecting a typed, direct `IActorRef` via .NET DI. It replaces the legacy `ActorSelection` approach that resolved actors by string path on every call.

**Consumers:**
- `SupervisorActorBridge` — uses `IRequiredActor<SupervisorActor>` to obtain the supervisor reference for the Application layer.
- `VisualizationEndpoints` — uses `IRequiredActor<SupervisorActor>` directly in the Minimal API endpoint to query agent statuses.

## ISupervisorActorBridge — Clean Architecture Abstraction

### Problem

The `Orchestrator` lives in the Application layer, which must not depend on Infrastructure packages like Akka.NET or Akka.Hosting. However, it needs to communicate with the `SupervisorActor` to assign issues to agents.

### Solution

A domain-level interface `ISupervisorActorBridge` abstracts the actor communication:

```csharp
// Domain layer — no Akka dependency
public interface ISupervisorActorBridge
{
    Task<ResolutionReport> AssignIssueAsync(
        string agentId, IssueRecord issue, IssueCategory category,
        TimeSpan timeout, CancellationToken ct = default);
}
```

The Infrastructure layer provides the implementation `SupervisorActorBridge`, which wraps `IRequiredActor<SupervisorActor>` and uses the Akka `Ask` pattern internally:

```csharp
// Infrastructure layer
public class SupervisorActorBridge(IRequiredActor<SupervisorActor> supervisorActor)
    : ISupervisorActorBridge
{
    private readonly IActorRef _supervisor = supervisorActor.ActorRef;

    public async Task<ResolutionReport> AssignIssueAsync(
        string agentId, IssueRecord issue, IssueCategory category,
        TimeSpan timeout, CancellationToken ct)
    {
        var message = new AssignIssueMessage(agentId, issue, category);
        var response = await _supervisor.Ask<ResolutionCompleteMessage>(message, timeout, ct);
        return response.Report;
    }
}
```

Registered in DI as:

```csharp
builder.Services.AddSingleton<ISupervisorActorBridge, SupervisorActorBridge>();
```

### Dependency Flow

```
Domain:         ISupervisorActorBridge, ActorMessages (zero external packages)
Application:    Orchestrator → ISupervisorActorBridge (no Akka dependency)
Infrastructure: SupervisorActorBridge implements ISupervisorActorBridge (uses Akka.Hosting)
Presentation:   DI wiring, VisualizationEndpoints uses IRequiredActor<SupervisorActor> directly
```

This keeps the Application layer free of any Akka package references while still enabling full actor communication through the bridge abstraction.
