# Debugging

> **📚 Navigation:** [← Back to README](../README.md)

This page covers the tools available to test and debug the workflow while the API is running locally.

## HTTP File for Testing

The project includes an HTTP file with ready-made requests for all endpoints:

```
src/AiSupportWorkflow.Presentation/AiSupportWorkflow.Presentation.http
```

Open it in Visual Studio, VS Code (with the REST Client extension), or JetBrains Rider to send requests directly from your IDE. It covers all six bug scenarios across both applications, plus edge cases (out-of-scope emails, ambiguous routing, failed routing, and empty input validation).

## PowerShell Monitor Script

The `scripts/Monitor-Workflow.ps1` script streams workflow events to the terminal in real time, useful when you don't have a frontend.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `-BaseUrl` | `string` | `http://localhost:5080` | Base URL of the running API |
| `-Agents` | `switch` | — | Query agent statuses instead of streaming events |

```powershell
# Stream workflow events
./scripts/Monitor-Workflow.ps1

# Custom URL
./scripts/Monitor-Workflow.ps1 -BaseUrl http://localhost:5000

# View agent statuses
./scripts/Monitor-Workflow.ps1 -Agents
```
