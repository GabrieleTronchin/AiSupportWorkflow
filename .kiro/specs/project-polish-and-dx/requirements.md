# Requirements Document

## Introduction

This specification covers a set of project optimizations and developer experience (DX) improvements for the AI Support Workflow .NET project. The changes consolidate multiple areas into a single spec: migrating sensitive configuration from environment variables to `appsettings.Development.json`, adding `.gitignore` rules to prevent secret leakage, creating an `.http` file for quick endpoint testing, simplifying the README, aligning the code style with the author's personal project conventions (as seen in [ASPNETFilters](https://github.com/GabrieleTronchin/ASPNETFilters), [CinemaSample](https://github.com/GabrieleTronchin/CinemaSample), and [MediatRPipelines](https://github.com/GabrieleTronchin/MediatRPipelines)), documenting the clean architecture approach, updating NuGet packages to latest stable versions, auditing packages for paid license compliance, adding an MIT license, setting up a GitHub Actions CI pipeline for pull requests targeting `dev`, and enforcing a branch naming convention.

## Glossary

- **Presentation_Project**: The `AiSupportWorkflow.Presentation` ASP.NET project that serves as the composition root and REST API host.
- **Infrastructure_Project**: The `AiSupportWorkflow.Infrastructure` project containing Semantic Kernel setup, actors, and external service integrations.
- **AppSettings_Development**: The `appsettings.Development.json` file located in the Presentation_Project, used for local development configuration overrides.
- **AppSettings_Base**: The `appsettings.json` file located in the Presentation_Project, containing non-sensitive default configuration.
- **GitIgnore**: The `.gitignore` file at the repository root.
- **HTTP_File**: A `.http` file placed in the Presentation_Project directory, used by Visual Studio / VS Code REST Client to send sample HTTP requests.
- **README**: The `README.md` file at the repository root.
- **SemanticKernelSetup**: The `SemanticKernelSetup.cs` class in the Infrastructure_Project that configures the Semantic Kernel and reads the OpenAI API key.
- **LlmProviderConfiguration**: The configuration class in the Infrastructure_Project that holds LLM provider settings including API key, model name, and provider type.
- **IEndpoint_Pattern**: A Minimal API endpoint registration pattern using an `IEndpoint` interface, `MapEndpoint` method, `AddEndpoints` assembly scanning, and `MapEndpoints` extension — as used in the author's MediatRPipelines project.
- **Code_Style_References**: The three GitHub repositories provided by the author as style references: ASPNETFilters, CinemaSample, and MediatRPipelines.
- **Clean_Architecture_Doc**: A documentation file in the `docs/` folder that describes how clean architecture is applied in this project, including layer structure, dependency rules, and folder mapping.
- **Solution**: The `AiSupportWorkflow.sln` file and all projects it references.
- **NuGet_Packages**: All third-party package references declared in `.csproj` files across the Solution.
- **LICENSE_File**: The `LICENSE` file at the repository root containing the project's open-source license text.
- **CI_Pipeline**: A GitHub Actions workflow file in `.github/workflows/` that automates build and test verification for pull requests.
- **Dev_Branch**: The `dev` branch used as the integration branch for feature work.
- **Branch_Naming_Convention**: The required pattern `feature/{branch-name}` that all feature branches must follow when targeting Dev_Branch.

## Requirements

### Requirement 1: Migrate Sensitive Configuration to appsettings.Development.json

**User Story:** As a developer, I want the OpenAI API key and other sensitive settings to be read from appsettings.Development.json instead of environment variables, so that I can configure the project locally without setting machine-level environment variables.

#### Acceptance Criteria

1. WHEN the application starts in Development environment, THE SemanticKernelSetup SHALL read the API key from the `LlmProvider:ApiKey` configuration path in AppSettings_Development.
2. THE SemanticKernelSetup SHALL remove the `Environment.GetEnvironmentVariable("OPENAI_API_KEY")` fallback logic.
3. THE AppSettings_Development SHALL contain a `LlmProvider` section with placeholder values for `ApiKey`, `Provider`, and `ModelName`.
4. THE AppSettings_Base SHALL NOT contain any API key values or sensitive secrets.
5. WHEN the `LlmProvider:ApiKey` configuration value is empty or missing, THE SemanticKernelSetup SHALL throw an `InvalidOperationException` with a descriptive message indicating the API key is not configured.

### Requirement 2: Exclude appsettings.Development.json from Version Control

**User Story:** As a developer, I want appsettings.Development.json excluded from Git, so that sensitive configuration like API keys cannot accidentally be committed to the repository.

#### Acceptance Criteria

1. THE GitIgnore SHALL contain an entry that excludes `appsettings.Development.json` files from version control.
2. WHEN a developer clones the repository, THE repository SHALL NOT contain any `appsettings.Development.json` file with real secrets.
3. THE README SHALL document that the developer must create `appsettings.Development.json` locally and provide the expected structure.

### Requirement 3: Create an HTTP File for Endpoint Testing

**User Story:** As a developer, I want an `.http` file with sample requests for all API endpoints, so that I can quickly test the workflow without external tools.

#### Acceptance Criteria

1. THE HTTP_File SHALL be named `AiSupportWorkflow.Presentation.http` and placed in the Presentation_Project directory.
2. THE HTTP_File SHALL define a `@HostAddress` variable set to `http://localhost:5080`.
3. THE HTTP_File SHALL contain a sample `POST` request to `/api/support/emails` with a valid JSON body including `Sender`, `Subject`, and `Body` fields.
4. THE HTTP_File SHALL contain a sample `GET` request to `/api/support/issues/{id}` with a placeholder GUID.
5. THE HTTP_File SHALL contain a sample `GET` request to `/api/support/issues`.
6. THE HTTP_File SHALL contain a sample `GET` request to `/api/support/stream`.
7. THE HTTP_File SHALL contain a sample `GET` request to `/api/support/agents`.
8. THE HTTP_File SHALL use `###` separators between each request, following the standard `.http` file format used in the author's Code_Style_References.

### Requirement 4: Simplify the README

**User Story:** As a developer, I want the README to be a concise, high-level overview of the project, so that it clearly communicates what the project does and that it is an experiment, without excessive implementation detail.

#### Acceptance Criteria

1. THE README SHALL contain a project title and a short description stating that the project is an AI-driven support workflow experiment.
2. THE README SHALL state that the project was entirely generated by AI using Kiro as a spec-driven development experiment.
3. THE README SHALL include a brief "What It Does" section summarizing the workflow pipeline in a few sentences.
4. THE README SHALL include a "Key Technologies" section listing the main libraries used: Microsoft Semantic Kernel, Akka.NET, and OpenAI, with links to their official documentation.
5. THE README SHALL include a "Getting Started" section that explains how to create `appsettings.Development.json` with the required API key, and how to run the project.
6. THE README SHALL NOT contain detailed tables of API endpoints, package versions, project structure trees, or configuration specifics.
7. THE README SHALL link to the `docs/` folder for detailed architecture documentation.

### Requirement 5: Align Code Style with Author's Conventions

**User Story:** As a developer, I want the project's code style to match my personal coding conventions from my other GitHub projects, so that the codebase feels consistent with my own work.

#### Acceptance Criteria

1. THE Presentation_Project SHALL adopt the `IEndpoint` interface pattern for Minimal API endpoint registration, where each endpoint group implements an `IEndpoint` interface with a `MapEndpoint(IEndpointRouteBuilder app)` method.
2. THE Presentation_Project SHALL use a `ServiceExtension` class with `AddEndpoints(Assembly)` for assembly-scanning endpoint registration and `MapEndpoints(WebApplication)` for mapping all discovered endpoints.
3. THE Presentation_Project Program.cs SHALL follow the concise composition style seen in the Code_Style_References: builder setup, service registration via extension methods, middleware pipeline, and `app.Run()` with minimal inline logic.
4. WHEN endpoint groups use route grouping, THE endpoint classes SHALL use `app.MapGroup("/path").WithTags("Tag Name")` to organize routes, consistent with the Code_Style_References.
5. THE Presentation_Project SHALL maintain the existing `.editorconfig` rules for file-scoped namespaces, naming conventions, and formatting.

### Requirement 6: Remove Obsolete OpenAIConfiguration Class

**User Story:** As a developer, I want unused configuration classes removed, so that the codebase stays clean and there is a single source of truth for LLM configuration.

#### Acceptance Criteria

1. THE Infrastructure_Project SHALL remove the `OpenAIConfiguration` class since `LlmProviderConfiguration` already covers the same settings.
2. WHEN the `OpenAIConfiguration` class is removed, THE Infrastructure_Project SHALL have no remaining references to the removed class.

### Requirement 7: Clean Architecture Documentation

**User Story:** As a developer, I want a documentation file that describes how clean architecture is used in this project, so that contributors can understand the layer structure, dependency rules, and folder mapping without reading the entire codebase.

#### Acceptance Criteria

1. THE Clean_Architecture_Doc SHALL be placed in the `docs/` folder with a descriptive filename.
2. THE Clean_Architecture_Doc SHALL describe the four-layer structure: Domain, Application, Infrastructure, and Presentation.
3. THE Clean_Architecture_Doc SHALL explain the inward dependency rule: Domain has zero external dependencies, Application depends only on Domain, Infrastructure implements Domain interfaces, and Presentation is the composition root.
4. THE Clean_Architecture_Doc SHALL map each layer to the corresponding project folder under `src/`.
5. THE Clean_Architecture_Doc SHALL reference Microsoft's official clean architecture guidance as an authoritative source.
6. THE Clean_Architecture_Doc SHALL verify that the project's current structure follows clean architecture principles by documenting any deviations or confirming full compliance.

### Requirement 8: Update NuGet Packages to Latest Stable Versions

**User Story:** As a developer, I want all NuGet packages updated to their latest stable versions, so that the project benefits from bug fixes, performance improvements, and security patches.

#### Acceptance Criteria

1. THE Solution SHALL have all NuGet_Packages updated to their latest stable versions, including Akka.NET, Akka.Hosting, Akka.TestKit.Xunit2, Microsoft.SemanticKernel, Microsoft.SemanticKernel.Connectors.OpenAI, Microsoft.Extensions.Http.Resilience, Microsoft.Extensions.Options, Microsoft.Extensions.Options.ConfigurationExtensions, Microsoft.Extensions.Logging.Abstractions, xUnit, xunit.runner.visualstudio, FsCheck.Xunit, NSubstitute, coverlet.collector, Microsoft.NET.Test.Sdk, and Microsoft.AspNetCore.Mvc.Testing.
2. WHEN all NuGet_Packages are updated, THE Solution SHALL build without errors using `dotnet build AiSupportWorkflow.sln`.
3. WHEN all NuGet_Packages are updated, THE Solution SHALL pass all unit tests and property-based tests using `dotnet test AiSupportWorkflow.sln`.

### Requirement 9: Verify No Paid Packages Are Used

**User Story:** As a project maintainer, I want to verify that all NuGet packages are free and open-source, so that the project can be distributed as an open-source project without licensing conflicts.

#### Acceptance Criteria

1. THE Solution SHALL use only NuGet_Packages that are free and open-source for use in an open-source project.
2. THE Clean_Architecture_Doc SHALL include a section documenting the license verification of all NuGet_Packages, confirming that each package is free to use in an open-source context.
3. IF a NuGet package requires a paid license for the project's use case, THEN THE Solution SHALL replace that package with a free alternative or remove the dependency.

### Requirement 10: Add MIT License

**User Story:** As a project maintainer, I want the repository to include an MIT License, so that the project is clearly marked as free to use, copy, modify, and distribute.

#### Acceptance Criteria

1. THE LICENSE_File SHALL be placed at the repository root and contain the full text of the MIT License.
2. THE LICENSE_File SHALL include the current year and the project author's name in the copyright notice.
3. THE README SHALL reference the LICENSE_File and state that the project is licensed under the MIT License.

### Requirement 11: GitHub Actions CI Pipeline for Feature-to-Dev Merges

**User Story:** As a developer, I want a CI pipeline that automatically builds and tests the project when a pull request targets the dev branch, so that broken code cannot be merged into the integration branch.

#### Acceptance Criteria

1. THE CI_Pipeline SHALL be defined as a GitHub Actions workflow file placed in `.github/workflows/`.
2. WHEN a pull request targets the Dev_Branch, THE CI_Pipeline SHALL trigger automatically.
3. THE CI_Pipeline SHALL restore NuGet packages, build the Solution, and run all tests as sequential steps.
4. IF the build fails or any test fails, THEN THE CI_Pipeline SHALL report a failing status that blocks the pull request from merging.
5. THE CI_Pipeline SHALL use the .NET 10.0 SDK matching the project's target framework.

### Requirement 12: Branch Naming Convention Enforcement

**User Story:** As a project maintainer, I want to enforce that all feature branches follow the `feature/{branch-name}` naming pattern, so that the repository maintains a consistent and predictable branch structure.

#### Acceptance Criteria

1. THE CI_Pipeline or a separate GitHub Actions workflow SHALL validate that the source branch of a pull request targeting Dev_Branch follows the `feature/{branch-name}` naming pattern.
2. WHEN a pull request source branch does not match the `feature/{branch-name}` pattern, THE workflow SHALL report a failing status that blocks the pull request from merging.
3. THE `{branch-name}` portion SHALL allow lowercase alphanumeric characters and hyphens.
