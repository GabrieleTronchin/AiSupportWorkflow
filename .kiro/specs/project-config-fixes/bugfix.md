# Bugfix Requirements Document

## Introduction

This document describes three project configuration issues that impact the development workflow: a sensitive development configuration file tracked by git despite being in `.gitignore`, an incomplete HTTP test file lacking coverage for the 6 DummyApps bug scenarios and false-positive edge cases, and a CI pipeline targeting the wrong branch. These bugs affect project hygiene, manual test coverage, and continuous integration — not application logic.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN `appsettings.Development.json` was committed before the `.gitignore` rule was added THEN the system continues to track the file in the git repository, potentially exposing development secrets (e.g., API keys)

1.2 WHEN a developer opens the HTTP test file to manually test the API THEN the system provides only a single generic POST test and basic GET endpoint tests, with no coverage for the 6 DummyApps bug scenarios (A1: Null Reference in Order Summary, A2: Incorrect Data Binding in Order Summary, A3: Missing Test for Empty Order, B1: SQL Injection in User Search, B2: Missing Null Check on Avatar URL, B3: Flaky Test Due to Hardcoded Date)

1.3 WHEN a developer wants to test false-positive and edge-case scenarios via the HTTP file THEN the system provides no tests for: out-of-scope emails (non-code-related), ambiguous routing (email mentioning both Application A and Application B), failed routing (email mentioning neither application), or empty input validation (missing subject or body)

1.4 WHEN a pull request is opened targeting the `dev` branch THEN the CI pipeline triggers, but `dev` is not the correct target branch for this project

1.5 WHEN a pull request is opened targeting the `master` branch THEN the CI pipeline does not trigger, preventing automatic build and test validation

### Expected Behavior (Correct)

2.1 WHEN `appsettings.Development.json` is listed in `.gitignore` THEN the system SHALL NOT track the file in the git repository; the file must be removed from git tracking via `git rm --cached` while remaining on the local filesystem

2.2 WHEN a developer opens the HTTP test file THEN the system SHALL provide a dedicated POST test for each of the 6 DummyApps bug scenarios: A1 (Null Reference in Order Summary — email about `GetOrderSummary` crashing with null `Items`), A2 (Incorrect Data Binding — email about `OrderSummary.razor` showing wrong property `TotalCost` instead of `TotalPrice`), A3 (Missing Test for Empty Order — email about missing test coverage for null/empty `Items`), B1 (SQL Injection in User Search — email about `SearchUsers` endpoint concatenating user input into SQL), B2 (Missing Null Check on Avatar URL — email about `UserProfile.razor` rendering broken image when `AvatarUrl` is null), B3 (Flaky Test Due to Hardcoded Date — email about `CreateUser_WithValidData_SetsCreatedDate` test failing due to hardcoded date)

2.3 WHEN a developer opens the HTTP test file THEN the system SHALL provide false-positive and edge-case tests including: an out-of-scope email (non-code-related request such as a password reset), an ambiguous routing email (mentioning both Application A and Application B), a failed routing email (mentioning neither application), and an empty input validation email (missing or blank subject/body)

2.4 WHEN a pull request is opened targeting the `master` branch THEN the CI pipeline SHALL trigger and execute branch name validation, build, and test jobs

2.5 WHEN a pull request is opened targeting the `dev` branch THEN the CI pipeline SHALL NOT trigger, since `master` is the correct target branch

### Unchanged Behavior (Regression Prevention)

3.1 WHEN `appsettings.Development.json` exists on a developer's local filesystem THEN the system SHALL CONTINUE TO allow the file to be used for local development configuration without any impact

3.2 WHEN the `.gitignore` file contains the rule `appsettings.Development.json` THEN the system SHALL CONTINUE TO ignore any future additions of the file in new commits

3.3 WHEN the existing GET endpoint tests (`/api/support/issues/{id}`, `/api/support/issues`, `/api/support/stream`, `/api/support/agents`) are present in the HTTP file THEN the system SHALL CONTINUE TO include them in the updated HTTP file

3.4 WHEN the CI pipeline is triggered THEN the system SHALL CONTINUE TO execute branch name validation using the pattern `feature/[a-z0-9-]+`

3.5 WHEN the CI pipeline is triggered THEN the system SHALL CONTINUE TO build and test using .NET 10.0.x against the `AiSupportWorkflow.sln` solution
