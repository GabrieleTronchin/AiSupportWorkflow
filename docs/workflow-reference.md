# Workflow and module reference

[← README](../README.md) · [Expressions](expressions.md) · [Module authoring](module-authoring.md) · [Demo mode](demo-mode.md)

A module definition is a folder of YAML, Markdown and JSON files (`modules/<id>/definition/`) published by the module service. Three YAML formats exist, each with a JSON Schema in `backend/schemas/` (also served by `GET /api/engine/schemas/{module|workflow|samples}`):

| File | Schema | Content |
|------|--------|---------|
| `module.yaml` | `module.schema.json` | Module id, config, agents, tools, list of workflow and sample files |
| `workflows/*.yaml` | `workflow.schema.json` | One workflow: input schema, steps, transitions |
| `samples/*.yaml` | `samples.schema.json` | Example inputs shown by the dashboard |

Start each file with a schema comment so editors with the YAML language server validate and autocomplete it:

```yaml
# yaml-language-server: $schema=../../../../backend/schemas/workflow.schema.json
```

The complete, runnable example is the template module: [`modules/_template/definition`](../modules/_template/definition).

## Conventions

- **Inline or file**: prompts accept a string or `{ file: prompts/x.md }`; schemas accept an inline JSON Schema or `{ file: schemas/x.schema.json }`. Paths are relative to `definition/`; paths leaving the folder are rejected.
- **Templates**: any string containing `{{ ... }}` is a Scriban template. A string that is exactly one `{{ expr }}` returns the typed value (number, boolean, object, array, null); other strings render to text. Strings without `{{` are literals. See [Expressions](expressions.md).
- **Conditions**: `when:` values are Scriban expressions without braces, e.g. `steps.check.score > 0.5`.
- **Identifiers**: module and workflow ids match `^[a-z][a-z0-9_-]*$`; step ids match `^[a-z][a-z0-9_]*$` (usable as `steps.<id>`); tool names are `<moduleId>.<name>`; agent ids contain no whitespace. Workflow ids are unique across all modules.
- JSON Schemas use draft 2020-12.

## Module manifest (`module.yaml`)

```yaml
# yaml-language-server: $schema=../../../backend/schemas/module.schema.json
id: notes
name: Notes
description: Summarises notes.
config:                       # free-form, available as config.* in templates; never secrets
  maxSummaryWords: 30
agents:                       # used for attribution, status and prompts
  - id: notes_writer
    name: Writer
    group: Writers            # optional grouping shown by the dashboard
    role: Writer              # optional
    persona: A concise technical writer.
tools:                        # operations implemented by the module service
  - name: notes.wordCount
    description: Counts the words of a text.
    input:  { schema: { file: schemas/word-count.input.schema.json } }
    output: { schema: { file: schemas/word-count.output.schema.json } }
    timeoutSeconds: 10        # optional
workflows:
  - workflows/notes-summary.yaml
samples:
  - samples/notes-summary.samples.yaml
```

Required: `id`, `name`, `workflows`. Tool names must start with the module id.

## Workflow definition

```yaml
# yaml-language-server: $schema=../../../../backend/schemas/workflow.schema.json
id: notes-summary
version: 1                    # bump when the workflow changes
name: Notes summary
description: Summarises a note and counts its words.
title: "{{ input.text | string.truncate 60 }}"   # run title shown in listings (optional)
input:
  schema: { file: schemas/notes-summary.input.schema.json }
  errorMessage: Text is required.               # detail of the 400 response (optional)
maxSteps: 100                 # optional, default 100
start: summarize
steps:
  - id: summarize
    kind: llm
    # ...
```

Required: `id`, `version`, `name`, `input.schema`, `start`, `steps`.

### Common step fields

| Field | Description |
|-------|-------------|
| `id`, `kind`, `name` | Required. `kind` is one of `llm`, `rule`, `map`, `human-input`, `tool`, `http`, `terminal`. |
| `description` | Optional text. |
| `detail` | Template rendered after the step; becomes the detail of the step-completed event and of the run. |
| `assignAgent` | Template; after the step, sets the run's assigned agent. The result must be a declared agent, otherwise the step fails. |
| `next` | Id of the next step. |
| `transitions` | Ordered list of `{ when?, to }`. The first transition whose `when` is true is followed; a transition without `when` is the default and should be last. |

Every non-terminal step declares either `next` or `transitions` (not both). When no transition matches, the step fails with `No transition matched after step '<id>'.`

```yaml
transitions:
  - when: steps.check.score >= 0.8
    to: publish
  - when: steps.check.score >= 0.5
    to: review
  - to: rejected
```

The output of each completed step is available to later steps as `steps.<id>`.

## Step kinds

### `llm`

Sends an optional system prompt and a user prompt to the engine's `IChatClient`, requests structured output matching `output.schema`, validates the response and stores it.

```yaml
- id: summarize
  kind: llm
  name: Summary
  agent: notes_writer          # optional: attribution; default is the run's assigned agent
  temperature: 0.2             # optional, 0-2
  systemPrompt: { file: prompts/summarize.system.md }
  prompt: "Summarise in at most {{ config.maxSummaryWords }} words: {{ input.text }}"
  output:
    schema:
      type: object
      additionalProperties: false
      required: [summary]
      properties:
        summary: { type: string }
  demo:                        # response used in Demo mode
    output: { summary: "[Demo] {{ input.text | string.truncate 80 }}" }
  next: count_words
```

- The output schema root must be `type: object` with `additionalProperties: false` (structured output).
- Prompts see the agent of the call as `agent` (`id`, `name`, `group`, `role`, `persona`).
- Failures: `LLM call failed for step '<id>': …`, `LLM response for step '<id>' is not valid JSON.`, `LLM response for step '<id>' does not match its schema: …`.
- In Demo mode the step needs a `demo:` block; see [Demo mode](demo-mode.md).

### `rule`

Matches a rendered value against ordered rules and outputs the matching rule's `output`.

```yaml
- id: route
  kind: rule
  name: Routing
  value: "{{ input.subject }} {{ input.body }}"
  ignoreCase: true                       # default true
  rules:
    - match: '\binvoice\b'               # regular expression, or
      output: { queue: billing }
    - when: input.priority == "high"     # expression
      output: { queue: urgent }
  onMultipleMatches: first               # default; or { fail: "message" }
  onNoMatch:                             # default: fail with "No rule matched in step '<id>'."
    output: { queue: general }           # or { fail: "message" }
  next: assign
```

Rule outputs and `fail` messages are templates.

### `map`

Computes named outputs from templates and lookup tables, in order. Earlier outputs are available as `output.<name>`.

```yaml
- id: assign
  kind: map
  name: Assignment
  lookups:
    ownerByQueue: { billing: notes_billing, urgent: notes_oncall, general: notes_writer }
  outputs:
    - name: owner
      lookup: ownerByQueue
      key: "{{ steps.route.queue }}"
      onMissing: "No owner for queue '{{ steps.route.queue }}'."   # optional failure message
    - name: label
      value: "{{ steps.route.queue }}:{{ output.owner }}"
  assignAgent: "{{ steps.assign.owner }}"
  next: review
```

### `human-input`

Publishes a review payload, pauses the run in `AwaitingInput` and continues when someone approves or rejects it. Output: `{ decision: "approved" | "rejected", reason: string | null }`. See [Human input](human-input.md).

```yaml
- id: review
  kind: human-input
  name: Review
  review:
    title: "Publish summary of {{ run.title }}?"
    fields:
      - { label: Summary, value: "{{ steps.summarize.summary }}" }
      - { label: Owner, value: "{{ steps.assign.owner }}" }
  transitions:
    - when: steps.review.decision == "approved"
      to: done
    - to: declined
```

### `tool`

Calls a tool of the owning module service (declared in `module.yaml`). `input` is an object of templates. The input and the result are validated against the tool's schemas.

```yaml
- id: count_words
  kind: tool
  name: Word count
  tool: notes.wordCount
  input:
    text: "{{ steps.summarize.summary }}"
  timeoutSeconds: 10           # optional
  next: done
```

Timeout precedence: the tool's `timeoutSeconds` in `module.yaml`, then the step's `timeoutSeconds`, then the module's `TimeoutSeconds` in the engine configuration, then `Flowgent:DefaultToolTimeoutSeconds` (30). Failures: `Tool '<name>' failed: …`, `Tool '<name>' timed out after Ns.`, `Module '<id>' is unavailable: …`.

### `http`

Calls an external HTTP API and stores the parsed JSON response.

```yaml
- id: enrich
  kind: http
  name: Enrichment
  method: POST                               # GET, POST, PUT, PATCH, DELETE
  url: "{{ config.enrichBaseUrl }}/api/enrich"
  headers:
    X-Run-Id: "{{ run.id }}"                 # literal or template
    Authorization: { secret: enrichToken }   # read from Flowgent:Secrets:<moduleId>:enrichToken
  body:
    text: "{{ steps.summarize.summary }}"
  timeoutSeconds: 30                         # default 30
  output:                                    # optional response schema
    schema: { type: object, required: [tags], properties: { tags: { type: array } } }
  next: done
```

Non-2xx status codes, non-JSON responses, timeouts and schema mismatches fail the step (`HTTP step '<id>' failed: …`); calls are not retried. Secret header values come from engine configuration and never appear in API responses.

### `terminal`

Ends the run. `outcome: success` ends it as `Completed`, `outcome: failure` as `Failed`. `message` (template) becomes the run detail; `output` (template, optional) becomes the final output.

```yaml
- id: done
  kind: terminal
  name: Done
  outcome: success
  message: "{{ steps.summarize.summary }} ({{ steps.count_words.words }} words)"
  output:
    summary: "{{ steps.summarize.summary }}"
    words: "{{ steps.count_words.words }}"
- id: declined
  kind: terminal
  name: Declined
  outcome: failure
  message: "{{ steps.review.reason | object.default 'Declined' }}"
```

Terminal steps cannot declare `next` or `transitions`.

## Samples

```yaml
# yaml-language-server: $schema=../../../../backend/schemas/samples.schema.json
samples:
  - id: notes-short
    name: Short note
    group: Examples              # optional, groups the dashboard picker
    workflow: notes-summary
    description: optional
    input: { text: "Agent Framework workflows in YAML." }
  - id: notes-empty
    name: Empty note (validation)
    workflow: notes-summary
    expectInvalid: true          # the input intentionally fails the input schema
    input: { text: "   " }
```

Samples are validated against the workflow input schema unless `expectInvalid: true`.

## Input schemas and the dashboard form

The dashboard builds the submission form from the workflow input schema: `string` → text input (`x-multiline: true` → textarea), `enum` → select, `boolean` → checkbox, `number`/`integer` → number input. `title` and `description` label the fields.

## Validation

The engine validates a module when it loads it, and the Module SDK runs the same validation at module startup and in tests (`ModuleDefinitionAssert.IsValid`). Errors carry file, step and line number and are shown by `GET /api/engine/modules` and the dashboard. An invalid module is not loaded; other modules are unaffected.

Errors:

- YAML syntax or format schema violations.
- Duplicate ids (steps, workflows, agents, tools, samples, map outputs); unknown `start` step or transition target.
- Steps not reachable from `start`; no reachable terminal step; `next` and `transitions` both declared; non-terminal step without either.
- Missing or invalid prompt/schema files; LLM output schema not a strict object.
- Agents (literal `agent` / `assignAgent`) or tools not declared in `module.yaml`; tool names without the module prefix.
- Invalid templates, expressions or regular expressions; `steps.<id>` references to unknown steps.
- Samples referencing unknown workflows or not matching the input schema.

Warnings (the module still loads): `llm` step without `demo:` (it fails in Demo mode), a transition without condition that is not the last one, a sample marked `expectInvalid` that is valid.
