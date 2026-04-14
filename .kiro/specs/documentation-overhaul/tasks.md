# Tasks — Documentation Overhaul

## Task 1: Rewrite README.md — Core Sections

- [x] 1.1 Write the project title, tagline, and description section explaining the spec-driven AI experiment with Kiro
- [x] 1.2 Write the "What It Does" section describing the full workflow pipeline (email → classification → routing → resolution → code fix)
- [x] 1.3 Write the "Technologies" section with a table listing .NET 10, Akka.NET 1.5.64, Semantic Kernel 1.74.0, OpenAI, xUnit, FsCheck, NSubstitute — each with a link to official docs
- [x] 1.4 Write the "Getting Started" section with clone, API key configuration in `appsettings.Development.json`, and `dotnet run` command
- [x] 1.5 Write the "Project Structure" section with a folder tree showing `src/`, `tests/`, `DummyApps/`, `docs/` and brief descriptions of each layer

## Task 2: README.md — Architecture Diagrams

- [x] 2.1 Add a Mermaid diagram showing the four Clean Architecture layers (Domain, Application, Infrastructure, Presentation) with inward dependency arrows
- [x] 2.2 Add a Mermaid diagram showing the workflow pipeline flow: email reception → LLM classification → team routing → agent assignment → root cause analysis → code fix generation

## Task 3: README.md — API Endpoints Section

- [x] 3.1 Add an endpoint summary table listing all 5 endpoints (method, route, description)
- [x] 3.2 Document `POST /api/support/emails` with expected JSON body (`Sender`, `Subject`, `Body`), success/error responses, and a request example
- [x] 3.3 Document `GET /api/support/issues/{id}` with the `id` parameter (GUID), expected response, and a request example
- [x] 3.4 Document `GET /api/support/issues` specifying it returns the list of all processed issues
- [x] 3.5 Document `GET /api/support/stream` specifying SSE behavior and the visualization configuration requirement
- [x] 3.6 Document `GET /api/support/agents` specifying it returns agent states and the visualization configuration requirement
- [x] 3.7 Add an explicit reference to the HTTP file path (`src/AiSupportWorkflow.Presentation/AiSupportWorkflow.Presentation.http`) with usage instructions

## Task 4: README.md — Deep-Dive, DummyApps, and Testing Sections

- [x] 4.1 Write navigable sections for Clean Architecture, Actor Architecture, and Semantic Kernel Integration — each with a 2-3 sentence summary and a link to the corresponding `docs/` file
- [x] 4.2 Write the "DummyApps & Test Scenarios" section explaining ApplicationA/ApplicationB as test fixtures, the three bug categories (BackendBug, FrontendBug, QualityTestIssue), and references to `BugScenarios.md` files
- [x] 4.3 Mention that the HTTP file contains ready-made requests for all scenarios including edge cases (out-of-scope, ambiguous routing, failed routing, empty input)
- [x] 4.4 Write the "Testing" section describing test organization (xUnit + NSubstitute for unit, FsCheck for property), commands (`dotnet test AiSupportWorkflow.sln`, `dotnet test tests/AiSupportWorkflow.UnitTests`, `dotnet test tests/AiSupportWorkflow.PropertyTests`), and conventions (one class per service, AAA pattern, NSubstitute for mocking)

## Task 5: Create docs/index.md

- [x] 5.1 Create `docs/index.md` with a Navigation_Index at the top (link back to README)
- [x] 5.2 Add a listing of all available documents (clean-architecture.md, actor-architecture.md, semantic-kernel-integration.md) with title, one-line description, and direct links

## Task 6: Add Navigation_Index to Existing Docs

- [x] 6.1 Add a Navigation_Index block to the top of `docs/clean-architecture.md` (after the H1 title) with links to README, index.md, and the other two in-depth docs — current page shown as bold text
- [x] 6.2 Add a Navigation_Index block to the top of `docs/actor-architecture.md` with the same consistent format
- [x] 6.3 Add a Navigation_Index block to the top of `docs/semantic-kernel-integration.md` with the same consistent format

## Task 7: Documentation-Code Coherence Verification

- [x] 7.1 Verify that all documented API endpoint routes match exactly the routes defined in `SupportEmailEndpoints.cs`, `WorkflowStatusEndpoints.cs`, and `VisualizationEndpoints.cs`
- [x] 7.2 Verify that documented technology versions match the versions in the `.csproj` files (net10.0, Akka.NET 1.5.64, Semantic Kernel 1.74.0, etc.)
- [x] 7.3 Verify that the documented project folder structure matches the actual repository layout
- [x] 7.4 Verify that build and test commands (`dotnet build`, `dotnet test`, `dotnet run`) use the correct project paths
