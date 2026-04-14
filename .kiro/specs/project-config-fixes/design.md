# Project Configuration Fixes — Bugfix Design

## Overview

This bugfix addresses three project-hygiene issues that do not involve application code changes:

1. **Git tracking leak**: `appsettings.Development.json` contains development secrets (API keys) and is tracked by git despite being listed in `.gitignore`, because it was committed before the ignore rule was added.
2. **Incomplete HTTP test file**: The `.http` file has only a single generic POST test and basic GET endpoint tests. It lacks coverage for all 6 DummyApps bug scenarios (A1–A3, B1–B3) and 4 false-positive/edge-case tests (out-of-scope, ambiguous routing, failed routing, empty input).
3. **Wrong CI branch target**: `.github/workflows/ci.yml` triggers on PRs to `dev`, but the project's main branch is `master`.

All three are file-level configuration fixes with no impact on application runtime behavior.

## Glossary

- **Bug_Condition (C)**: The set of conditions that trigger each of the three configuration defects — a cached git file, missing HTTP test entries, and an incorrect branch target in CI YAML.
- **Property (P)**: The desired state after the fix — file untracked, HTTP tests complete, CI targeting `master`.
- **Preservation**: Existing behaviors that must remain unchanged — local dev config usage, existing GET endpoint tests in the HTTP file, CI job structure (branch validation, build, test steps).
- **`appsettings.Development.json`**: Development-only configuration in `src/AiSupportWorkflow.Presentation/` containing LLM provider settings and API keys.
- **`.http` file**: `src/AiSupportWorkflow.Presentation/AiSupportWorkflow.Presentation.http` — a manual HTTP test file used by developers to exercise the API.
- **`ci.yml`**: `.github/workflows/ci.yml` — the GitHub Actions CI pipeline definition.
- **DummyApps Bug Scenarios**: Six predefined bug scenarios (A1–A3 for ApplicationA, B1–B3 for ApplicationB) documented in `DummyApps/*/BugScenarios.md`, used as test fixtures for the support workflow.

## Bug Details

### Bug Condition

The bugs manifest across three independent configuration files. Each has a distinct trigger condition but all share the characteristic of being project-hygiene issues with no application code involvement.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type ProjectConfigState
  OUTPUT: boolean

  condition1 := input.file == "appsettings.Development.json"
                AND input.isInGitignore == true
                AND input.isTrackedByGit == true

  condition2 := input.file == "AiSupportWorkflow.Presentation.http"
                AND (input.hasBugScenarioTests(["A1","A2","A3","B1","B2","B3"]) == false
                     OR input.hasEdgeCaseTests(["out-of-scope","ambiguous","failed-routing","empty-input"]) == false)

  condition3 := input.file == "ci.yml"
                AND input.triggerBranch == "dev"
                AND input.expectedBranch == "master"

  RETURN condition1 OR condition2 OR condition3
END FUNCTION
```

### Examples

- **Bug 1**: Running `git status` after modifying `appsettings.Development.json` shows the file as modified, even though `.gitignore` contains the rule `appsettings.Development.json`. Expected: file should not appear in `git status`.
- **Bug 2**: A developer opens the `.http` file to test scenario A1 (Null Reference in Order Summary). There is no test for it — only a generic POST with a vague "500 error" email. Expected: a dedicated POST request with an email body describing `GetOrderSummary` crashing with null `Items`.
- **Bug 2 (edge case)**: A developer wants to test what happens when an email mentions both Application A and Application B. No such test exists. Expected: a dedicated POST request for the ambiguous routing scenario.
- **Bug 3**: A developer opens a PR targeting `master`. The CI pipeline does not run. Expected: CI triggers on PRs to `master`.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- `appsettings.Development.json` must continue to exist on the local filesystem and be usable for local development configuration
- The `.gitignore` rule for `appsettings.Development.json` must remain in place so future additions are ignored
- The existing GET endpoint tests in the HTTP file (`/api/support/issues/{id}`, `/api/support/issues`, `/api/support/stream`, `/api/support/agents`) must be preserved
- The CI pipeline must continue to execute branch name validation using the pattern `feature/[a-z0-9-]+`
- The CI pipeline must continue to build and test using .NET 10.0.x against `AiSupportWorkflow.sln`
- The CI pipeline job structure (`check-branch-name`, `build-and-test`) and their steps must remain unchanged

**Scope:**
All application source code, domain logic, infrastructure services, and test projects are completely out of scope. This fix touches only:
- Git index (cache removal)
- One `.http` file (additional test entries)
- One `.yml` file (branch name change)

## Hypothesized Root Cause

Based on the bug descriptions, the root causes are straightforward:

1. **Git cache not cleared**: `appsettings.Development.json` was committed to the repository before the `.gitignore` rule was added. Git continues to track files that are already in its index regardless of `.gitignore` rules. The fix requires explicitly removing the file from the git index with `git rm --cached`.

2. **HTTP test file never expanded**: The `.http` file was created with a single generic example POST and the standard GET endpoints. The 6 DummyApps bug scenarios and 4 edge-case tests were never added. The file needs 10 additional POST request entries with scenario-specific email payloads.

3. **Incorrect branch name in CI YAML**: The `on.pull_request.branches` array in `ci.yml` contains `dev` instead of `master`. This is likely a copy-paste error from a template or an oversight during initial setup.

## Correctness Properties

Property 1: Bug Condition — Git Tracking Removal

_For any_ state where `appsettings.Development.json` is listed in `.gitignore` AND is currently tracked by git, running `git rm --cached` on the file SHALL remove it from the git index so that `git status` no longer reports it as tracked, while the file remains on the local filesystem.

**Validates: Requirements 2.1**

Property 2: Bug Condition — HTTP Test Completeness

_For any_ state where the HTTP test file exists, the file SHALL contain exactly 6 dedicated POST requests corresponding to DummyApps bug scenarios (A1: Null Reference in Order Summary, A2: Incorrect Data Binding, A3: Missing Test for Empty Order, B1: SQL Injection in User Search, B2: Missing Null Check on Avatar URL, B3: Flaky Test Due to Hardcoded Date) AND exactly 4 false-positive/edge-case POST requests (out-of-scope email, ambiguous routing, failed routing, empty input validation).

**Validates: Requirements 2.2, 2.3**

Property 3: Bug Condition — CI Branch Target

_For any_ state where the CI pipeline YAML exists, the `on.pull_request.branches` array SHALL contain `master` and SHALL NOT contain `dev`.

**Validates: Requirements 2.4, 2.5**

Property 4: Preservation — Local Dev Config Unaffected

_For any_ state after the git cache fix is applied, `appsettings.Development.json` SHALL continue to exist on the local filesystem with its contents intact, and the `.gitignore` rule SHALL continue to prevent future tracking.

**Validates: Requirements 3.1, 3.2**

Property 5: Preservation — Existing HTTP Tests Retained

_For any_ state after the HTTP file is updated, the file SHALL continue to contain the existing GET endpoint tests for `/api/support/issues/{id}`, `/api/support/issues`, `/api/support/stream`, and `/api/support/agents`.

**Validates: Requirements 3.3**

Property 6: Preservation — CI Pipeline Structure Unchanged

_For any_ state after the CI YAML is updated, the pipeline SHALL continue to include the `check-branch-name` job with the `feature/[a-z0-9-]+` branch name validation pattern, and the `build-and-test` job using .NET 10.0.x and `AiSupportWorkflow.sln`.

**Validates: Requirements 3.4, 3.5**

## Fix Implementation

### Changes Required

**File 1**: Git index (not a file edit — a git command)

**Command**: `git rm --cached src/AiSupportWorkflow.Presentation/appsettings.Development.json`

**Effect**: Removes the file from git tracking. The file stays on disk. Future commits will not include it. The `.gitignore` rule already prevents re-addition.

---

**File 2**: `src/AiSupportWorkflow.Presentation/AiSupportWorkflow.Presentation.http`

**Changes**:
1. **Keep** the existing `@HostAddress` variable and all existing GET endpoint tests
2. **Replace** the single generic POST test with 6 scenario-specific POST tests:
   - A1: Email about `GetOrderSummary` crashing with null `Items` (mentions Application A)
   - A2: Email about `OrderSummary.razor` showing wrong property `TotalCost` instead of `TotalPrice` (mentions Application A)
   - A3: Email about missing test coverage for null/empty `Items` in order tests (mentions Application A)
   - B1: Email about `SearchUsers` endpoint concatenating user input into SQL (mentions Application B)
   - B2: Email about `UserProfile.razor` rendering broken image when `AvatarUrl` is null (mentions Application B)
   - B3: Email about `CreateUser_WithValidData_SetsCreatedDate` test failing due to hardcoded date (mentions Application B)
3. **Add** 4 false-positive/edge-case POST tests:
   - Out-of-scope: Non-code-related email (e.g., password reset request)
   - Ambiguous routing: Email mentioning both Application A and Application B
   - Failed routing: Email mentioning neither application
   - Empty input: Email with missing or blank subject/body

---

**File 3**: `.github/workflows/ci.yml`

**Function**: `on.pull_request.branches` trigger configuration

**Specific Change**: Replace `[dev]` with `[master]` on line 4. No other changes to the file.

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, verify the bugs exist in the current state, then verify the fixes resolve them and preserve existing behavior. Since these are configuration/file-level changes (not application code), testing is primarily manual verification and file-content inspection rather than automated unit/property tests.

### Exploratory Bug Condition Checking

**Goal**: Confirm the three bugs exist BEFORE implementing fixes.

**Test Plan**: Inspect current project state to verify each defect is present.

**Test Cases**:
1. **Git tracking test**: Run `git ls-files --cached src/AiSupportWorkflow.Presentation/appsettings.Development.json` — expect the file to be listed (confirms bug 1)
2. **HTTP file inspection**: Open `AiSupportWorkflow.Presentation.http` and count POST tests — expect only 1 generic test, no scenario-specific tests (confirms bug 2)
3. **CI branch inspection**: Read `ci.yml` and check `on.pull_request.branches` — expect `[dev]` instead of `[master]` (confirms bug 3)

**Expected Counterexamples**:
- `git ls-files` returns the file path, proving it is tracked despite `.gitignore`
- HTTP file contains only 1 POST request instead of the required 10 (6 scenarios + 4 edge cases)
- CI YAML contains `dev` where `master` is expected

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fix produces the expected state.

**Pseudocode:**
```
FOR ALL configState WHERE isBugCondition(configState) DO
  result := applyFix(configState)
  ASSERT expectedState(result)
END FOR
```

**Concrete checks**:
- After `git rm --cached`: `git ls-files --cached` no longer lists `appsettings.Development.json`
- After HTTP file update: file contains 6 scenario POST tests + 4 edge-case POST tests + all original GET tests
- After CI YAML update: `on.pull_request.branches` equals `[master]`

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, existing behavior is unchanged.

**Pseudocode:**
```
FOR ALL configState WHERE NOT isBugCondition(configState) DO
  ASSERT originalState(configState) = fixedState(configState)
END FOR
```

**Testing Approach**: Manual file-content verification is appropriate here because:
- The changes are isolated to 3 specific files/commands
- There is no application logic to generate random inputs against
- Preservation can be verified by diffing file contents before and after

**Test Cases**:
1. **Local file preservation**: After `git rm --cached`, verify `appsettings.Development.json` still exists on disk with original contents
2. **Gitignore preservation**: Verify `.gitignore` still contains the `appsettings.Development.json` rule
3. **GET endpoint preservation**: After HTTP file update, verify all 4 original GET tests are still present and unchanged
4. **CI job structure preservation**: After YAML update, verify `check-branch-name` job still validates `feature/[a-z0-9-]+` pattern and `build-and-test` job still uses .NET 10.0.x and `AiSupportWorkflow.sln`

### Unit Tests

- Verify `ci.yml` parses as valid YAML after the branch change
- Verify the HTTP file contains the correct number of `###` request separators (should be 15: 6 scenarios + 4 edge cases + 5 existing)
- Verify each scenario POST test references the correct application name in its email body

### Property-Based Tests

- Given the nature of these fixes (static file content, git commands), property-based testing has limited applicability. The "properties" are better expressed as content assertions:
  - For every scenario ID in `{A1, A2, A3, B1, B2, B3}`, the HTTP file contains a POST request whose body references that scenario's key terms
  - For every edge case in `{out-of-scope, ambiguous, failed-routing, empty-input}`, the HTTP file contains a corresponding POST request

### Integration Tests

- After all three fixes are applied, run `dotnet build AiSupportWorkflow.sln` to confirm no build regressions
- After the CI YAML change, verify the workflow triggers correctly by opening a test PR to `master` (manual)
- After the HTTP file update, run each POST request against a local dev server to verify they produce valid API responses (manual)
