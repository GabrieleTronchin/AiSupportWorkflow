# Requirements Document

## Introduction

Today the backend is a single-purpose application: the support-email pipeline (classification → team routing → agent assignment → resolution → human approval → code generation) is hard-wired in C# through the whole stack — the `WorkflowStage` enum, typed executors, `SupportWorkflowFactory`, prompts inside executors, regexes in `TeamRouter`, persistence, REST API, gRPC stream and dashboard.

This feature turns the project into a **generic, distributed platform for LLM flows**, set up from the start in its final shape:

- An **API Gateway** is the single entry point for the dashboard and any client.
- A **domain-agnostic Engine Service** executes workflows **defined in YAML** (steps, transitions, prompts, schemas, routing rules, human review, demo responses) on Microsoft Agent Framework Workflows. It owns runs, persistence, the inbox, human input, LLM access, Demo mode and telemetry.
- **Module Services**, one per business domain, own their workflow definitions and expose domain-specific endpoints and custom operations ("tools") that workflows call when logic does not belong in YAML.
- The **dashboard** is a generic monitoring console for every module, served through the gateway.
- A **local orchestrator** starts the whole system with one command.

The current support pipeline becomes the first Module Service with its workflow ported to YAML and identical behaviour; it is the reference implementation and the regression suite of the platform. The repository is structured so that a second module (e.g. food tracking) is added by creating a new module from a template, without changes to the Gateway, the Engine or the dashboard. The second business module itself is not part of this feature.

The platform keeps the current technical choices where they still apply: Agent Framework Workflows, `IChatClient` (Demo and OpenAI-compatible providers), EF Core InMemory persistence in the Engine, the transactional inbox, gRPC streaming and the React dashboard.

### Out of Scope

- Implementing a second business module: only the module template and a test fixture module are delivered.
- Multimodal input (images), end-user authentication and authorization, multi-tenancy, persistent databases.
- Service-to-service authentication (services are assumed to run on a private network behind the gateway); the design must allow adding it later.
- Editing workflows from the dashboard; changing workflow definitions without restarting the owning Module Service.
- Production deployment (container images, Kubernetes manifests, CI/CD for deployments) and NuGet packaging.

## Glossary

- **Platform**: The whole system: Gateway, Engine Service, Module Services, dashboard and orchestrator.
- **Gateway**: The reverse proxy exposing a single public entry point and routing requests to the Engine Service and Module Services.
- **Engine_Service**: The domain-agnostic service that loads workflow definitions from modules and executes them.
- **Module_Service**: A service owning one business domain: its Module_Manifest, Workflow_Definitions, prompts, sample inputs, demo data, domain endpoints and Tools.
- **Module_SDK**: Library used by Module_Services to publish their manifest and definitions, expose Tools and register with the Engine_Service.
- **Orchestrator**: The local development host that starts and wires all services and the dashboard with one command.
- **Module_Manifest**: The `module.yaml` declaring a module's id, name, agents, configuration, workflows, Tools and sample inputs.
- **Workflow_Definition**: A YAML document describing one workflow: id, version, input schema, steps, transitions and terminal outcomes.
- **Step**: A node of a workflow with an id unique within the workflow and a Step_Kind.
- **Step_Kind**: The built-in behaviour of a step: `llm`, `rule`, `map`, `human-input`, `tool`, `http`, `terminal`.
- **Tool**: A named operation exposed by a Module_Service over HTTP and invoked by a `tool` step (e.g. `support.lookupComponent`).
- **Run_Context**: The JSON document carried through a run: input, outputs of completed steps, module configuration and run metadata.
- **Expression** / **Template**: Side-effect-free expressions and placeholder texts evaluated against the Run_Context.
- **Run**: One execution of a workflow for one input, with a current step and a Run_Status.
- **Run_Status**: `Queued`, `Running`, `AwaitingInput`, `Completed`, `Failed`, `Cancelled`.
- **Agent**: An LLM actor declared in a Module_Manifest (e.g. `TeamA_BackendDeveloper`), used for status and telemetry attribution.
- **Sample_Input**: An example input declared by a module, shown as a template in the dashboard.
- **Support_Module**: The current support-email pipeline as a Module_Service.
- **Fixture_Module**: A minimal Module_Service used only by tests to verify multi-module behaviour.

## Requirements

### Requirement 1: Platform Topology

**User Story:** As the maintainer, I want the platform split into gateway, engine and module services from the start, so that new domains are added as new services without reworking the core.

#### Acceptance Criteria

1. THE Platform SHALL consist of: the Gateway, the Engine_Service, one Module_Service per module, the dashboard and the Orchestrator.
2. THE Gateway SHALL be the only component the dashboard and external clients call.
3. THE Engine_Service SHALL contain no module-specific types, prompts, categories, routing rules, Tools or endpoints (e.g. `IncomingEmail`, `IssueCategory`, `TeamRouter`, `/emails`).
4. Module_Services SHALL communicate with the Engine_Service only through its public HTTP contracts, and the Engine_Service SHALL call Module_Services only through the Module_SDK contracts (manifest, definitions, Tools, health).
5. EACH service SHALL be runnable and testable on its own.
6. THE Solution SHALL include automated architecture tests that fail WHEN the Engine references a module project, or a Module_Service references Engine implementation projects instead of the Module_SDK and contract projects.

### Requirement 2: Repository Structure

**User Story:** As a developer, I want a predictable folder structure, so that I know where every part lives and can create a module by copying a template.

#### Acceptance Criteria

1. THE Repository SHALL separate: Engine libraries, the Engine_Service host, the Gateway, the Module_SDK and contracts, the Orchestrator and shared service defaults, one folder per module under `modules/`, the module template (`modules/_template`), platform test projects, the dashboard and the platform documentation (`docs/`).
2. EACH module folder SHALL follow the same layout: `definition/` (`module.yaml`, `workflows/`, `prompts/`, `samples/`, `demo/`), `service/` (the Module_Service project), `tests/`, `docs/`, `README.md` and, when needed, module-specific asset folders (e.g. the Support_Module's `dummy-apps/`).
3. THE `modules/_template` folder SHALL contain a minimal, valid, documented module (definition and service) that builds, registers and runs one workflow end-to-end in Demo mode after being copied and renamed.
4. Nothing specific to a module SHALL live outside its module folder: domain fixtures, sample requests (`.http` files), scenario descriptions, prompts and module documentation belong to the module.

### Requirement 3: Gateway

**User Story:** As a client developer, I want a single, stable entry point, so that clients do not depend on where services run.

#### Acceptance Criteria

1. THE Gateway SHALL route `/api/engine/**` to the Engine_Service and `/api/modules/{moduleId}/**` to the Module_Service registered for `moduleId`.
2. THE Gateway SHALL route the gRPC-Web workflow stream to the Engine_Service without buffering.
3. THE Gateway SHALL preserve `POST /api/support/emails` by forwarding it to the Support_Module.
4. THE Gateway SHALL propagate trace context and a correlation id to downstream services.
5. IF a target service is unavailable, THEN THE Gateway SHALL return `503 Service Unavailable` with a problem-details body identifying the service.
6. THE Gateway's routes SHALL be configuration-driven, so that adding a module requires configuration only.

### Requirement 4: Module Registration and Discovery

**User Story:** As a module author, I want my Module_Service to make its workflows available to the platform automatically, so that adding a module needs no change to the Engine.

#### Acceptance Criteria

1. EACH Module_Service SHALL publish its Module_Manifest, Workflow_Definitions, prompts, schemas, demo data and Sample_Inputs through the Module_SDK endpoints.
2. THE Engine_Service SHALL load the definitions of every configured module at startup and retry modules that are not yet available, without blocking its own startup.
3. WHILE a module's definitions are not loaded or its Module_Service is unhealthy, THE Engine_Service SHALL report the module as unavailable and reject new inputs for its workflows with a descriptive error.
4. THE Engine_Service SHALL reload a module's definitions WHEN the Module_Service restarts with a new definition version; runs already in progress SHALL complete with the version they started with.
5. IF two modules declare the same id, or two workflows declare the same id, THEN THE Engine_Service SHALL refuse the conflicting module and report a descriptive error.

### Requirement 5: Workflow Definition Format

**User Story:** As a module author, I want to describe a workflow entirely in YAML, so that I can create or change flows without engine code.

#### Acceptance Criteria

1. A Workflow_Definition SHALL declare: id, version, display name, description, input schema, start step, steps, transitions and terminal outcomes.
2. EACH step SHALL declare an id, a Step_Kind, a display name and kind-specific settings; transitions SHALL declare a target step and an optional condition Expression.
3. WHEN a step has several transitions, THE Engine SHALL follow the first whose condition is true, in declaration order; a transition without a condition SHALL act as the default.
4. THE Platform SHALL publish JSON Schemas of the Workflow_Definition and Module_Manifest formats, so that editors can validate and autocomplete YAML files.
5. Prompts and JSON schemas SHALL be usable inline or by reference to a file in the module definition folder.

### Requirement 6: Built-in Step Kinds

**User Story:** As a module author, I want a catalogue of reusable step kinds, so that most flows need no custom code.

#### Acceptance Criteria

1. THE `llm` step SHALL send a system prompt and a user prompt Template to the configured `IChatClient`, request a structured response matching the declared output schema, validate it against that schema and store it as the step output; it SHALL support temperature and the Agent the call is attributed to.
2. THE `rule` step SHALL evaluate an ordered list of rules (regular expression or Expression) against a Template value and output the matching rule's value, with declared behaviour when no rule or several rules match (fail with a message, or output a fallback).
3. THE `map` step SHALL compute named outputs from Expressions and lookup tables.
4. THE `human-input` step SHALL pause the run in `AwaitingInput`, publish a review payload (title and ordered labelled fields from Templates) and continue along the transition declared for the decision (`approved`, `rejected`).
5. THE `tool` step SHALL invoke a Tool of the owning Module_Service with an input built from Templates, validate the result against the Tool's declared output schema and store it as the step output.
6. THE `http` step SHALL call an external HTTP API (method, URL Template, headers from configuration, body Template, timeout), map the JSON response to the step output and fail on non-success status codes or timeouts.
7. THE `terminal` step SHALL end the run as success or failure with a message Template and an optional final output.
8. WHEN a step fails (LLM error, schema validation error, rule failure, Tool or HTTP error, timeout), THE Engine SHALL end the run as `Failed` with the step id and a descriptive reason.

### Requirement 7: Expressions and Templates

**User Story:** As a module author, I want to reference the input and previous results in prompts and conditions, so that steps can build on each other.

#### Acceptance Criteria

1. Expressions and Templates SHALL access `input.*`, `steps.{stepId}.*`, `config.*` and run metadata (`run.id`, `run.workflowId`).
2. Expressions SHALL support comparison, boolean logic, string functions (contains, lower, regex match) and null checks, and SHALL NOT allow file, network or process access.
3. WHEN a Workflow_Definition is loaded, THE Engine SHALL parse every Expression and Template and reject the definition IF any is invalid.

### Requirement 8: Definition Validation

**User Story:** As a module author, I want mistakes in YAML reported before any run starts.

#### Acceptance Criteria

1. WHEN a module's definitions are loaded, THE Engine SHALL validate them and reject the module with a descriptive error IF: the YAML does not match the format schema; step ids are not unique; a transition targets an unknown step; a step is unreachable; no terminal step is reachable; a referenced prompt or schema is missing or invalid; a referenced Agent or Tool is not declared in the Module_Manifest; or an Expression or Template is invalid.
2. Validation errors SHALL identify module, file, step and, where available, line number, and SHALL be visible through the Engine API and the dashboard.
3. THE Module_SDK SHALL provide offline validation (usable in module tests and at Module_Service startup) using the same rules as the Engine.

### Requirement 9: Execution Runtime

**User Story:** As a developer, I want YAML workflows to run on the existing Agent Framework runtime, so that execution, concurrency and cancellation keep working as today.

#### Acceptance Criteria

1. THE Engine SHALL compile each Workflow_Definition into an Agent Framework workflow graph composed of generic step executors.
2. EACH run SHALL have an isolated Run_Context; concurrent runs of the same or different workflows SHALL not share state.
3. THE Engine SHALL support aborting a running workflow, cancelling in-flight LLM, Tool and HTTP calls and pending human input, and recording a single `Failed` transition with reason "Aborted by user", as today.
4. Calls to Module_Services and external APIs SHALL use timeouts and retries for transient failures, configurable per module.

### Requirement 10: Generic Run Model and Persistence

**User Story:** As a developer, I want runs, transitions and events stored generically, so that every module's workflow is tracked and audited the same way.

#### Acceptance Criteria

1. THE Engine SHALL replace the `WorkflowStage` enum with a step id (string) plus a Run_Status.
2. EACH Run SHALL record module id, workflow id and version, title, input, current step, Run_Status, last update, detail, step outputs and final output.
3. WHEN a run moves to a step or changes status, THE Engine SHALL update the run and append a transition event in the same operation (dual-write preserved).
4. THE gRPC stream SHALL publish, for each transition, run id, module id, workflow id, step id, Run_Status, timestamp and detail, and SHALL support filtering by run id and by workflow id.

### Requirement 11: Generic Intake

**User Story:** As a module author, I want every workflow to use the same asynchronous intake, so that inputs are persisted and processed reliably.

#### Acceptance Criteria

1. THE Engine SHALL expose a generic endpoint to submit an input to a workflow; it SHALL validate the input against the workflow's input schema and return `202 Accepted` with the message id, or `400 Bad Request` with the validation errors.
2. THE InboxProcessor SHALL preserve the current semantics: FIFO start order, concurrent runs that never block polling, no infinite retries, error recorded on the message, sequential mode starting at most one run at a time.
3. IF a message targets an unknown, disabled or unavailable workflow, THEN THE InboxProcessor SHALL mark the message as failed with a descriptive error.

### Requirement 12: LLM, Demo Mode and Telemetry

**User Story:** As a module author, I want LLM access, Demo mode and telemetry provided centrally, so that modules work offline and are observable.

#### Acceptance Criteria

1. THE Engine_Service SHALL own the `IChatClient` configuration (Demo default and OpenAI-compatible providers), the telemetry middleware and the runtime configuration endpoint; Module_Services SHALL not call LLMs directly for workflow steps.
2. EACH `llm` step SHALL declare its Demo response in YAML: a static output, or ordered rules with a default, with Templates for dynamic values; WHERE this is not enough, it SHALL reference a Tool that produces the Demo response.
3. IF an `llm` step has no Demo response while Demo mode is active, THEN THE Engine SHALL fail the step with a descriptive error.
4. THE Engine SHALL attribute LLM telemetry to the Agent declared on each `llm` step.

### Requirement 13: Human Input

**User Story:** As an operator, I want to approve, reject or abort paused runs of any workflow from the same page.

#### Acceptance Criteria

1. THE Engine SHALL expose generic endpoints to list pending human inputs (with their review payload) and to approve, reject (with optional reason) or abort a run.
2. THE endpoints SHALL keep the current status codes: `204` on success (`202` when an abort cancels a running workflow), `404` for unknown runs, `409` for runs not awaiting input or already completed.

### Requirement 14: Engine API

**User Story:** As a dashboard developer, I want module-agnostic endpoints, so that the dashboard works for every module.

#### Acceptance Criteria

1. THE Engine_Service SHALL expose endpoints to list modules (with availability and validation errors), workflows (with their graph and input schema) and Sample_Inputs; list and get runs filterable by module, workflow and status; list transition events, agents with status, inbox messages, telemetry and runtime configuration.
2. THE API SHALL never expose secrets (API keys, endpoint URLs, HTTP step headers).

### Requirement 15: Workflow-Agnostic Dashboard

**User Story:** As an operator, I want the dashboard to adapt to the selected workflow, so that I can monitor any module from the same console.

#### Acceptance Criteria

1. THE Dashboard SHALL call only the Gateway.
2. THE Dashboard SHALL let the user select module and workflow, remember the last selection, and show module availability and validation errors.
3. THE Dashboard SHALL render the pipeline graph from the workflow definition with automatic layout and readable labels at the default zoom.
4. THE Dashboard SHALL derive stage filters, badges, terminal detection and counters from the workflow definition and Run_Status, with no hard-coded step names.
5. THE Dashboard SHALL render the approval page from the generic review payload and build the submission form from the workflow's input schema and Sample_Inputs.
6. THE Dashboard SHALL keep the existing pages (Overview, Inbox, Runs, Approvals, Agents, Event Log) and the LLM mode badge.

### Requirement 16: Local Orchestration and Observability

**User Story:** As a developer, I want to start and observe the whole platform with one command, so that the distributed setup does not slow down development.

#### Acceptance Criteria

1. THE Orchestrator SHALL start the Gateway, the Engine_Service, every enabled Module_Service and the dashboard with one command, wiring service addresses automatically.
2. EACH service SHALL expose health endpoints and emit logs, traces and metrics with a shared trace context across Gateway, Engine and Module_Services.
3. THE Orchestrator SHALL show the status, logs and traces of all services in one place.

### Requirement 17: Support Module Parity

**User Story:** As the maintainer, I want the support pipeline to behave exactly as before, so that it remains a trustworthy reference for the platform.

#### Acceptance Criteria

1. THE Support_Module SHALL define the current pipeline in YAML: classification, out-of-scope branch, team routing (including ambiguous and failed routing), agent assignment, resolution, human approval, rejection, code generation, abort and terminal outcomes.
2. THE Support_Module SHALL provide `POST /api/support/emails` (through the Gateway) with its current request and response contract, forwarding to the Engine's generic intake.
3. WHEN each dashboard template (A1–A3, B1–B3, Out-of-Scope, Ambiguous Routing, Failed Routing, Empty Input) is submitted in Demo mode through the Gateway, THE Platform SHALL reach the same final step and outcome as before this feature.
4. THE existing backend and frontend tests SHALL be ported to the new structure and pass, with behavioural assertions preserved.
5. THE `DummyApps/` folder SHALL move into the Support_Module (`modules/support/dummy-apps/application-a`, `modules/support/dummy-apps/application-b`), keeping the source files and the bug scenario descriptions.
6. Prompts, demo responses and tests SHALL reference dummy app files through module configuration (e.g. `config.dummyAppsRoot`) instead of hard-coded `DummyApps/...` paths, and the property tests on generated file paths SHALL be updated accordingly.
7. THE bug scenarios (A1–A3, B1–B3) and edge cases SHALL have a single source in the Support_Module, from which the Sample_Inputs, the dashboard templates and the module's `.http` requests are derived, so that they cannot drift apart.

### Requirement 18: Multi-Module Readiness

**User Story:** As the maintainer, I want proof that several modules can coexist, so that adding the second business module is low-risk.

#### Acceptance Criteria

1. THE test suite SHALL include a Fixture_Module service, running alongside the Support_Module, whose workflow exercises `llm`, `rule` or `map`, `human-input`, `tool` and `http` steps and both terminal outcomes.
2. WHEN runs of both modules execute concurrently, THE Platform SHALL keep them isolated: state, events, human inputs, telemetry and API listings of one module are not affected by the other.
3. WHEN the Fixture_Module service is stopped, THE Platform SHALL report it unavailable, reject its new inputs, fail its in-flight Tool steps with a descriptive reason, and keep the Support_Module working.

### Requirement 19: Platform Documentation

**User Story:** As a developer reusing the project, I want the main documentation to describe the generic platform, so that I can understand it and build new modules without reading about the support domain.

#### Acceptance Criteria

1. THE root README SHALL describe the platform: purpose, topology, folder structure, how to start everything, how modules work and the list of available modules with links to their own documentation. It SHALL NOT describe the support pipeline beyond a one-line summary and a link.
2. THE platform documentation (`docs/`) SHALL include: architecture and service contracts, the Workflow_Definition and Module_Manifest reference (every Step_Kind with an example), the Expression reference, the module authoring guide based on `modules/_template`, the Gateway routes, the Engine API reference, Demo mode, the inbox, human input, the dashboard, local orchestration and observability, and debugging.
3. Examples in the platform documentation SHALL use generic or template-module workflows, not the support pipeline.
4. THE platform documentation SHALL no longer describe removed architecture (Akka.NET actors, hard-coded stages, `SupportWorkflowFactory`); obsolete documents SHALL be removed.
5. THE steering files (`.kiro/steering/`) SHALL describe the platform (product, structure, tech) and point to the Support_Module documentation for domain details.

### Requirement 20: Support Module Documentation

**User Story:** As a developer working on the support pipeline, I want its documentation next to its definitions, service and dummy apps, so that everything about the module lives in one place.

#### Acceptance Criteria

1. THE Support_Module SHALL include `modules/support/README.md` describing: the pipeline and its diagram, the steps and routing rules, the agents and teams, the human approval and rejection behaviour, the configuration, the Demo responses, and how to run and test the module.
2. THE Support_Module documentation SHALL describe the dummy apps and the bug scenarios (A1–A3, B1–B3) and edge cases, with links to the scenario files in `modules/support/dummy-apps/`.
3. THE Support_Module documentation SHALL describe its endpoints (`POST /api/support/emails`) and include its `.http` requests.
4. Links from the root README and platform documentation to the Support_Module documentation, and back, SHALL be valid.

## Decisions

1. **Engine name**: `Flowgent` (projects `Flowgent.*`).
2. **Gateway**: YARP, configuration-driven, with gRPC-Web passthrough.
3. **Orchestrator**: .NET Aspire AppHost with shared service defaults (health, OpenTelemetry, resilience, service discovery).
4. **Definition ownership**: Module_Services own and publish their definitions; the Engine_Service pulls them (startup, retry, reload on new version).
5. **Expression and template language**: an existing sandboxed library, selected in the design (candidates: Scriban, JsonLogic).
6. **API compatibility**: the current read routes (`/api/support/issues`, `/events`, `/agents`, …) are replaced by `/api/engine/*`; only `POST /api/support/emails` is preserved.
