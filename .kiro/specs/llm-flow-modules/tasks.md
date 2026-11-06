# Implementation Plan: LLM Flow Modules (LlmFlows platform)

## Overview

The work is split into seven phases. At the end of each phase the solution builds and the tests pass. The old `AiSupportWorkflow.*` projects stay in place until the support module reaches parity (phase 4), then they are removed. Each change is a targeted commit on `feature/llm-flows-platform`.

## Tasks

- [ ] 1. Repository structure, shared build files, ServiceDefaults, gateway, AppHost
  - [ ] 1.1 Move `Directory.Build.props`, `Directory.Packages.props`, `nuget.config` and `.editorconfig` from `backend/` to the repo root. Create `LlmFlows.slnx` with the existing projects. Update CI to the new solution.
    - _Requirements: 2.1_
  - [ ] 1.2 Add the new package versions to `Directory.Packages.props`: Aspire 13.6.4, ServiceDiscovery 10.11.0, Yarp 2.3.0, YamlDotNet 18.1.0, Scriban 7.5.3, JsonSchema.Net 9.6.0, OpenTelemetry 1.20.x, NetArchTest 1.3.2.
    - _Requirements: 16.2_
  - [ ] 1.3 Create `LlmFlows.ServiceDefaults`: health, OpenTelemetry, service discovery, resilience.
    - _Requirements: 16.2_
  - [ ] 1.4 Create `LlmFlows.Gateway`:
    - config-driven YARP routes (engine, modules, extra routes, gRPC-Web)
    - correlation id and 503 problem details
    - `GatewayHost.Create`
    - `LlmFlows.Gateway.Tests`
    - _Requirements: 3.1–3.6_
  - [ ] 1.5 Create `LlmFlows.AppHost`: engine/modules/gateway/dashboard wiring, `http` profile, module list from config. Make `vite.config.ts` read `GATEWAY_URL`.
    - _Requirements: 16.1, 16.3_

- [ ] 2. Engine definition libraries
  - [ ] 2.1 `LlmFlows.Contracts`: module package, tool invocation, intake, engine API DTOs, `RunStatus`, `RunEventKind`.
    - _Requirements: 1.4, 10.1_
  - [ ] 2.2 JSON Schemas `backend/schemas/{module,workflow,samples}.schema.json`, embedded in `LlmFlows.Definitions`.
    - _Requirements: 5.4_
  - [ ] 2.3 YAML → `JsonNode` with line map; definition model records; `ModuleLoader.Load/LoadFromDirectory`.
    - _Requirements: 5.1, 5.2, 5.5_
  - [ ] 2.4 Scriban expressions and templates:
    - typed full-expression results
    - sandbox settings
    - `JsonNode` ⇄ Scriban conversion
    - static `steps.*` reference check
    - _Requirements: 7.1–7.3_
  - [ ] 2.5 Semantic validation (all rules of design §3) with module/file/step/line errors and warnings.
    - _Requirements: 8.1, 8.2_
  - [ ] 2.6 Tests for the loader, schemas, expressions and validator, including FsCheck round-trip properties.
    - _Requirements: 5, 7, 8_

- [ ] 3. Engine runtime, persistence and service
  - [ ] 3.1 LLM: chat client setup, generic `DemoChatClient`, telemetry middleware and store with module/run/step attribution. Port the existing tests.
    - _Requirements: 12.1, 12.4_
  - [ ] 3.2 Step handlers: llm (structured output, schema validation, demo), rule, map, human-input, tool, http, terminal.
    - _Requirements: 6.1–6.8, 12.2, 12.3, 13_
  - [ ] 3.3 `WorkflowCompiler` (per-run executors, `StepMessage` edges, outputs) and `RunOrchestrator` (outcomes, abort, cancellation, max steps), `RunRegistry`, `HumanInputRegistry`/`HumanInputService`.
    - _Requirements: 9.1–9.3, 13.1, 13.2_
  - [ ] 3.4 `LlmFlows.Persistence`: entities, `EngineDbContext`, run store with dual-write and update channel, inbox store.
    - _Requirements: 10.1–10.3, 11.1_
  - [ ] 3.5 Module catalog: polling with ETag, states, conflicts, version reload, per-module timeouts and retries.
    - _Requirements: 4.2–4.5, 9.4_
  - [ ] 3.6 Inbox processor ported (FIFO, non-blocking, sequential mode, unknown/unavailable workflow failure).
    - _Requirements: 11.2, 11.3_
  - [ ] 3.7 `LlmFlows.Engine.Service`: `/api/engine/*` endpoints, gRPC `RunMonitor` with filters, `EngineHost.Create`, config, `.http`.
    - _Requirements: 10.4, 11.1, 13, 14_
  - [ ] 3.8 Port the engine tests: orchestrator, executors, inbox, persistence, endpoints and properties.
    - _Requirements: 17.4_

- [ ] 4. Module SDK and support module
  - [ ] 4.1 `LlmFlows.Module.Sdk`: package endpoint with ETag, tools, startup validation, `IEngineClient`, `ModuleDefinitionAssert`.
    - _Requirements: 4.1, 8.3_
  - [ ] 4.2 `LlmFlows.Testing`: in-process `PlatformHarness`.
    - _Requirements: 1.5_
  - [ ] 4.3 Move `DummyApps/` to `modules/support/dummy-apps/application-a|b` (git mv).
    - _Requirements: 17.5_
  - [ ] 4.4 Support definition:
    - `module.yaml` (config, agents, tool)
    - `support-email.yaml`
    - prompts, schemas
    - `samples/scenarios.yaml` (10 samples)
    - _Requirements: 17.1, 17.6, 17.7_
  - [ ] 4.5 Support service: email endpoints (legacy contract), `support.locateComponent`, generated `support.http`.
    - _Requirements: 3.3, 17.2, 17.7_
  - [ ] 4.6 Support tests: port the routing, agent, classification, resolution and email tests, plus end-to-end parity of all samples (approve/reject/abort).
    - _Requirements: 17.3, 17.4, 17.6_
  - [ ] 4.7 Wire the support module and engine into the AppHost and gateway. Remove the old `AiSupportWorkflow.*` projects and the obsolete `DummyApps` path references.
    - _Requirements: 1.1, 1.3_

- [ ] 5. Generic dashboard
  - [ ] 5.1 Types, REST client, gRPC client regenerated from `run_monitor.proto`.
    - _Requirements: 15.1_
  - [ ] 5.2 Module/workflow selection (persisted) with availability and validation errors.
    - _Requirements: 15.2_
  - [ ] 5.3 Pipeline graph from the definition with dagre auto-layout; status-driven badges and counters.
    - _Requirements: 15.3, 15.4_
  - [ ] 5.4 Schema-driven submission form with samples; generic approvals page.
    - _Requirements: 15.5_
  - [ ] 5.5 Pages (Overview, Inbox, Runs, Approvals, Agents, Event Log) and LLM badge ported. Port all existing tests.
    - _Requirements: 15.6, 17.4_

- [ ] 6. Multi-module readiness
  - [ ] 6.1 `modules/_template` (definition, service, tests, README) and an AppHost toggle.
    - _Requirements: 2.3_
  - [ ] 6.2 Fixture module covering every step kind and both outcomes.
    - _Requirements: 18.1_
  - [ ] 6.3 Integration tests: concurrent isolation, module down, version reload, gateway end-to-end.
    - _Requirements: 4.4, 18.2, 18.3_
  - [ ] 6.4 Architecture tests.
    - _Requirements: 1.3, 1.6_

- [ ] 7. Documentation and final verification
  - [ ] 7.1 Root README (platform only) and `docs/`:
    - architecture, workflow reference, expressions, module authoring, gateway, engine API
    - demo mode, inbox, human input, dashboard, orchestration, debugging
    - remove obsolete docs
    - _Requirements: 19.1–19.4_
  - [ ] 7.2 `modules/support/README.md` and `docs/` (pipeline, rules, agents, approval, config, demo, dummy apps, scenarios, endpoints, `.http`), with valid links in both directions.
    - _Requirements: 20.1–20.4_
  - [ ] 7.3 Steering files (product, structure, tech) for the platform.
    - _Requirements: 19.5_
  - [ ] 7.4 Final verification:
    - full build and tests (backend + dashboard)
    - AppHost run and Playwright E2E through the gateway
    - secret scan
    - review with the user before pushing
    - _Requirements: 17.3_
