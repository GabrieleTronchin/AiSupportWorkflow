# Design Document: LLM Flow Modules, follow-up (round 1 closing)

## Overview

No production code changes are planned. The work is a fixed sequence of checks on the Closing_Commit, one browser smoke run, a secret scan and a review with the user, then an optional CI pass. A check that fails is fixed with a targeted commit and the sequence restarts from Requirement 1 on the new HEAD.

```
1. automated verification ─> 2. AppHost run ─> 3. browser smoke ─> 4. secret scan ─> 5. user review ─> push + PR ─> 6. CI (optional)
```

## 1. Automated verification (Req 1)

From the repository root (`global.json` is read from the working directory):

```bash
dotnet build backend/Flowgent.slnx
dotnet test --solution backend/Flowgent.slnx
cd dashboard && npm install && npm run typecheck && npm run test -- --run && npm run build
```

- Baseline from round 1: 314 tests passed (commit 0dc8fec). A lower count needs an explanation in the review.
- `package-lock.json` after `npm install`: compare `git diff dashboard/package-lock.json` with `package.json`. Only metadata or resolved/integrity reordering → `git checkout -- dashboard/package-lock.json`. Any added, removed or re-versioned package → leave it and report it.

## 2. AppHost run (Req 2)

```bash
dotnet run --project backend/src/AppHost/Flowgent.AppHost -- --AppHost:LlmProvider=Demo
```

- Aspire dashboard at http://localhost:15080: `engine`, `support`, `gateway`, `dashboard` running and healthy (`template` absent, disabled by default).
- `GET http://localhost:5080/api/engine/modules`: `support` available, `errors` empty.
- `GET http://localhost:5080/api/engine/config`: `demoMode: true`.

## 3. Browser smoke (Req 3)

Run once against the platform from step 2, with the Playwright browser tools (MCP). No Playwright package, config or test project is added to the repository. If the user wants it repeatable, it becomes a single committed spec in phase 6 (see Decisions).

| # | Action | Expected |
|---|--------|----------|
| 1 | Open http://localhost:5173/ | Overview with the module/workflow picker and the LLM badge showing Demo |
| 2 | Select `support` / `support-email` | Graph with the workflow steps; form generated from the input schema |
| 3 | Pick sample `scenario-a1` ("A1: NullReferenceException"), submit | Run accepted; graph advances to `approval` |
| 4 | Open `/approvals`, approve the run | Run leaves the list |
| 5 | Open `/runs` | Run `Completed` on step `code_change_generated` |

During the run:

- Network requests: only `http://localhost:5173/api/**` and `/flowgent.RunMonitor/**` (Vite proxy → gateway :5080). No request to :5081 or :5082.
- Console: no errors.

Everything else in Req 17.3 is already asserted in-process: `SupportParityTests` (all ten samples through the gateway, approve/reject/abort) and `Flowgent.Integration.Tests` (isolation, module down, version reload, gRPC-Web through the gateway).

## 4. Secret scan (Req 4)

`gitleaks` is not installed on the dev machine. Use it if available, otherwise the git fallback:

```bash
gitleaks git --log-opts="origin/master..HEAD" --redact
```

```powershell
# history of the branch: lines added or removed that match a secret pattern
git log -p origin/master..HEAD -G '<pattern>'
# tracked files of the Closing_Commit
git grep -nE '<pattern>' HEAD
# ignored settings never committed on the branch
git log --name-only --format= origin/master..HEAD | Select-String -SimpleMatch 'appsettings.Development.json'
git check-ignore -v backend/src/Services/Flowgent.Engine.Service/appsettings.Development.json
```

Patterns: `sk-[A-Za-z0-9_-]{20,}`, `gh[pousr]_[A-Za-z0-9]{36}`, `github_pat_`, `AKIA[0-9A-Z]{16}`, `-----BEGIN [A-Z ]*PRIVATE KEY-----`, `AccountKey=`, and non-empty values for keys named `ApiKey`, `Password`, `Secret`, `Token`. Empty placeholders (`"ApiKey": ""`) are allowed. Findings are reported by commit, file and key name only. The ignored files themselves are not read.

## 5. User review (Req 5)

The review summary contains:

- `git log --oneline origin/master..HEAD`
- results of steps 1–4 (counts, smoke outcome, scan outcome)
- open points:
  - the legacy git-ignored `backend/src/AiSupportWorkflow.Presentation/appsettings.development.json` (contains secrets; LLM settings already migrated to the engine's ignored `appsettings.Development.json`): the user decides whether to delete it
  - whether to keep the smoke test as a committed Playwright spec (Decisions)

After the user approves: `git push -u origin feature/llm-flows-platform` and `gh pr create --base master`.

## 6. CI review (Req 6, optional)

- `ci.yml`: already on `backend/Flowgent.slnx` with path filters `backend/**`, `modules/**`, `global.json`. Check the first PR run only.
- `dashboard-ci.yml` (not reviewed in round 1), points to check:
  - `node-version: 20` against `engines.node >=20.19` (pin `20.19` or `22`)
  - `npm run lint` is `tsc --noEmit`, same as `typecheck`; ESLint packages are installed but there is no ESLint config in `dashboard/`
  - path filter is `dashboard/**` only; `backend/protos/**` changes the generated client
- Changes stay in the workflow files; no deployment jobs.

## Decisions

1. Smoke test runs through the Playwright browser tools, not a committed test suite. A committed `@playwright/test` spec (one file, pinned version, outside Vitest) is optional and only if the user asks for it at the review.
2. No new automated tests: the Harness_Tests already cover parity and multi-module behaviour.
3. CI is the last phase and can be deferred past the push.
