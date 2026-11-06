import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { buildFields, emptyValues, toInput, validateValues, valuesFromInput } from '../utils/schemaForm';
import { ancestorsOf, formatEdgeLabel } from '../utils/graphLayout';
import { buildFlow } from '../components/PipelineVisualizer';
import { formatStatus, getModuleStatusBadgeClasses, getStatusBadgeClasses } from '../utils/badges';
import { formatRelativeTime, sortRuns } from '../utils/runs';
import { isTerminal, RUN_STATUSES } from '../types';
import { useRunSubmit } from '../hooks/useRunSubmit';
import { graph, run, workflow } from './fixtures';

vi.mock('../api/client', () => ({
  submitRun: vi.fn(),
}));

import { submitRun } from '../api/client';

describe('schema form', () => {
  const fields = buildFields(workflow.inputSchema);

  it('derives fields, labels and widgets from the schema', () => {
    expect(fields.map((f) => [f.name, f.label, f.widget, f.required])).toEqual([
      ['sender', 'Sender', 'text', false],
      ['subject', 'Subject', 'text', true],
      ['body', 'Body', 'textarea', true],
    ]);
  });

  it('humanizes property names without a title', () => {
    expect(buildFields({ properties: { receiptPhotoUrl: { type: 'string' }, max_items: { type: 'integer' } } }).map((f) => f.label))
      .toEqual(['Receipt Photo Url', 'Max items']);
  });

  it('handles a missing schema', () => {
    expect(buildFields(null)).toEqual([]);
  });

  it('round-trips sample inputs and converts numbers and booleans', () => {
    const custom = buildFields({
      type: 'object',
      required: ['count'],
      properties: { count: { type: 'integer' }, urgent: { type: 'boolean' }, note: { type: 'string' } },
    });

    const values = valuesFromInput(custom, { count: 3, urgent: true });

    expect(values).toEqual({ count: '3', urgent: true, note: '' });
    expect(toInput(custom, values)).toEqual({ count: 3, urgent: true });
    expect(validateValues(custom, { ...values, count: 'abc' })).toEqual({ count: 'Count must be a number' });
    expect(emptyValues(custom)).toEqual({ count: '', urgent: false, note: '' });
  });
});

describe('graph helpers', () => {
  it('formats edge labels from conditions', () => {
    expect(formatEdgeLabel('steps.approval.decision == "approved"', false)).toBe('approved');
    expect(formatEdgeLabel('steps.classify.isCodeRelated', false)).toBe('isCodeRelated');
    expect(formatEdgeLabel(null, false)).toBe('otherwise');
    expect(formatEdgeLabel(null, true)).toBeUndefined();
  });

  it('finds the ancestors of a step', () => {
    expect([...ancestorsOf(graph, 'approval')].sort()).toEqual(['assign_agent', 'classify', 'resolve', 'route_team']);
    expect(ancestorsOf(graph, 'classify').size).toBe(0);
  });

  it('builds nodes with step names and terminal outcome borders', () => {
    const { nodes, edges } = buildFlow(graph, []);

    expect(nodes.map((n) => n.data.label)).toEqual(graph.nodes.map((n) => n.name));
    expect(nodes.find((n) => n.id === 'manual_review_required')!.style!.border).toContain('#ef4444');
    expect(nodes.find((n) => n.id === 'code_change_generated')!.style!.border).toContain('#10b981');
    expect(edges.find((e) => e.target === 'generate_code')!.label).toBe('approved');
    expect(edges.find((e) => e.target === 'manual_review_required')!.style!.strokeDasharray).toBe('5,5');
  });

  it('animates the edges traversed by active runs', () => {
    const { edges } = buildFlow(graph, [{ id: 'r', currentStepId: 'assign_agent', status: 'Running' }]);

    expect(edges.find((e) => e.source === 'classify' && e.target === 'route_team')!.animated).toBe(true);
    expect(edges.find((e) => e.source === 'resolve')!.animated).toBe(false);
  });
});

describe('status helpers', () => {
  it.each([
    ['Queued', 'text-blue-400 bg-blue-400/10'],
    ['Running', 'text-blue-400 bg-blue-400/10'],
    ['AwaitingInput', 'text-amber-400 bg-amber-400/10'],
    ['Completed', 'text-emerald-400 bg-emerald-400/10'],
    ['Failed', 'text-red-400 bg-red-400/10'],
    ['Cancelled', 'text-red-400 bg-red-400/10'],
  ] as const)('%s badge', (status, classes) => {
    expect(getStatusBadgeClasses(status)).toBe(classes);
  });

  it('knows terminal statuses and labels', () => {
    expect(RUN_STATUSES.filter(isTerminal)).toEqual(['Completed', 'Failed', 'Cancelled']);
    expect(formatStatus('AwaitingInput')).toBe('Awaiting input');
    expect(formatStatus('Failed')).toBe('Failed');
    expect(getModuleStatusBadgeClasses('Unavailable')).toContain('red');
  });

  it('sorts runs newest first', () => {
    const sorted = sortRuns([run({ id: 'old', createdAt: '2024-01-01T00:00:00Z' }), run({ id: 'new', createdAt: '2024-02-01T00:00:00Z' })]);
    expect(sorted.map((r) => r.id)).toEqual(['new', 'old']);
  });

  it('formats relative times', () => {
    const now = Date.now();
    expect(formatRelativeTime(new Date(now + 5000).toISOString())).toBe('just now');
    expect(formatRelativeTime(new Date(now - 30_000).toISOString())).toBe('30 sec ago');
    expect(formatRelativeTime(new Date(now - 5 * 60_000).toISOString())).toBe('5 min ago');
    expect(formatRelativeTime(new Date(now - 60 * 60_000).toISOString())).toBe('1 hour ago');
    expect(formatRelativeTime(new Date(now - 3 * 24 * 60 * 60_000).toISOString())).toBe('3 days ago');
  });
});

describe('useRunSubmit', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('submits and exposes the accepted ids', async () => {
    vi.mocked(submitRun).mockResolvedValue({ messageId: 'm1', runId: 'r1' });
    const { result } = renderHook(() => useRunSubmit());

    await act(async () => {
      await result.current.submit('support-email', { subject: 'S' });
    });

    expect(submitRun).toHaveBeenCalledWith('support-email', { subject: 'S' });
    expect(result.current.isSuccess).toBe(true);
    expect(result.current.accepted).toEqual({ messageId: 'm1', runId: 'r1' });
    expect(result.current.isSubmitting).toBe(false);
  });

  it('exposes the API error', async () => {
    vi.mocked(submitRun).mockRejectedValue({ statusCode: 400, message: 'Subject and Body are required.' });
    const { result } = renderHook(() => useRunSubmit());

    await act(async () => {
      await result.current.submit('support-email', {});
    });

    expect(result.current.isSuccess).toBe(false);
    expect(result.current.error).toEqual({ statusCode: 400, message: 'Subject and Body are required.' });
  });

  it('reset clears the state', async () => {
    vi.mocked(submitRun).mockResolvedValue({ messageId: 'm1', runId: 'r1' });
    const { result } = renderHook(() => useRunSubmit());
    await act(async () => {
      await result.current.submit('support-email', {});
    });

    act(() => result.current.reset());

    expect(result.current.isSuccess).toBe(false);
    expect(result.current.accepted).toBeNull();
    expect(result.current.error).toBeNull();
  });
});
