# Human input

[← README](../README.md) · [Workflow reference](workflow-reference.md#human-input) · [Engine API](engine-api.md#human-input)

A `human-input` step pauses a run until someone approves or rejects it. The engine handles it generically, so every workflow uses the same endpoints and the same dashboard page.

## In the workflow

```yaml
- id: review
  kind: human-input
  name: Review
  review:
    title: "Publish summary of {{ run.title }}?"
    fields:
      - { label: Summary, value: "{{ steps.summarize.summary }}" }
      - { label: Words, value: "{{ steps.count_words.words }}" }
  detail: Waiting for review           # optional, detail of the AwaitingInput event
  transitions:
    - when: steps.review.decision == "approved"
      to: publish
    - to: declined
```

The step:

1. Renders the review payload (title and ordered fields) from the templates.
2. Records an `AwaitingInput` transition; the run status becomes `AwaitingInput`.
3. Waits for a decision, then records `InputReceived` (detail `approved` or `rejected: <reason>`).
4. Stores `{ decision: "approved" | "rejected", reason: string | null }` as `steps.<id>` and follows its transitions.

What happens after a rejection is up to the workflow: typically a `failure` terminal step whose message uses the reason, e.g. `"{{ steps.review.reason | object.default 'Declined' }}"`.

## API

| Method | Route | Result |
|--------|-------|--------|
| GET | `/api/engine/human-inputs?moduleId=&workflowId=` | Pending inputs with `runId`, `stepId`, `runTitle`, `review` and `requestedAt` |
| POST | `/api/engine/human-inputs/{runId}/approve` | `204` |
| POST | `/api/engine/human-inputs/{runId}/reject` | `204`; body `{ "reason": "..." }` optional |
| POST | `/api/engine/runs/{runId}/abort` | `202`; the run ends in `Failed` with `Aborted by user` |

`404` means the run does not exist; `409` means it is not awaiting input or has already completed.

## In the dashboard

The **Approvals** page lists the pending inputs of every module with their review fields, workflow and step, and offers **Approve**, **Reject** (with an optional reason) and **Abort**. The list refreshes on stream updates and every 5 seconds.

## Notes

- Pending inputs live in engine memory (`HumanInputRegistry`), like runs and events (EF Core InMemory): restarting the engine loses them.
- Abort and engine shutdown release the wait: abort ends the run in `Failed` with a single transition, shutdown ends it in `Cancelled`.
- Runs paused on human input do not block the inbox; in sequential mode they do block the next run from starting.
