import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ApprovalsPage } from '../pages/ApprovalsPage';
import { RunsPage } from '../pages/RunsPage';
import { parsePayload } from '../pages/InboxPage';
import { RUN_STATUSES, type HumanInput, type RunSummary } from '../types';
import { run, workflow } from './fixtures';

const inputs: HumanInput[] = [
  {
    runId: 'run-aaaaaaaa-1111',
    moduleId: 'support',
    workflowId: 'support-email',
    stepId: 'approval',
    runTitle: 'NullReferenceException in OrderController',
    review: {
      title: 'Proposed fix: OrderController.cs',
      fields: [
        { label: 'Root cause', value: 'order.Items is null' },
        { label: 'Severity', value: 'High' },
        { label: 'Escalation', value: '' },
      ],
    },
    requestedAt: '2024-01-01T00:00:00Z',
  },
];

const mockRemove = vi.fn();
const mockSetError = vi.fn();
vi.mock('../hooks/useHumanInputs', () => ({
  useHumanInputs: () => ({ inputs, isLoading: false, error: null, setError: mockSetError, remove: mockRemove, reload: vi.fn() }),
}));

const runs: RunSummary[] = [
  run({ id: 'awaiting0000000', status: 'AwaitingInput', currentStepId: 'approval', detail: 'Initialize Items' }),
  run({ id: 'failed000000000', status: 'Failed', currentStepId: 'route_team', detail: 'Ambiguous routing' }),
  run({ id: 'done00000000000', status: 'Completed', currentStepId: 'code_change_generated', detail: '[Demo] Fix' }),
];
vi.mock('../hooks/useRuns', () => ({
  useRuns: () => ({ runs, isLoading: false, error: null }),
}));

vi.mock('../hooks/WorkflowCatalogContext', () => ({
  useWorkflowCatalog: () => ({
    modules: [{ id: 'support', name: 'Support', status: 'Available', errors: [], workflows: ['support-email'], description: null, version: '1', lastLoadedAt: null, lastError: null }],
    workflows: [workflow],
    isLoading: false,
    error: null,
    selection: { moduleId: 'support', workflowId: 'support-email' },
    selectedModule: { id: 'support', name: 'Support', status: 'Available', errors: [], workflows: ['support-email'], description: null, version: '1', lastLoadedAt: null, lastError: null },
    selectedWorkflow: workflow,
    select: vi.fn(),
    stepName: (_w: string, stepId: string | null) => workflow.graph.nodes.find((n) => n.id === stepId)?.name ?? stepId ?? '—',
    workflowName: () => 'Support email',
    refresh: vi.fn(),
  }),
}));

vi.mock('../api/client', () => ({
  approveRun: vi.fn().mockResolvedValue(undefined),
  rejectRun: vi.fn().mockResolvedValue(undefined),
  abortRun: vi.fn().mockResolvedValue(undefined),
}));

import { abortRun, approveRun, rejectRun } from '../api/client';

describe('ApprovalsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the generic review payload with run context', () => {
    render(<ApprovalsPage />);

    const card = screen.getByRole('article', { name: 'Proposed fix: OrderController.cs' });
    expect(within(card).getByText('Root cause')).toBeInTheDocument();
    expect(within(card).getByText('order.Items is null')).toBeInTheDocument();
    expect(within(card).getByText('High')).toBeInTheDocument();
    expect(within(card).getByText('Human approval')).toBeInTheDocument();
    expect(within(card).getByText('NullReferenceException in OrderController')).toBeInTheDocument();
    // Empty values render as a dash
    expect(within(card).getByText('—')).toBeInTheDocument();
  });

  it('approves a run and removes it from the list', async () => {
    render(<ApprovalsPage />);

    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(approveRun).toHaveBeenCalledWith('run-aaaaaaaa-1111'));
    expect(mockRemove).toHaveBeenCalledWith('run-aaaaaaaa-1111');
  });

  it('rejects with the reason entered by the user', async () => {
    vi.spyOn(window, 'prompt').mockReturnValue('Too risky');
    render(<ApprovalsPage />);

    fireEvent.click(screen.getByRole('button', { name: 'Reject' }));

    await waitFor(() => expect(rejectRun).toHaveBeenCalledWith('run-aaaaaaaa-1111', 'Too risky'));
  });

  it('aborts only after confirmation', async () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    render(<ApprovalsPage />);

    fireEvent.click(screen.getByRole('button', { name: 'Abort' }));
    expect(abortRun).not.toHaveBeenCalled();

    confirm.mockReturnValue(true);
    fireEvent.click(screen.getByRole('button', { name: 'Abort' }));
    await waitFor(() => expect(abortRun).toHaveBeenCalledWith('run-aaaaaaaa-1111'));
  });

  it('reports a failed action', async () => {
    vi.mocked(approveRun).mockRejectedValueOnce({ statusCode: 409, message: 'Conflict' });
    render(<ApprovalsPage />);

    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(mockSetError).toHaveBeenCalledWith('Failed to approve run run-aaaaaaaa-1111'));
    expect(mockRemove).not.toHaveBeenCalled();
  });
});

describe('RunsPage', () => {
  const renderPage = () => render(<MemoryRouter><RunsPage /></MemoryRouter>);

  it('offers every run status in the filter, including AwaitingInput', () => {
    renderPage();

    const options = within(screen.getByLabelText('Filter by status')).getAllByRole('option').map((o) => o.getAttribute('value'));
    expect(options).toEqual(['All', ...RUN_STATUSES]);
  });

  it('offers the steps of the selected workflow, with their names', () => {
    renderPage();

    const options = within(screen.getByLabelText('Filter by step')).getAllByRole('option').map((o) => o.textContent);
    expect(options).toEqual(['All Steps', ...workflow.graph.nodes.map((n) => n.name)]);
  });

  it('filters runs by status', () => {
    renderPage();

    fireEvent.change(screen.getByLabelText('Filter by status'), { target: { value: 'AwaitingInput' } });

    expect(screen.getByText('awaiting')).toBeInTheDocument();
    expect(screen.queryByText('failed00')).not.toBeInTheDocument();
  });

  it('filters runs by step', () => {
    renderPage();

    fireEvent.change(screen.getByLabelText('Filter by step'), { target: { value: 'route_team' } });

    expect(screen.getByText('failed00')).toBeInTheDocument();
    expect(screen.queryByText('awaiting')).not.toBeInTheDocument();
    expect(screen.getAllByText('Team routing').length).toBeGreaterThan(0);
  });
});

describe('InboxPage payload', () => {
  it('lists the input fields of any workflow', () => {
    expect(parsePayload('{"subject":"Hi","count":3,"tags":["a"]}')).toEqual([
      ['subject', 'Hi'],
      ['count', '3'],
      ['tags', '["a"]'],
    ]);
  });

  it('returns null for invalid or non-object payloads', () => {
    expect(parsePayload(null)).toBeNull();
    expect(parsePayload('not json')).toBeNull();
    expect(parsePayload('[1,2]')).toBeNull();
  });
});
