import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import type { ApiError, RunUpdate } from '../types';
import { useRuns } from '../hooks/useRuns';
import { mergeRuns } from '../utils/runs';
import { run, update } from './fixtures';

vi.mock('../api/client', () => ({
  fetchRuns: vi.fn(),
}));

vi.mock('../hooks/useGrpcStream', () => ({
  useGrpcStream: vi.fn(),
}));

import { fetchRuns } from '../api/client';
import { useGrpcStream } from '../hooks/useGrpcStream';

const mockFetchRuns = vi.mocked(fetchRuns);
const mockUseGrpcStream = vi.mocked(useGrpcStream);

const runA = run({ id: 'run-a', createdAt: '2024-01-01T00:00:00Z', lastUpdated: '2024-01-01T00:00:00Z' });
const runB = run({ id: 'run-b', workflowId: 'other', createdAt: '2024-01-02T00:00:00Z', lastUpdated: '2024-01-02T00:00:00Z', currentStepId: 'route_team' });

function stream(lastUpdate: RunUpdate | null) {
  mockUseGrpcStream.mockReturnValue({ latestRuns: [], lastUpdate, isConnected: true });
}

describe('useRuns', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    stream(null);
    mockFetchRuns.mockResolvedValue([runA, runB]);
  });

  it('is loading with no runs before the fetch completes', () => {
    mockFetchRuns.mockReturnValue(new Promise(() => {}));

    const { result } = renderHook(() => useRuns());

    expect(result.current.runs).toEqual([]);
    expect(result.current.isLoading).toBe(true);
  });

  it('fetches runs on mount, newest first', async () => {
    const { result } = renderHook(() => useRuns());

    await waitFor(() => expect(result.current.runs.map((r) => r.id)).toEqual(['run-b', 'run-a']));
    expect(result.current.isLoading).toBe(false);
  });

  it('filters by workflow', async () => {
    const { result } = renderHook(() => useRuns('support-email'));

    await waitFor(() => expect(result.current.runs.map((r) => r.id)).toEqual(['run-a']));
  });

  it('applies stream updates to known runs', async () => {
    const { result, rerender } = renderHook(() => useRuns());
    await waitFor(() => expect(result.current.runs).toHaveLength(2));

    stream(update({ runId: 'run-a', stepId: 'approval', status: 'AwaitingInput', detail: 'Waiting', timestamp: '2024-01-05T00:00:00Z' }));
    rerender();

    await waitFor(() => {
      const found = result.current.runs.find((r) => r.id === 'run-a')!;
      expect(found.status).toBe('AwaitingInput');
      expect(found.currentStepId).toBe('approval');
      expect(found.detail).toBe('Waiting');
      expect(found.title).toBe(runA.title);
    });
  });

  it('adds runs that were not in the initial fetch', async () => {
    const { result, rerender } = renderHook(() => useRuns());
    await waitFor(() => expect(result.current.runs).toHaveLength(2));

    stream(update({ runId: 'run-new', eventKind: 'Queued', status: 'Queued', stepId: null, title: 'New one', timestamp: '2024-01-10T00:00:00Z' }));
    rerender();

    await waitFor(() => {
      expect(result.current.runs).toHaveLength(3);
      expect(result.current.runs[0]).toMatchObject({ id: 'run-new', title: 'New one', status: 'Queued' });
    });
  });

  it('exposes fetch errors and stops loading', async () => {
    const apiError: ApiError = { statusCode: 500, message: 'Server error' };
    mockFetchRuns.mockRejectedValue(apiError);

    const { result } = renderHook(() => useRuns());

    await waitFor(() => expect(result.current.error).toEqual(apiError));
    expect(result.current.isLoading).toBe(false);
  });
});

describe('mergeRuns', () => {
  it('upserts by run id and keeps runs without updates', () => {
    const result = mergeRuns([runA, runB], [
      update({ runId: 'run-a', status: 'Completed', stepId: 'code_change_generated', timestamp: '2024-01-05T00:00:00Z' }),
      update({ runId: 'run-c', status: 'Queued', stepId: null, timestamp: '2024-01-03T00:00:00Z' }),
    ]);

    expect(result).toHaveLength(3);
    expect(result.find((r) => r.id === 'run-a')?.status).toBe('Completed');
    expect(result.find((r) => r.id === 'run-b')).toEqual(runB);
    expect(result.find((r) => r.id === 'run-c')?.status).toBe('Queued');
  });

  it('ignores updates older than the stored run', () => {
    const current = run({ id: 'run-a', status: 'Completed', lastUpdated: '2024-01-05T00:00:00Z' });

    const result = mergeRuns([current], [update({ runId: 'run-a', status: 'Running', timestamp: '2024-01-04T00:00:00Z' })]);

    expect(result[0].status).toBe('Completed');
  });

  it('keeps the current step when an update has none', () => {
    const result = mergeRuns([run({ id: 'run-a', currentStepId: 'approval' })], [update({ runId: 'run-a', stepId: null, status: 'Failed', timestamp: '2024-02-01T00:00:00Z' })]);

    expect(result[0].currentStepId).toBe('approval');
    expect(result[0].status).toBe('Failed');
  });
});
