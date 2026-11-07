# Implementation Plan: LLM Flow Modules (LlmFlows platform)

## Overview

The work is split into seven phases. At the end of each phase the solution builds and the tests pass. The old `AiSupportWorkflow.*` projects stay in place until the support module reaches parity (phase 4), then they are removed. Each change is a targeted commit on `feature/llm-flows-platform`.

Status: the first implementation round is closed. Everything is done except the parts of 7.4 marked as moved; they continue in [`llm-flow-modules-followup`](../llm-flow-modules-followup/tasks.md).

## Tasks

- [x] 1. Repository structure, shared build files, ServiceDefaults, gateway, AppHost
  - [x] 1.1 Move `Directory.Build.props`, `Directory.Packages.props`, `nuget.config` and `.editorconfig` from `backend/` to the repo root. Create `LlmFlows.slnx` with the existing projects. Update CI to the new solution.
    - Later moved back (0dc8fec): solution and build files live in `backend/`; see Implementation notes.
    - _Requirements: 2.1_
  - [x] 1.2 Add the new package versions to `Directory.Packages.props`: Aspire 13.6.4, ServiceDiscovery 10.11.0, Yarp 2.3.0, YamlDotNet 18.1.0, Scriban 7.5.3, JsonSchema.Net 9.6.0, OpenTelemetry 1.20.x, NetArchTest 1.3.2.
    - _Requirements: 16.2_
  - [x] 1.3 Create `LlmFlows.ServiceDefaults`: health, OpenTelemetry, service discovery, resilience.
    - _Requirements: 16.2_
  - [x] 1.4 Create `LlmFlows.Gateway`:
    - config-driven YARP routes (engine, modules, extra routes, gRPC-Web)
    - correlation id and 503 problem details
    - `GatewayHost.Create`
    - `LlmFlows.Gateway.Tests`
    - _Requirements: 3.1–3.6_
  - [x] 1.5 Create `LlmFlows.AppHost`: engine/modules/gateway/dashboard wiring, `http` profile, module list from config. Make `vite.config.ts` read `GATEWAY_URL`.
    - _Requirements: 16.1, 16.3_

- [x] 2. Engine definition libraries
  - [x] 2.1 `LlmFlows.Contracts`: module package, tool invocation, intake, engine API DTOs, `RunStatus`, `RunEventKind`.
    - _Requirements: 1.4, 10.1_
  - [x] 2.2 JSON Schemas `backend/schemas/{module,workflow,samples}.schema.json`, embedded in `LlmFlows.Definitions`.
    - _Requirements: 5.4_
  - [x] 2.3 YAML → `JsonNode` with line map; definition model records; `ModuleLoader.Load/LoadFromDirectory`.
    - _Requirements: 5.1, 5.2, 5.5_
  - [x] 2.4 Scriban expressions and templates:
    - typed full-expression results
    - sandbox settings
    - `JsonNode` ⇄ Scriban conversion
    - static `steps.*` reference check
    - _Requirements: 7.1–7.3_
  - [x] 2.5 Semantic validation (all rules of design §3) with module/file/step/line errors and warnings.
    - _Requirements: 8.1, 8.2_
  - [x] 2.6 Tests for the loader, schemas, expressions and validator, including FsCheck round-trip properties.
    - _Requirements: 5, 7, 8_

- [x] 3. Engine runtime, persistence and service
  - [x] 3.1 LLM: chat client setup, generic `DemoChatClient`, telemetry middleware and store with module/run/step attribution. Port the existing tests.
    - _Requirements: 12.1, 12.4_
  - [x] 3.2 Step handlers: llm (structured output, schema validation, demo), rule, map, human-input, tool, http, terminal.
    - _Requirements: 6.1–6.8, 12.2, 12.3, 13_
  - [x] 3.3 `WorkflowCompiler` (per-run executors, `StepMessage` edges, outputs) and `RunOrchestrator` (outcomes, abort, cancellation, max steps), `RunRegistry`, `HumanInputRegistry`/`HumanInputService`.
    - _Requirements: 9.1–9.3, 13.1, 13.2_
  - [x] 3.4 `LlmFlows.Persistence`: entities, `EngineDbContext`, run store with dual-write and update channel, inbox store.
    - _Requirements: 10.1–10.3, 11.1_
  - [x] 3.5 Module catalog: polling with ETag, states, conflicts, version reload, per-module timeouts and retries.
    - _Requirements: 4.2–4.5, 9.4_
  - [x] 3.6 Inbox processor ported (FIFO, non-blocking, sequential mode, unknown/unavailable workflow failure).
    - _Requirements: 11.2, 11.3_
  - [x] 3.7 `LlmFlows.Engine.Service`: `/api/engine/*` endpoints, gRPC `RunMonitor` with filters, `EngineHost.Create`, config, `.http`.
    - _Requirements: 10.4, 11.1, 13, 14_
  - [x] 3.8 Port the engine tests: orchestrator, executors, inbox, persistence, endpoints and properties.
    - _Requirements: 17.4_

- [x] 4. Module SDK and support module
  - [x] 4.1 `LlmFlows.Module.Sdk`: package endpoint with ETag, tools, startup validation, `IEngineClient`, `ModuleDefinitionAssert`.
    - _Requirements: 4.1, 8.3_
  - [x] 4.2 `LlmFlows.Testing`: in-process `PlatformHarness`.
    - _Requirements: 1.5_
  - [x] 4.3 Move `DummyApps/` to `modules/support/dummy-apps/application-a|b` (git mv).
    - _Requirements: 17.5_
  - [x] 4.4 Support definition:
    - `module.yaml` (config, agents, tool)
    - `support-email.yaml`
    - prompts, schemas
    - `samples/scenarios.yaml` (10 samples)
    - _Requirements: 17.1, 17.6, 17.7_
  - [x] 4.5 Support service: email endpoints (legacy contract), `support.locateComponent`, generated `support.http`.
    - _Requirements: 3.3, 17.2, 17.7_
  - [x] 4.6 Support tests: port the routing, agent, classification, resolution and email tests, plus end-to-end parity of all samples (approve/reject/abort).
    - _Requirements: 17.3, 17.4, 17.6_
  - [x] 4.7 Wire the support module and engine into the AppHost and gateway. Remove the old `AiSupportWorkflow.*` projects and the obsolete `DummyApps` path references.
    - _Requirements: 1.1, 1.3_

- [x] 5. Generic dashboard
  - [x] 5.1 Types, REST client, gRPC client regenerated from `run_monitor.proto`.
    - _Requirements: 15.1_
  - [x] 5.2 Module/workflow selection (persisted) with availability and validation errors.
    - _Requirements: 15.2_
  - [x] 5.3 Pipeline graph from the definition with dagre auto-layout; status-driven badges and counters.
    - _Requirements: 15.3, 15.4_
  - [x] 5.4 Schema-driven submission form with samples; generic approvals page.
    - _Requirements: 15.5_
  - [x] 5.5 Pages (Overview, Inbox, Runs, Approvals, Agents, Event Log) and LLM badge ported. Port all existing tests.
    - _Requirements: 15.6, 17.4_

- [x] 6. Multi-module readiness
  - [x] 6.1 `modules/_template` (definition, service, tests, README) and an AppHost toggle.
    - _Requirements: 2.3_
  - [x] 6.2 Fixture module covering every step kind and both outcomes.
    - _Requirements: 18.1_
  - [x] 6.3 Integration tests: concurrent isolation, module down, version reload, gateway end-to-end.
    - _Requirements: 4.4, 18.2, 18.3_
  - [x] 6.4 Architecture tests.
    - _Requirements: 1.3, 1.6_

- [ ] 7. Documentation and final verification
  - [x] 7.1 Root README (platform only) and `docs/`:
    - architecture, workflow reference, expressions, module authoring, gateway, engine API
    - demo mode, inbox, human input, dashboard, orchestration, debugging
    - remove obsolete docs
    - _Requirements: 19.1–19.4_
  - [x] 7.2 `modules/support/README.md` and `docs/` (pipeline, rules, agents, approval, config, demo, dummy apps, scenarios, endpoints, `.http`), with valid links in both directions.
    - _Requirements: 20.1–20.4_
  - [x] 7.3 Steering files (product, structure, tech) for the platform.
    - _Requirements: 19.5_
  - [ ] 7.4 Final verification (moved to `llm-flow-modules-followup`, except the backend run noted below):
    - full build and tests (backend + dashboard): backend done in round 1 (`dotnet build backend/LlmFlows.slnx` 0 warnings/0 errors, `dotnet test --solution backend/LlmFlows.slnx` 314 passed, commit 0dc8fec); the final backend + dashboard run on the closing commit is moved
    - AppHost run and Playwright E2E through the gateway: moved, Playwright reduced to one smoke scenario
    - secret scan: moved
    - review with the user before pushing: moved
    - _Requirements: 17.3_

## Implementation notes

Deviations from design.md and additions made during round 1 (design.md is not rewritten; some of these were already folded into it while implementing):

- The support workflow has an extra `tool` step, `locate_component` (`support.locateComponent`), between `assign_agent` and `resolve`; the resolution and code-generation prompts and demo responses use its `component` and `path`.
- `llm` prompts also get an `agent` variable (`id`, `name`, `group`, `role`, `persona`): the agent the call is attributed to, i.e. the step's `agent` or else the run's assigned agent. The support prompts use `agent.persona`, not `run.agent.persona` as in the design example.
- The orchestrator is behind an `IRunExecutor` interface (`ExecuteAsync(runId, hostToken)`), so inbox tests can fake it; module client and chat client are replaceable too.
- Host shutdown ends in-flight runs as `Cancelled` (reason `Workflow was cancelled.`); abort stays a single `Failed` "Aborted by user".
- Intake creates the inbox message and the run as `Queued` in one operation and returns `202 { messageId, runId }` with Location `/api/engine/runs/{runId}` (Req 11.1 only asked for the message id); `POST /api/support/emails` returns the same body.
- The out-of-scope branch (`classified_out_of_scope`) is a success terminal, so the run ends `Completed`.
- `LlmFlows.Gateway.Tests` is a separate test project (routes, forwarding, correlation id, 503).
- The AppHost accepts `--AppHost:LlmProvider=Demo` (or any provider), passed to the engine as `LlmFlows__Llm__Provider`.
- The template tool is `template.wordCount` (design example: `hello.wordCount`).
- The fixture module's `http` step calls the fixture service's own `/api/modules/fixture/echo` through `config.echoBaseUrl`.
- `PlatformHarness.RestartModuleAsync` was added to `LlmFlows.Testing` as a test seam for the version-reload integration test.
- `Grpc.Net.Client.Web` 2.85.0 was added (integration tests only) for the gRPC-Web-through-the-gateway test.
- `LlmFlows.slnx`, `Directory.Build.props`, `Directory.Packages.props` and `nuget.config` live in `backend/` (not the repo root as in task 1.1 and design.md); `modules/` has `Directory.Build.props`/`Directory.Packages.props` importing the backend ones and a mirrored `nuget.config`. `global.json`, `.editorconfig` and `.gitattributes` stay at the root. CI builds `backend/LlmFlows.slnx`.
