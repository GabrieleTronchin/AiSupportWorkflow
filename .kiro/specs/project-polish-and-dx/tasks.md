# Implementation Plan: Project Polish & Developer Experience

## Overview

This plan implements 12 project polish and DX improvements across configuration, code style, documentation, packages, licensing, and CI/CD. Tasks are ordered for logical dependency: infrastructure cleanup first (remove obsolete class, migrate config), then code style refactoring (IEndpoint pattern), then documentation and static files, then NuGet updates (after code changes are stable), and finally CI/CD setup.

## Tasks

- [ ] 1. Remove obsolete OpenAIConfiguration class and migrate API key configuration
  - [ ] 1.1 Delete `OpenAIConfiguration.cs` and verify no remaining references
    - Delete `src/AiSupportWorkflow.Infrastructure/Configuration/OpenAIConfiguration.cs`
    - Search the entire solution for any references to `OpenAIConfiguration` and remove them
    - _Requirements: 6.1, 6.2_
  - [ ] 1.2 Update `SemanticKernelSetup.cs` to remove environment variable fallback and add validation
    - Remove the `Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? config.ApiKey` line
    - Replace with `config.ApiKey` only
    - Add guard clause: `if (string.IsNullOrWhiteSpace(config.ApiKey)) throw new InvalidOperationException("LLM API key is not configured. Set 'LlmProvider:ApiKey' in appsettings.Development.json.");`
    - _Requirements: 1.1, 1.2, 1.5_
  - [ ] 1.3 Create `appsettings.Development.json` with placeholder LlmProvider section
    - Create `src/AiSupportWorkflow.Presentation/appsettings.Development.json` with `LlmProvider` section containing placeholder `ApiKey`, `Provider` ("OpenAI"), and `ModelName` ("gpt-4o-mini")
    - Verify `appsettings.json` (base) does NOT contain any `ApiKey` value
    - _Requirements: 1.3, 1.4_
  - [ ] 1.4 Add `appsettings.Development.json` to `.gitignore`
    - Append `appsettings.Development.json` entry to the repository root `.gitignore`
    - _Requirements: 2.1, 2.2_

- [ ] 2. Adopt IEndpoint pattern for Minimal API endpoint registration
  - [ ] 2.1 Create `IEndpoint` interface and `ServiceExtension` class
    - Create `src/AiSupportWorkflow.Presentation/Endpoints/Primitives/IEndpoint.cs` with `void MapEndpoint(IEndpointRouteBuilder app)` method
    - Create `src/AiSupportWorkflow.Presentation/ServiceExtension.cs` with `AddEndpoints(Assembly)` for assembly-scanning registration and `MapEndpoints(WebApplication)` for mapping all discovered endpoints
    - _Requirements: 5.1, 5.2_
  - [ ] 2.2 Refactor `SupportEmailEndpoints` to implement `IEndpoint`
    - Convert from static extension method class to a class implementing `IEndpoint`
    - Use `app.MapGroup("/api/support").WithTags("Support Emails")` for route grouping
    - Move the POST `/emails` endpoint into the `MapEndpoint` method using the group
    - _Requirements: 5.1, 5.4_
  - [ ] 2.3 Refactor `WorkflowStatusEndpoints` to implement `IEndpoint`
    - Convert from static extension method class to a class implementing `IEndpoint`
    - Use `app.MapGroup("/api/support").WithTags("Workflow Status")` for route grouping
    - Move the GET `/issues/{id}` and GET `/issues` endpoints into the `MapEndpoint` method using the group
    - _Requirements: 5.1, 5.4_
  - [ ] 2.4 Refactor `VisualizationEndpoints` to implement `IEndpoint`
    - Convert from static extension method class to a class implementing `IEndpoint`
    - Use `app.MapGroup("/api/support").WithTags("Visualization")` for route grouping
    - Move the GET `/stream` and GET `/agents` endpoints into the `MapEndpoint` method using the group
    - Keep the private `StreamWorkflowStatesAsync` helper method
    - _Requirements: 5.1, 5.4_
  - [ ] 2.5 Update `Program.cs` to use `AddEndpoints` and `MapEndpoints`
    - Replace the three manual `app.MapSupportEmailEndpoints()`, `app.MapWorkflowStatusEndpoints()`, `app.MapVisualizationEndpoints()` calls
    - Use `builder.Services.AddEndpoints(typeof(Program).Assembly)` and `app.MapEndpoints()`
    - Ensure the concise composition style: builder setup, service registration via extension methods, middleware pipeline, `app.Run()`
    - _Requirements: 5.2, 5.3_

- [ ] 3. Checkpoint - Verify build and tests pass after refactoring
  - Ensure all tests pass with `dotnet build AiSupportWorkflow.sln` and `dotnet test AiSupportWorkflow.sln`, ask the user if questions arise.

- [ ] 4. Create HTTP file for endpoint testing
  - Create `src/AiSupportWorkflow.Presentation/AiSupportWorkflow.Presentation.http`
  - Define `@HostAddress = http://localhost:5080` variable
  - Add sample `POST` request to `/api/support/emails` with JSON body containing `Sender`, `Subject`, and `Body` fields
  - Add sample `GET` request to `/api/support/issues/{id}` with a placeholder GUID
  - Add sample `GET` request to `/api/support/issues`
  - Add sample `GET` request to `/api/support/stream`
  - Add sample `GET` request to `/api/support/agents`
  - Use `###` separators between each request
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8_

- [ ] 5. Create clean architecture documentation
  - Create `docs/clean-architecture.md`
  - Describe the four-layer structure: Domain, Application, Infrastructure, Presentation
  - Explain the inward dependency rule with project reference mapping
  - Map each layer to the corresponding project folder under `src/`
  - Verify the project's current structure follows clean architecture principles, document any deviations or confirm compliance
  - Reference Microsoft's official clean architecture guidance
  - Include a NuGet package license verification section confirming all packages are free/open-source (Apache-2.0, MIT, BSD-3-Clause)
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 9.1, 9.2_

- [ ] 6. Simplify the README
  - Rewrite `README.md` with concise, high-level content
  - Include project title and short description stating it is an AI-driven support workflow experiment
  - State the project was entirely generated by AI using Kiro as a spec-driven development experiment
  - Add brief "What It Does" section summarizing the workflow pipeline in a few sentences
  - Add "Key Technologies" section with links to official docs for Microsoft Semantic Kernel, Akka.NET, and OpenAI
  - Add "Getting Started" section explaining `appsettings.Development.json` creation with required API key and how to run the project
  - Add MIT License reference
  - Link to `docs/` folder for detailed architecture documentation
  - Remove detailed endpoint tables, package version tables, project structure trees, and configuration specifics
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 2.3, 10.3_

- [ ] 7. Add MIT License file
  - Create `LICENSE` file at the repository root with the full MIT License text
  - Include the current year and the project author's name in the copyright notice
  - _Requirements: 10.1, 10.2_

- [ ] 8. Update NuGet packages to latest stable versions
  - [ ] 8.1 Update all NuGet packages across the solution
    - Run `dotnet list package --outdated` to identify latest versions
    - Update all packages in all `.csproj` files: Akka.NET, Akka.Hosting, Akka.TestKit.Xunit2, Microsoft.SemanticKernel, Microsoft.SemanticKernel.Connectors.OpenAI, Microsoft.Extensions.Http.Resilience, Microsoft.Extensions.Options, Microsoft.Extensions.Options.ConfigurationExtensions, Microsoft.Extensions.Logging.Abstractions, xUnit, xunit.runner.visualstudio, FsCheck.Xunit, NSubstitute, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.AspNetCore.Mvc.Testing
    - Replace any preview/pre-release versions with latest stable where available
    - _Requirements: 8.1_
  - [ ] 8.2 Verify solution builds and all tests pass after package updates
    - Run `dotnet build AiSupportWorkflow.sln` and confirm no build errors
    - Run `dotnet test AiSupportWorkflow.sln` and confirm all unit tests and property-based tests pass
    - _Requirements: 8.2, 8.3_

- [ ] 9. Checkpoint - Verify full solution integrity
  - Ensure all tests pass with `dotnet build AiSupportWorkflow.sln` and `dotnet test AiSupportWorkflow.sln`, ask the user if questions arise.

- [ ] 10. Set up GitHub Actions CI pipeline and branch naming enforcement
  - [ ] 10.1 Create GitHub Actions CI workflow
    - Create `.github/workflows/ci.yml`
    - Trigger on pull requests targeting the `dev` branch
    - Include sequential steps: checkout, setup .NET 10.0 SDK, `dotnet restore`, `dotnet build --no-restore`, `dotnet test --no-build`
    - Failing build or test steps must block the PR from merging
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5_
  - [ ] 10.2 Add branch naming convention enforcement
    - Add a `check-branch-name` job (or step) in the CI workflow that validates the source branch matches `feature/[a-z0-9-]+`
    - Use `github.head_ref` to get the source branch name
    - Fail the workflow with a descriptive error message if the pattern doesn't match
    - _Requirements: 12.1, 12.2, 12.3_

- [ ] 11. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass with `dotnet build AiSupportWorkflow.sln` and `dotnet test AiSupportWorkflow.sln`, ask the user if questions arise.

## Notes

- No property-based tests are included — the design explicitly assessed PBT as not applicable (all changes are configuration, documentation, refactoring, and CI/CD with no pure functions or algorithmic logic)
- Existing 39 unit tests and 15 property-based tests must remain green throughout all changes
- Tasks are ordered so that code-breaking changes (removing OpenAIConfiguration, refactoring endpoints) happen before documentation and NuGet updates
- NuGet updates are placed after code refactoring to avoid updating packages that might be affected by the refactoring
