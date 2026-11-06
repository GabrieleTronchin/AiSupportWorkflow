import { describe, it, expect, vi } from 'vitest';
import * as fc from 'fast-check';
import { render, screen, fireEvent } from '@testing-library/react';
import { capEvents } from '../components/EventLog';
import { RunSubmitForm } from '../components/RunSubmitForm';
import { getNodeStyle } from '../components/PipelineVisualizer';
import { renderAgentActivity } from '../components/AgentMonitor';
import { AgentsPage } from '../pages/AgentsPage';
import { useAgents } from '../hooks/useAgents';
import { mergeRuns } from '../utils/runs';
import { buildFields, validateValues } from '../utils/schemaForm';
import { computeNodeStates, layoutGraph } from '../utils/graphLayout';
import { RUN_STATUSES, type RunEvent, type RunStatus, type RunSummary, type RunUpdate } from '../types';
import { agent, graph, mainPath, workflow } from './fixtures';

vi.mock('../hooks/useRunSubmit', () => ({
  useRunSubmit: () => ({
    submit: vi.fn().mockResolvedValue(undefined),
    isSubmitting: false,
    isSuccess: false,
    error: null,
    accepted: null,
    reset: vi.fn(),
  }),
}));

vi.mock('../hooks/useAgents', () => ({
  useAgents: vi.fn(),
}));

const arbStatus = fc.constantFrom<RunStatus>(...RUN_STATUSES);
const arbIso = fc.date({ noInvalidDate: true, min: new Date('2020-01-01'), max: new Date('2030-01-01') }).map((d) => d.toISOString());

const arbRun = fc.record<RunSummary>({
  id: fc.uuid(),
  moduleId: fc.constant('support'),
  workflowId: fc.constant('support-email'),
  workflowVersion: fc.constant(1),
  title: fc.option(fc.string({ maxLength: 50 }), { nil: null }),
  status: arbStatus,
  currentStepId: fc.option(fc.constantFrom(...mainPath), { nil: null }),
  detail: fc.option(fc.string({ minLength: 1, maxLength: 100 }), { nil: null }),
  assignedAgentId: fc.constant(null),
  createdAt: arbIso,
  lastUpdated: arbIso,
});

const arbUpdate = fc.record<RunUpdate>({
  runId: fc.uuid(),
  moduleId: fc.constant('support'),
  workflowId: fc.constant('support-email'),
  stepId: fc.option(fc.constantFrom(...mainPath), { nil: null }),
  status: arbStatus,
  eventKind: fc.constant('StepStarted'),
  timestamp: arbIso,
  detail: fc.option(fc.string({ minLength: 1, maxLength: 100 }), { nil: null }),
  title: fc.option(fc.string({ maxLength: 50 }), { nil: null }),
});

const arbEvent = fc.record<RunEvent>({
  id: fc.uuid(),
  runId: fc.uuid(),
  moduleId: fc.constant('support'),
  workflowId: fc.constant('support-email'),
  kind: fc.constantFrom('Queued', 'StepStarted', 'StepCompleted', 'Completed', 'Failed'),
  previousStepId: fc.option(fc.constantFrom(...mainPath), { nil: null }),
  stepId: fc.option(fc.constantFrom(...mainPath), { nil: null }),
  status: arbStatus,
  timestamp: arbIso,
  detail: fc.option(fc.string({ minLength: 1, maxLength: 100 }), { nil: null }),
});

describe('Property: Event Log cap invariant', () => {
  it('for any events array with length > 200, capEvents returns the first (most recent) 200', () => {
    fc.assert(
      fc.property(fc.array(arbEvent, { minLength: 201, maxLength: 400 }), (events) => {
        const capped = capEvents(events);
        expect(capped).toHaveLength(200);
        expect(capped).toEqual(events.slice(0, 200));
      }),
    );
  });
});

describe('Property: Run merge', () => {
  it('merging the same updates twice produces the same state as merging once', () => {
    fc.assert(
      fc.property(fc.array(arbRun, { maxLength: 30 }), fc.array(arbUpdate, { minLength: 1, maxLength: 30 }), (existing, updates) => {
        const once = mergeRuns(existing, updates);
        expect(mergeRuns(once, updates)).toEqual(once);
      }),
    );
  });

  it('every updated run ends with the latest update for its id', () => {
    fc.assert(
      fc.property(fc.uuid(), fc.array(arbUpdate, { minLength: 1, maxLength: 20 }), (runId, raw) => {
        const updates = raw.map((u, i) => ({ ...u, runId, timestamp: new Date(Date.UTC(2024, 0, 1, 0, 0, i)).toISOString() }));
        const [merged] = mergeRuns([], updates);
        expect(merged.status).toBe(updates[updates.length - 1].status);
        expect(merged.lastUpdated).toBe(updates[updates.length - 1].timestamp);
      }),
    );
  });
});

describe('Property: Input validation completeness', () => {
  const fields = buildFields(workflow.inputSchema);

  it('allows submission iff every required field has a non-blank value', () => {
    fc.assert(
      fc.property(fc.string(), fc.string(), fc.string(), (sender, subject, body) => {
        const errors = validateValues(fields, { sender, subject, body });
        const shouldAllow = subject.trim().length > 0 && body.trim().length > 0;
        expect(Object.keys(errors).length === 0).toBe(shouldAllow);
        expect('sender' in errors).toBe(false);
      }),
    );
  });
});

describe('Property: Template selection fills all form fields', () => {
  const valid = workflow.samples.filter((s) => !s.expectInvalid);

  it('selecting any sample fills every field with the sample input', () => {
    fc.assert(
      fc.property(fc.constantFrom(...valid), (sample) => {
        const { unmount } = render(<RunSubmitForm workflow={workflow} />);

        fireEvent.change(screen.getByLabelText('Template'), { target: { value: sample.id } });

        expect((screen.getByLabelText('Sender') as HTMLInputElement).value).toBe(sample.input!.sender);
        expect((screen.getByLabelText(/^Subject/) as HTMLInputElement).value).toBe(sample.input!.subject);
        expect((screen.getByLabelText(/^Body/) as HTMLTextAreaElement).value).toBe(sample.input!.body);

        unmount();
      }),
      { numRuns: 30 },
    );
  });

  it('user modifications persist after template selection', () => {
    fc.assert(
      fc.property(fc.constantFrom(...valid), fc.string({ minLength: 1 }), fc.constantFrom('Sender', 'Subject', 'Body'), (sample, value, label) => {
        const { unmount } = render(<RunSubmitForm workflow={workflow} />);
        fireEvent.change(screen.getByLabelText('Template'), { target: { value: sample.id } });

        const input = screen.getByLabelText(new RegExp(`^${label}`)) as HTMLInputElement | HTMLTextAreaElement;
        fireEvent.change(input, { target: { value } });

        expect(input.value).toBe(value);
        unmount();
      }),
      { numRuns: 30 },
    );
  });
});

describe('Property: Pipeline node states follow the run position', () => {
  it('steps before the current step are completed, the current step is active, later steps are idle', () => {
    fc.assert(
      fc.property(fc.integer({ min: 0, max: mainPath.length - 2 }), (index) => {
        const current = mainPath[index];
        const states = computeNodeStates(graph, [{ currentStepId: current, status: 'Running' }]);

        mainPath.forEach((step, i) => {
          const expected = i < index ? 'completed' : i === index ? 'active' : 'idle';
          expect(states.get(step)).toBe(expected);
        });
      }),
    );
  });

  it('a run awaiting input highlights its step in amber', () => {
    const states = computeNodeStates(graph, [{ currentStepId: 'approval', status: 'AwaitingInput' }]);

    expect(states.get('approval')).toBe('awaiting');
    expect(getNodeStyle('awaiting', null).background).toBe('#f59e0b');
    expect(String(getNodeStyle('awaiting', null).boxShadow)).toContain('rgba(245, 158, 11');
  });

  it('every active run at a distinct step gets an active indicator', () => {
    fc.assert(
      fc.property(fc.uniqueArray(fc.constantFrom(...mainPath.slice(0, -1)), { minLength: 1, maxLength: 5 }), (steps) => {
        const states = computeNodeStates(graph, steps.map((s) => ({ currentStepId: s, status: 'Running' as RunStatus })));
        // The furthest step of each run is active unless another run already passed it.
        const furthest = Math.max(...steps.map((s) => mainPath.indexOf(s)));
        expect(states.get(mainPath[furthest])).toBe('active');
        expect(getNodeStyle('active', null).background).toBe('#3b82f6');
        expect(String(getNodeStyle('active', null).boxShadow)).toContain('rgba(59, 130, 246');
      }),
    );
  });

  it('nodes with no runs are neutral gray', () => {
    const states = computeNodeStates(graph, []);

    for (const node of graph.nodes) {
      expect(states.get(node.id)).toBe('idle');
    }
    expect(getNodeStyle('idle', null).background).toBe('#3f3f46');
  });
});

describe('Property: Automatic layout', () => {
  it('places every node, left to right along the main path, without overlaps', () => {
    const positions = new Map(layoutGraph(graph).map((p) => [p.id, p]));

    expect(positions.size).toBe(graph.nodes.length);
    for (let i = 1; i < mainPath.length; i++) {
      expect(positions.get(mainPath[i])!.x).toBeGreaterThan(positions.get(mainPath[i - 1])!.x);
    }
    const keys = [...positions.values()].map((p) => `${Math.round(p.x)},${Math.round(p.y)}`);
    expect(new Set(keys).size).toBe(keys.length);
  });

  it('lays out any chain of steps', () => {
    fc.assert(
      fc.property(fc.integer({ min: 1, max: 15 }), (length) => {
        const ids = Array.from({ length }, (_, i) => `s${i}`);
        const chain = {
          start: 's0',
          nodes: ids.map((id) => ({ id, name: id, kind: 'map', outcome: null })),
          edges: ids.slice(1).map((id, i) => ({ from: ids[i], to: id, label: null })),
        };
        const xs = layoutGraph(chain).map((p) => p.x);
        expect(xs).toEqual([...xs].sort((a, b) => a - b));
      }),
      { numRuns: 20 },
    );
  });
});

describe('Property: Error display contains HTTP status code and message', () => {
  it('for any HTTP error status code (400-599) and non-empty message, both appear in the rendered output', () => {
    fc.assert(
      fc.property(
        fc.integer({ min: 400, max: 599 }),
        fc.string({ minLength: 1, maxLength: 100 }).filter((s) => s.trim().length > 0),
        (statusCode, message) => {
          vi.mocked(useAgents).mockReturnValue({ agents: [], isLoading: false, error: { statusCode, message }, retry: vi.fn() });

          const { container, unmount } = render(<AgentsPage />);

          expect(container.textContent).toContain(String(statusCode));
          expect(container.textContent).toContain(message);
          unmount();
        },
      ),
      { numRuns: 50 },
    );
  });
});

describe('Property: Working agent card displays the current run', () => {
  it('renderAgentActivity contains run id, title and step', () => {
    fc.assert(
      fc.property(fc.uuid(), fc.string({ minLength: 1, maxLength: 100 }), fc.constantFrom(...mainPath), (runId, title, step) => {
        const result = renderAgentActivity(agent({ status: 'Working', currentRunId: runId, currentRunTitle: title, currentStepId: step }));

        expect(result).toContain(runId);
        expect(result).toContain(title);
        expect(result).toContain(step);
      }),
    );
  });
});
