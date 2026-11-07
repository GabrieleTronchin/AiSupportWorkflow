import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import {
  readStoredSelection,
  resolveSelection,
  SELECTION_STORAGE_KEY,
  WorkflowCatalogProvider,
} from '../hooks/WorkflowCatalogContext';
import { ModuleWorkflowPicker } from '../components/ModuleWorkflowPicker';
import type { ModuleInfo, WorkflowInfo } from '../types';
import { workflow } from './fixtures';

vi.mock('../api/client', () => ({
  fetchModules: vi.fn(),
  fetchWorkflows: vi.fn(),
}));

import { fetchModules, fetchWorkflows } from '../api/client';

const support: ModuleInfo = {
  id: 'support', name: 'Support', description: null, status: 'Available', version: 'v1',
  workflows: ['support-email'], errors: [], lastLoadedAt: null, lastError: null,
};
const food: ModuleInfo = {
  id: 'food', name: 'Food', description: null, status: 'Unavailable', version: 'v1',
  workflows: ['receipt'], errors: [], lastLoadedAt: null, lastError: 'No connection could be made',
};
const receipt: WorkflowInfo = { ...workflow, id: 'receipt', moduleId: 'food', name: 'Receipt', available: false };

describe('selection helpers', () => {
  it('keeps a stored selection that still exists', () => {
    expect(resolveSelection([workflow, receipt], { moduleId: 'food', workflowId: 'receipt' })).toEqual({ moduleId: 'food', workflowId: 'receipt' });
  });

  it('falls back to the first available workflow', () => {
    expect(resolveSelection([receipt, workflow], { moduleId: 'x', workflowId: 'gone' })).toEqual({ moduleId: 'support', workflowId: 'support-email' });
    expect(resolveSelection([receipt, workflow], null)).toEqual({ moduleId: 'support', workflowId: 'support-email' });
  });

  it('falls back to any workflow when none is available, and to null when there are none', () => {
    expect(resolveSelection([receipt], null)).toEqual({ moduleId: 'food', workflowId: 'receipt' });
    expect(resolveSelection([], null)).toBeNull();
  });

  it('reads only well-formed stored selections', () => {
    const storage = (value: string | null) => ({ getItem: () => value });
    expect(readStoredSelection(storage('{"moduleId":"a","workflowId":"b"}'))).toEqual({ moduleId: 'a', workflowId: 'b' });
    expect(readStoredSelection(storage('{"moduleId":1}'))).toBeNull();
    expect(readStoredSelection(storage('not json'))).toBeNull();
    expect(readStoredSelection(storage(null))).toBeNull();
  });
});

describe('ModuleWorkflowPicker', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.localStorage.clear();
    vi.mocked(fetchModules).mockResolvedValue([support, food]);
    vi.mocked(fetchWorkflows).mockResolvedValue([workflow, receipt]);
  });

  const renderPicker = () => render(<WorkflowCatalogProvider><ModuleWorkflowPicker /></WorkflowCatalogProvider>);

  it('selects the first available workflow and shows module availability', async () => {
    renderPicker();

    await waitFor(() => expect(screen.getByLabelText('Workflow')).toHaveValue('support-email'));
    expect(screen.getByLabelText('Module')).toHaveValue('support');
    expect(screen.getByText('Available')).toBeInTheDocument();
    expect(screen.getByText(/Module Food: Unavailable — No connection could be made/)).toBeInTheDocument();
  });

  it('remembers the selection across reloads', async () => {
    const { unmount } = renderPicker();
    await waitFor(() => expect(screen.getByLabelText('Module')).toHaveValue('support'));

    fireEvent.change(screen.getByLabelText('Module'), { target: { value: 'food' } });
    await waitFor(() => expect(screen.getByLabelText('Workflow')).toHaveValue('receipt'));
    expect(JSON.parse(window.localStorage.getItem(SELECTION_STORAGE_KEY)!)).toEqual({ moduleId: 'food', workflowId: 'receipt' });
    unmount();

    renderPicker();
    await waitFor(() => expect(screen.getByLabelText('Workflow')).toHaveValue('receipt'));
  });

  it('lists the validation errors of the selected module', async () => {
    vi.mocked(fetchModules).mockResolvedValue([{
      ...support,
      status: 'Invalid',
      errors: [
        { moduleId: 'support', file: 'workflows/support-email.yaml', stepId: 'route_team', line: 42, message: "Transition target 'ghost' does not exist.", severity: 'Error' },
        { moduleId: 'support', file: 'workflows/support-email.yaml', stepId: 'classify', line: 10, message: 'No demo response', severity: 'Warning' },
      ],
    }]);
    vi.mocked(fetchWorkflows).mockResolvedValue([workflow]);

    renderPicker();

    const errors = await screen.findByRole('list', { name: 'Validation errors' });
    expect(errors).toHaveTextContent("workflows/support-email.yaml:42 [route_team] Transition target 'ghost' does not exist.");
    expect(errors).not.toHaveTextContent('No demo response');
  });

  it('reports an unreachable engine', async () => {
    vi.mocked(fetchModules).mockRejectedValue({ statusCode: 503, message: "The 'engine' service is unavailable." });

    renderPicker();

    expect(await screen.findByRole('alert')).toHaveTextContent("The 'engine' service is unavailable.");
  });
});
