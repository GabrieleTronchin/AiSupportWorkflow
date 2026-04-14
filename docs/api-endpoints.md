# API Endpoints

> **📚 Navigation:** [← Back to README](../README.md)

All endpoints are served under the `/api/support` base path.

## POST /api/support/emails

Submits a support email to the AI workflow pipeline. The email is validated, classified by an LLM, routed to the appropriate team and agent, and processed through root cause analysis and code fix generation.

**Request body:**

```json
{
  "Sender": "dev.team@example.com",
  "Subject": "NullReferenceException in GetOrderSummary endpoint",
  "Body": "We have a critical bug in Application A. The GetOrderSummary endpoint throws a NullReferenceException when order.Items is null."
}
```

All three fields are strings. `Subject` and `Body` must be non-empty.

**Success response** (`200 OK`):

```json
{
  "issueId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "isSuccess": true,
  "pullRequest": {
    "id": "...",
    "issueId": "...",
    "title": "...",
    "description": "...",
    "affectedFilePaths": ["..."],
    "simulatedDiff": "..."
  },
  "isOutOfScope": false,
  "failureReason": null
}
```

For out-of-scope emails, `isSuccess` is `true`, `isOutOfScope` is `true`, and `pullRequest` is `null`.

**Error response** (`400 Bad Request`) — missing fields:

```json
{
  "error": "Subject and Body are required."
}
```

**Error response** (`400 Bad Request`) — processing failure (e.g., routing failure):

```json
{
  "failureReason": "Routing failed: no matching application found."
}
```

## GET /api/support/issues/{id:guid}

Returns the current workflow state for a specific issue.

**Parameters:**

| Name | Type | Location | Description |
|------|------|----------|-------------|
| `id` | `GUID` | Path | The issue ID returned when the email was submitted |

**Response** (`200 OK`):

```json
{
  "issueId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "stage": "CodeChangeGenerated",
  "lastUpdated": "2025-01-15T10:30:00+00:00",
  "detail": "Pull request generated.",
  "isTerminal": true
}
```

The `stage` field is one of: `Received`, `Classified`, `ClassifiedOutOfScope`, `TeamAssigned`, `AgentAssigned`, `Resolving`, `Resolved`, `CodeChangeGenerated`, `Failed`, `ManualReviewRequired`.

## GET /api/support/issues

Returns the list of all processed issues and their current workflow states.

**Response** (`200 OK`):

```json
[
  {
    "issueId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "stage": "CodeChangeGenerated",
    "lastUpdated": "2025-01-15T10:30:00+00:00",
    "detail": "Pull request generated.",
    "isTerminal": true
  }
]
```

## GET /api/support/stream

Opens a Server-Sent Events (SSE) stream that pushes real-time workflow state updates every second. The response uses `text/event-stream` content type with `no-cache` and `keep-alive` headers.

> **Requires visualization to be enabled.** Set `Workflow:EnableVisualization` to `true` in `appsettings.json`. Returns `404 Not Found` when disabled.

**Response** (SSE stream):

```
data: [{"issueId":"...","stage":"Resolving","lastUpdated":"...","detail":"...","isTerminal":false}]

data: [{"issueId":"...","stage":"Resolved","lastUpdated":"...","detail":"...","isTerminal":false}]
```

## GET /api/support/agents

Returns the current state of all AI agents managed by the supervisor actor.

> **Requires visualization to be enabled.** Set `Workflow:EnableVisualization` to `true` in `appsettings.json`. Returns `404 Not Found` when disabled.

**Response** (`200 OK`):

```json
[
  {
    "agentId": "TeamA_BackendDeveloper",
    "status": "Idle",
    "lastAction": null
  }
]
```
