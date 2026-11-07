# Requirements Document

## Introduction

The first implementation round of [`llm-flow-modules`](../llm-flow-modules/tasks.md) is closed: phases 1–6 and tasks 7.1–7.3 are done. The phase 6–7 commits (06ed44b..2fc3157) report no unfinished item. What is left is task 7.4, the final verification. This spec covers it and finishes the round so the branch `feature/llm-flows-platform` can be reviewed and pushed.

Browser testing is kept to the minimum: one smoke scenario through the gateway. Everything else (all ten samples, rejection, abort, multi-module isolation, module down, version reload, gRPC-Web through the gateway) is already covered by the in-process `PlatformHarness` tests and stays there.

Requirement numbers in brackets refer to [`llm-flow-modules/requirements.md`](../llm-flow-modules/requirements.md).

### Out of Scope

- New platform or module features, and the second business module.
- Browser tests beyond the single smoke scenario. Browser tests in CI.
- Deployment pipelines (out of scope in the original spec too).
- Pushing or opening the pull request without the user's approval.

## Glossary

- **Closing_Commit**: The branch HEAD on which the final verification runs.
- **Smoke_Scenario**: The single browser scenario of Requirement 3.
- **Harness_Tests**: The in-process tests on `PlatformHarness` (support, template, integration, engine and gateway test projects).

## Requirements

### Requirement 1: Final Automated Verification

Completes [17.4] (task 7.4, "full build and tests").

#### Acceptance Criteria

1. WHEN the final verification runs on the Closing_Commit, THE backend SHALL build with `dotnet build backend/Flowgent.slnx` with 0 errors and 0 warnings, and `dotnet test --solution backend/Flowgent.slnx` SHALL pass with no failed tests.
2. WHEN the final verification runs on the Closing_Commit, THE dashboard SHALL pass `npm run typecheck`, `npm run test -- --run` and `npm run build`.
3. IF `dashboard/package-lock.json` is modified by the install, THEN it SHALL be restored when the diff is only install noise (no dependency or version change compared to `package.json`), and reported otherwise.
4. THE results (commands, counts, Closing_Commit sha) SHALL be recorded in the commit or summary that closes the verification.

### Requirement 2: Local Platform Run

Completes [16.1] and [16.3] (task 7.4, "AppHost run").

#### Acceptance Criteria

1. WHEN the AppHost is started with `--AppHost:LlmProvider=Demo`, THE Orchestrator SHALL start the gateway, the engine, the support module and the dashboard, and the Aspire dashboard (:15080) SHALL show them running and healthy.
2. WHILE the platform is running, `GET http://localhost:5080/api/engine/modules` SHALL report the `support` module available with no validation errors, and `GET /api/engine/config` SHALL report Demo mode.

### Requirement 3: Browser Smoke Test Through the Gateway

Completes [15.1], [15.5] and [17.3] for the browser path (task 7.4, "Playwright E2E through the gateway", reduced to one scenario).

#### Acceptance Criteria

1. THE Smoke_Scenario SHALL be the only browser scenario: in the dashboard (:5173) select module `support` and workflow `support-email`, submit sample `scenario-a1` from the generated form, approve the run on the Approvals page, and see the run end `Completed` on step `code_change_generated`.
2. WHILE the Smoke_Scenario runs, every dashboard request (REST and gRPC-Web stream) SHALL go to the dashboard origin and be proxied to the gateway; none SHALL target the engine (:5081) or a module service (:5082) directly.
3. WHILE the Smoke_Scenario runs, the browser console SHALL show no errors.
4. THE remaining parity cases of [17.3] (other samples, rejection, abort, edge cases) SHALL stay covered by the Harness_Tests and SHALL NOT be added as browser scenarios.

### Requirement 4: Secret Scan Before Push

Completes task 7.4, "secret scan". Supports [14.2] and the product constraint that secrets come only from configuration.

#### Acceptance Criteria

1. BEFORE any push, THE scan SHALL cover every commit in `origin/master..HEAD` and the tracked files of the Closing_Commit, looking for API keys, tokens, passwords, connection strings and private keys.
2. THE git-ignored settings files (e.g. the engine's `appsettings.Development.json`) SHALL be confirmed ignored and absent from every commit of the branch.
3. IF the scan finds a secret, THEN the push SHALL be blocked and the finding reported by commit, file and key name, never by value.

### Requirement 5: Final Review With the User

Completes task 7.4, "review with the user before pushing", and closes [17.3] and [17.4].

#### Acceptance Criteria

1. THE review SHALL present: the commits since the branch base, the results of Requirements 1–4, and the open points (including the legacy git-ignored `backend/src/AiSupportWorkflow.Presentation/appsettings.development.json`, whose LLM settings were migrated to the engine's ignored `appsettings.Development.json`: delete it or keep it).
2. THE branch SHALL NOT be pushed until the user approves; once approved it is pushed to `feature/llm-flows-platform` and a pull request to `master` is opened.
3. IF the user finds a behaviour that differs from the pre-feature support pipeline, THEN it SHALL be fixed (or recorded as a decision) before the push.

### Requirement 6: CI Review (optional, low priority)

Completes [2.1] for CI and [17.4] for the dashboard pipeline. Done only after Requirement 5, and may be deferred.

#### Acceptance Criteria

1. WHEN the pull request is opened, `.github/workflows/ci.yml` (`backend/Flowgent.slnx`) SHALL build and test the backend successfully.
2. `.github/workflows/dashboard-ci.yml` SHALL be reviewed and, where needed, aligned with the platform: Node version against `engines` (`>=20.19`), the `lint` script, path filters (including `backend/protos/**`, the source of the generated gRPC client).
3. No deployment workflow SHALL be added.
