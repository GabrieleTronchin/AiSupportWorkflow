# Expressions and templates

[← README](../README.md) · [Workflow reference](workflow-reference.md)

Workflow definitions use [Scriban](https://github.com/scriban/scriban) for templates and conditions. Everything is parsed when the module is loaded: an invalid template or expression, or a reference to an unknown `steps.<id>`, makes the module invalid before any run starts.

## Templates

Any string containing `{{ ... }}` is a template (prompts, `detail`, `message`, `title`, tool and HTTP inputs, review fields, demo outputs, rule outputs).

| Form | Result |
|------|--------|
| `"plain text"` | Literal string, not evaluated. |
| `"Summary: {{ steps.summarize.summary }}"` | Rendered text. |
| `"{{ steps.summarize }}"` | Exactly one expression: the **typed** value (object, array, number, boolean, null). |

Typed results let you pass whole objects around:

```yaml
output: "{{ steps.summarize }}"            # the object, not its JSON text
input:
  words: "{{ steps.count_words.words }}"   # a number
```

Objects and arrays in YAML are evaluated recursively, so each string inside them follows the same rules.

## Conditions

`when:` in transitions and rules is a Scriban expression **without** braces. `null`, `false`, `0` and the empty string are false; everything else is true.

```yaml
when: steps.review.decision == "approved"
when: steps.check.score >= 0.5 && input.priority != "low"
when: input.text | string.contains "urgent"
when: steps.lookup.owner == null
```

## Data available

| Variable | Content |
|----------|---------|
| `input` | The run input (validated against the workflow input schema). |
| `steps.<id>` | Output of a completed step. Missing members are `null`. |
| `config` | The `config` section of `module.yaml`. |
| `run` | `{ id, moduleId, workflowId, title, agentId, agent }`; `agent` is the run's assigned agent (`id`, `name`, `group`, `role`, `persona`) or `null`. |
| `agent` | Only in `llm` prompts: the agent of this call (the step's `agent`, else the run's assigned agent). |
| `output` | Only in `map` steps: the outputs computed so far. |

The run `title` template is rendered at intake, when only `input`, `config` and `run.id/moduleId/workflowId` exist.

## Built-in functions

The standard Scriban built-ins are available, for example:

| Need | Example |
|------|---------|
| Text | `input.text \| string.downcase`, `string.upcase`, `string.truncate 60`, `string.contains "x"`, `string.starts_with "x"`, `string.replace "a" "b"` |
| Regex | `input.text \| regex.match '\d+'`, `regex.replace` |
| Defaults | `steps.review.reason \| object.default 'Declined'` |
| Numbers | `steps.check.score * 100 \| math.round` |
| Arrays | `steps.enrich.tags \| array.size`, `array.join ", "` |
| Control flow (templates) | `{{ if steps.check.ok }}yes{{ else }}no{{ end }}` |
| Variables (templates) | `{{ t = input.text \| string.downcase }}{{ if t \| string.contains 'x' }}…{{ end }}` |

Inside a YAML string, `|` is the Scriban pipe; quote the whole value (`"..."` or `'...'`).

## Sandbox

Templates only see JSON data, never .NET objects, and run with limits:

- No template loader: `include` and `import` of files fail.
- Loop limit 1000, recursion limit 50, output limit 200 000 characters, regex timeout 1 s.
- Relaxed member access: a missing member is `null` instead of an error.

There is no file, network or process access. A runtime error fails the step with `Template error in step '<id>': …`.
