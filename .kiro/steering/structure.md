# Project Structure

## Topology

```
Dashboard (:5173) ─┐
HTTP clients ──────┴─> Gateway (:5080) ─┬─> Engine service (:5081)
                                        └─> Module services (support :5082, template :5083)
Engine ──(GET /_module/package, POST /_module/tools/{name})──> Module services
Module services ──(POST /api/engine/workflows/{id}/runs)──> Engine
```

Service-to-service calls go directly (service discovery under Aspire), never through the gateway.

## Repository layout

```
LlmFlows.slnx                        # single solution
Directory.Build.props, Directory.Packages.props, nuget.config, .editorconfig, global.json
backend/
├── schemas/                         # module/workflow/samples JSON Schemas (embedded in LlmFlows.Definitions)
├── protos/run_monitor.proto         # gRPC contract (engine + dashboard codegen)
├── src/
│   ├── Engine/
│   │   ├── LlmFlows.Contracts/      # HTTP DTOs: module package, tool invocation, engine API views, RunStatus/RunEventKind
│   │   ├── LlmFlows.Definitions/    # YAML → JsonNode reader, model records, ModuleLoader, DefinitionValidator, Scriban TemplateEngine
│   │   ├── LlmFlows.Runtime/        # WorkflowCompiler, step handlers, RunOrchestrator, RunRegistry, HumanInputRegistry,
│   │   │                            #   IntakeService, InboxProcessor, ModuleCatalog(+Service), ModuleClient, LLM (Demo, telemetry)
│   │   └── LlmFlows.Persistence/    # EF Core InMemory EngineDbContext, EfRunStore (dual-write), EfInboxStore
│   ├── Services/
│   │   ├── LlmFlows.Engine.Service/ # EngineHost, /api/engine endpoints, RunMonitorService (gRPC), engine.http
│   │   └── LlmFlows.Gateway/        # GatewayHost, GatewayRoutes, GatewayMiddleware (correlation id, 503)
│   ├── Sdk/
│   │   ├── LlmFlows.Module.Sdk/     # AddLlmFlowsModule/MapLlmFlowsModule, ModuleTool<TIn,TOut>, IEngineClient, ModuleDefinitionAssert
│   │   └── LlmFlows.Testing/        # PlatformHarness: engine, modules and gateway in-process on TestServer
│   └── AppHost/
│       ├── LlmFlows.AppHost/        # Aspire AppHost, ModuleWiring, AppHost:Modules toggles
│       └── LlmFlows.ServiceDefaults/ # health, OpenTelemetry, service discovery, resilience
└── tests/
    ├── LlmFlows.Engine.Tests/       # definitions, runtime, persistence, LLM, endpoints (+ FsCheck properties)
    ├── LlmFlows.Gateway.Tests/      # routes, forwarding, correlation id, 503
    ├── LlmFlows.Integration.Tests/  # support + fixture modules: isolation, module down, version reload, gRPC-Web via gateway
    ├── LlmFlows.Architecture.Tests/ # dependency rules (project files + NetArchTest)
    └── Fixtures/LlmFlows.Fixtures.Module/  # test-only module covering every step kind
modules/
├── _template/                       # definition/, service/ (LlmFlows.Modules.Template), tests/, README.md
└── support/                         # definition/, service/ (LlmFlows.Modules.Support), tests/, dummy-apps/, docs/, README.md
dashboard/                           # React console: api/, gen/, hooks/, components/, pages/, utils/, types/, __tests__/
docs/                                # platform documentation (engine, not domain)
```

## Module folder layout

Every module follows the same layout; nothing module-specific lives outside it:

```
modules/<id>/
├── definition/   # module.yaml, workflows/, prompts/, schemas/, samples/
├── service/      # LlmFlows.Modules.<Name>.csproj: <Name>Host.Create, Program.cs, Tools/, endpoints, <id>.http
├── tests/        # LlmFlows.Modules.<Name>.Tests.csproj
├── docs/, README.md
└── assets        # e.g. support's dummy-apps/
```

New modules start from `modules/_template` (see its README and `docs/module-authoring.md`). Support domain details: `modules/support/README.md`.

## Dependency rules (LlmFlows.Architecture.Tests)

- `Contracts` has no project references; `Definitions` references only `Contracts`.
- `Runtime` → `Definitions`; `Persistence` → `Runtime`; `Engine.Service` → `Runtime`, `Persistence`, `ServiceDefaults`; `Gateway` → `ServiceDefaults`; `Module.Sdk` → `Contracts`, `Definitions`; `Testing` → `Engine.Service`, `Gateway`, `Module.Sdk`.
- Engine projects and the gateway never reference `modules/` or `Fixtures/` and contain no `LlmFlows.Modules.*` types.
- Module services reference only `Module.Sdk`, `Contracts`, `Definitions`, `ServiceDefaults`.
- `Definitions` and `Runtime` do not depend on ASP.NET Core.
- Engine sources and the proto contain no support-domain terms.

## Key patterns

- **Hosts as factories**: every service exposes `XxxHost.Create(WebApplicationOptions, Action<WebApplicationBuilder>?)`; `Program.cs` calls it and tests use it in-process.
- **Pull model**: the engine polls module packages with ETag; new versions load atomically, in-flight runs keep their definition.
- **Per-run graphs**: `WorkflowCompiler` builds one Agent Framework workflow per run with one executor per step; `StepMessage(NextStepId)` is the only message.
- **Result types** for expected failures; `StepFailedException(stepId, reason)` ends a run as `Failed`.
- **Configuration-driven**: modules (engine `LlmFlows:Modules`, gateway `Gateway:Modules`, AppHost `AppHost:Modules`), never hard-coded.
- **Async-first** with `CancellationToken` for all I/O.

## Testing conventions

- xUnit v3 on Microsoft.Testing.Platform; FsCheck for properties; NSubstitute where mocks are needed.
- Platform and module tests run services in-process with `PlatformHarness` in Demo mode (no ports, no Aspire).
- Module tests validate the definition with `ModuleDefinitionAssert.IsValid` and run workflows end-to-end.
- Generated artifacts (e.g. `modules/support/service/support.http`) are checked by tests and regenerated with `UPDATE_SNAPSHOTS=1`.
- Dashboard: Vitest (jsdom) + fast-check in `dashboard/src/__tests__/`.
