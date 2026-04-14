# Implementation Plan

- [x] 1. Write bug condition exploration tests
  - **Property 1: Bug Condition** — Project Configuration Defects
  - **CRITICAL**: Run these checks BEFORE implementing any fixes
  - **GOAL**: Surface counterexamples that demonstrate all three bugs exist
  - **Checks to perform:**
    1. Run `git ls-files --cached src/AiSupportWorkflow.Presentation/appsettings.Development.json` — expect the file to be listed (proves Bug 1: file is tracked despite `.gitignore`)
    2. Inspect `src/AiSupportWorkflow.Presentation/AiSupportWorkflow.Presentation.http` — expect only 1 generic POST test and 4 GET tests; no scenario-specific POST tests for A1–A3, B1–B3; no false-positive/edge-case tests (proves Bug 2: HTTP file incomplete)
    3. Read `.github/workflows/ci.yml` line 4 `on.pull_request.branches` — expect `[dev]` instead of `[master]` (proves Bug 3: wrong CI branch target)
  - **EXPECTED OUTCOME**: All three defects are confirmed present
  - Document counterexamples found:
    - `git ls-files` returns `src/AiSupportWorkflow.Presentation/appsettings.Development.json` — file tracked despite `.gitignore`
    - HTTP file contains only 1 POST request instead of the required 11 (6 scenarios + 4 edge cases + 1 existing generic)
    - CI YAML `on.pull_request.branches` contains `dev` where `master` is expected
  - Mark task complete when all three bugs are confirmed and documented
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 2. Write preservation verification tests (BEFORE implementing fixes)
  - **Property 2: Preservation** — Existing Behavior Baseline
  - **IMPORTANT**: Follow observation-first methodology — verify current correct behaviors BEFORE any changes
  - **Checks to perform:**
    1. Verify `appsettings.Development.json` exists on disk at `src/AiSupportWorkflow.Presentation/appsettings.Development.json` with its LLM provider configuration intact
    2. Verify `.gitignore` contains the rule `appsettings.Development.json` at the end of the file
    3. Verify the HTTP file contains all 4 existing GET endpoint tests: `/api/support/issues/{id}`, `/api/support/issues`, `/api/support/stream`, `/api/support/agents`
    4. Verify `ci.yml` contains the `check-branch-name` job with `feature/[a-z0-9-]+` branch name validation pattern
    5. Verify `ci.yml` contains the `build-and-test` job using `dotnet-version: '10.0.x'` and `AiSupportWorkflow.sln`
  - **EXPECTED OUTCOME**: All preservation baselines confirmed — these behaviors must remain unchanged after fixes
  - Mark task complete when all baselines are documented and confirmed passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 3. Fix Bug 1: Remove `appsettings.Development.json` from git tracking

  - [x] 3.1 Run `git rm --cached src/AiSupportWorkflow.Presentation/appsettings.Development.json`
    - This removes the file from the git index (stops tracking) while keeping it on the local filesystem
    - The `.gitignore` rule already prevents future re-addition
    - _Bug_Condition: isBugCondition(input) where input.file == "appsettings.Development.json" AND input.isInGitignore == true AND input.isTrackedByGit == true_
    - _Expected_Behavior: File is no longer tracked by git; `git ls-files --cached` no longer lists it_
    - _Preservation: File remains on disk with original contents; `.gitignore` rule unchanged_
    - _Requirements: 2.1, 3.1, 3.2_

  - [x] 3.2 Verify bug condition exploration test now passes for Bug 1
    - **Property 1: Expected Behavior** — Git Tracking Removal
    - **IMPORTANT**: Re-run the SAME check from task 1 — do NOT write a new test
    - Run `git ls-files --cached src/AiSupportWorkflow.Presentation/appsettings.Development.json` — expect empty output (file no longer tracked)
    - Verify `appsettings.Development.json` still exists on disk with original contents
    - **EXPECTED OUTCOME**: Check PASSES (confirms Bug 1 is fixed)
    - _Requirements: 2.1_

  - [x] 3.3 Verify preservation tests still pass for Bug 1
    - **Property 2: Preservation** — Local Dev Config Unaffected
    - **IMPORTANT**: Re-run the SAME checks from task 2 — do NOT write new tests
    - Verify `appsettings.Development.json` still exists on disk at `src/AiSupportWorkflow.Presentation/appsettings.Development.json`
    - Verify `.gitignore` still contains the `appsettings.Development.json` rule
    - **EXPECTED OUTCOME**: Checks PASS (confirms no regressions)

- [x] 4. Fix Bug 2: Expand HTTP test file with scenario and edge-case tests

  - [x] 4.1 Add 6 DummyApps bug scenario POST tests to the HTTP file
    - Edit `src/AiSupportWorkflow.Presentation/AiSupportWorkflow.Presentation.http`
    - Keep the existing `@HostAddress` variable and all existing GET endpoint tests
    - Replace the single generic POST test with 6 scenario-specific POST requests to `/api/support/emails`:
      - **A1**: Null Reference in Order Summary — email about `GetOrderSummary` crashing with `NullReferenceException` when `order.Items` is null (mentions Application A, `OrderController.cs`)
      - **A2**: Incorrect Data Binding — email about `OrderSummary.razor` showing wrong property `TotalCost` instead of `TotalPrice` (mentions Application A)
      - **A3**: Missing Test for Empty Order — email about missing test coverage for null/empty `Items` in `OrderServiceTests` (mentions Application A)
      - **B1**: SQL Injection in User Search — email about `SearchUsers` endpoint concatenating user input directly into SQL query (mentions Application B, `UserController.cs`)
      - **B2**: Missing Null Check on Avatar URL — email about `UserProfile.razor` rendering broken image when `AvatarUrl` is null (mentions Application B)
      - **B3**: Flaky Test Due to Hardcoded Date — email about `CreateUser_WithValidData_SetsCreatedDate` test failing due to hardcoded `DateTime(2024, 1, 15)` (mentions Application B)
    - Each POST must use `Content-Type: application/json` with `Sender`, `Subject`, and `Body` fields
    - Each email body must reference the correct application name and scenario-specific technical details from `DummyApps/*/BugScenarios.md`
    - _Bug_Condition: isBugCondition(input) where input.hasBugScenarioTests(["A1","A2","A3","B1","B2","B3"]) == false_
    - _Expected_Behavior: HTTP file contains 6 dedicated POST requests for all DummyApps bug scenarios_
    - _Preservation: Existing GET endpoint tests retained unchanged_
    - _Requirements: 2.2, 3.3_

  - [x] 4.2 Add 4 false-positive and edge-case POST tests to the HTTP file
    - Append to the HTTP file after the scenario tests:
      - **Out-of-scope**: Non-code-related email (e.g., password reset request) — should be classified as OutOfScope
      - **Ambiguous routing**: Email mentioning both Application A and Application B — should fail routing with ambiguous error
      - **Failed routing**: Email mentioning neither application — should fail routing with no match
      - **Empty input**: Email with missing or blank subject/body — should fail email validation
    - Each POST must use `Content-Type: application/json` to `/api/support/emails`
    - _Bug_Condition: isBugCondition(input) where input.hasEdgeCaseTests(["out-of-scope","ambiguous","failed-routing","empty-input"]) == false_
    - _Expected_Behavior: HTTP file contains 4 false-positive/edge-case POST requests_
    - _Requirements: 2.3_

  - [x] 4.3 Verify bug condition exploration test now passes for Bug 2
    - **Property 1: Expected Behavior** — HTTP Test Completeness
    - **IMPORTANT**: Re-run the SAME check from task 1 — do NOT write a new test
    - Inspect the HTTP file and confirm it contains exactly 6 scenario POST tests (A1–A3, B1–B3) + 4 edge-case POST tests + the original GET tests
    - Count `###` request separators — expect 15 total (6 scenarios + 4 edge cases + 5 existing GET/original)
    - **EXPECTED OUTCOME**: Check PASSES (confirms Bug 2 is fixed)
    - _Requirements: 2.2, 2.3_

  - [x] 4.4 Verify preservation tests still pass for Bug 2
    - **Property 2: Preservation** — Existing HTTP Tests Retained
    - **IMPORTANT**: Re-run the SAME checks from task 2 — do NOT write new tests
    - Verify the HTTP file still contains GET tests for `/api/support/issues/{id}`, `/api/support/issues`, `/api/support/stream`, `/api/support/agents`
    - **EXPECTED OUTCOME**: Checks PASS (confirms no regressions)
    - _Requirements: 3.3_

- [x] 5. Fix Bug 3: Update CI pipeline branch target from `dev` to `master`

  - [x] 5.1 Change `on.pull_request.branches` from `[dev]` to `[master]` in `.github/workflows/ci.yml`
    - Edit line 4 of `ci.yml`: replace `branches: [dev]` with `branches: [master]`
    - No other changes to the file — job structure, steps, and configuration remain identical
    - _Bug_Condition: isBugCondition(input) where input.triggerBranch == "dev" AND input.expectedBranch == "master"_
    - _Expected_Behavior: `on.pull_request.branches` contains `master` and does not contain `dev`_
    - _Preservation: `check-branch-name` job, `build-and-test` job, .NET 10.0.x, `AiSupportWorkflow.sln` all unchanged_
    - _Requirements: 2.4, 2.5, 3.4, 3.5_

  - [x] 5.2 Verify bug condition exploration test now passes for Bug 3
    - **Property 1: Expected Behavior** — CI Branch Target
    - **IMPORTANT**: Re-run the SAME check from task 1 — do NOT write a new test
    - Read `ci.yml` and confirm `on.pull_request.branches` is `[master]` and does not contain `dev`
    - **EXPECTED OUTCOME**: Check PASSES (confirms Bug 3 is fixed)
    - _Requirements: 2.4, 2.5_

  - [x] 5.3 Verify preservation tests still pass for Bug 3
    - **Property 2: Preservation** — CI Pipeline Structure Unchanged
    - **IMPORTANT**: Re-run the SAME checks from task 2 — do NOT write new tests
    - Verify `check-branch-name` job still validates `feature/[a-z0-9-]+` pattern
    - Verify `build-and-test` job still uses `dotnet-version: '10.0.x'` and `AiSupportWorkflow.sln`
    - **EXPECTED OUTCOME**: Checks PASS (confirms no regressions)
    - _Requirements: 3.4, 3.5_

- [x] 6. Checkpoint — Ensure all fixes are applied and verified
  - Confirm Bug 1: `appsettings.Development.json` is no longer tracked by git (but exists on disk)
  - Confirm Bug 2: HTTP file contains 6 scenario tests + 4 edge-case tests + all original GET tests
  - Confirm Bug 3: CI pipeline targets `master` instead of `dev`
  - Confirm all preservation requirements are satisfied (local config, existing GET tests, CI job structure)
  - Run `dotnet build AiSupportWorkflow.sln` to verify no build regressions
  - Ask the user if questions arise
