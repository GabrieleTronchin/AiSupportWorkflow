# Implementation Plan: LLM Flow Modules, follow-up (round 1 closing)

## Overview

Closes task 7.4 of [`llm-flow-modules`](../llm-flow-modules/tasks.md). Phases run in order; a failure is fixed with a targeted commit on `feature/llm-flows-platform` and the run restarts from phase 1 on the new HEAD. Nothing is pushed before phase 4.

## Tasks

- [ ] 1. Automated verification on the Closing_Commit
  - [ ] 1.1 `dotnet build backend/LlmFlows.slnx` (0 warnings, 0 errors) and `dotnet test --solution backend/LlmFlows.slnx` (no failures; round-1 baseline 314 passed).
    - _Requirements: 1.1, 1.4_
  - [ ] 1.2 Dashboard: `npm install`, `npm run typecheck`, `npm run test -- --run`, `npm run build`; restore `package-lock.json` if the diff is only install noise, otherwise report it.
    - _Requirements: 1.2, 1.3, 1.4_

- [ ] 2. Platform run and browser smoke
  - [ ] 2.1 Start the AppHost with `--AppHost:LlmProvider=Demo`; check the Aspire dashboard, `/api/engine/modules` and `/api/engine/config` through the gateway.
    - _Requirements: 2.1, 2.2_
  - [ ] 2.2 Run the Smoke_Scenario with the Playwright browser tools: support / support-email, sample `scenario-a1`, approve, run `Completed` on `code_change_generated`; check network targets and console. No other browser scenario.
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [ ] 3. Secret scan
  - [ ] 3.1 Scan `origin/master..HEAD` and the tracked files (gitleaks if installed, git fallback otherwise); confirm ignored settings files were never committed. Report findings by commit, file and key name only.
    - _Requirements: 4.1, 4.2, 4.3_

- [ ] 4. Review with the user and push
  - [ ] 4.1 Present commits, results of phases 1–3 and open points (legacy `AiSupportWorkflow.Presentation/appsettings.development.json`, committed smoke spec yes/no).
    - _Requirements: 5.1, 5.3_
  - [ ] 4.2 After approval: push `feature/llm-flows-platform` and open the PR to `master`.
    - _Requirements: 5.2_

- [ ] 5. CI (optional, low priority)
  - [ ] 5.1 Check the first PR run of `ci.yml`.
    - _Requirements: 6.1_
  - [ ] 5.2 Review `dashboard-ci.yml` (Node version, `lint` script, path filters) and align it; no deployment jobs.
    - _Requirements: 6.2, 6.3_
