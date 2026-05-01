import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import type { WorkflowState, ApiError } from '../types';
import { mergeIssues, useIssues } from '../hooks/useIssues';

vi.mock('../api/client', () => ({
  fetchIssues: vi.fn(),
}));

vi.mock('../hooks/useSSE', () => ({
  useSSE: vi.fn(),
}));

import { fetchIssues } from '../api/client';
import { useSSE } from '../hooks/useSSE';

const mockFetchIssues = vi.mocked(fetchIssues);
const mockUseSSE = vi.mocked(useSSE);

const issueA: WorkflowState = {
  issueId: 'issue-a',
  stage: 'Received',
  lastUpdated: '2024-01-01T00:00:00Z',
  detail: null,
};

const issueB: WorkflowState = {
  issueId: 'issue-b',
  stage: 'Classified',
  lastUpdated: '2024-01-02T00:00:00Z',
  detail: 'Code related',
};

const issueC: WorkflowState = {
  issueId: 'issue-c',
  stage: 'Resolving',
  lastUpdated: '2024-01-03T00:00:00Z',
  detail: 'In progress',
};

describe('useIssues', () => {
  beforeEach(() => {
    mockUseSSE.mockReturnValue({ latestStates: [], isConnected: true });
    mockFetchIssues.mockResolvedValue([issueA, issueB]);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('initial fetch', () => {
    it('returns empty array initially while loading', () => {
      mockFetchIssues.mockReturnValue(new Promise(() => {})); // never resolves

      const { result } = renderHook(() => useIssues());

      expect(result.current.issues).toEqual([]);
      expect(result.current.isLoading).toBe(true);
    });

    it('fetches issues on mount and sets them in state', async () => {
      const { result } = renderHook(() => useIssues());

      await waitFor(() => {
        expect(result.current.issues).toEqual([issueA, issueB]);
      });
    });

    it('sets isLoading to false after fetch completes', async () => {
      const { result } = renderHook(() => useIssues());

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });
    });
  });

  describe('SSE merge', () => {
    it('merges new issues from SSE into existing state', async () => {
      const { result, rerender } = renderHook(() => useIssues());

      await waitFor(() => {
        expect(result.current.issues).toEqual([issueA, issueB]);
      });

      mockUseSSE.mockReturnValue({ latestStates: [issueC], isConnected: true });
      rerender();

      await waitFor(() => {
        expect(result.current.issues).toContainEqual(issueC);
        expect(result.current.issues).toHaveLength(3);
      });
    });

    it('updates existing issues when SSE provides updated state for same issueId', async () => {
      const { result, rerender } = renderHook(() => useIssues());

      await waitFor(() => {
        expect(result.current.issues).toEqual([issueA, issueB]);
      });

      const updatedA: WorkflowState = {
        issueId: 'issue-a',
        stage: 'Resolved',
        lastUpdated: '2024-01-05T00:00:00Z',
        detail: 'Fixed',
      };

      mockUseSSE.mockReturnValue({ latestStates: [updatedA], isConnected: true });
      rerender();

      await waitFor(() => {
        const found = result.current.issues.find((i) => i.issueId === 'issue-a');
        expect(found).toEqual(updatedA);
      });
    });

    it('adds new issues that were not in the initial fetch', async () => {
      const { result, rerender } = renderHook(() => useIssues());

      await waitFor(() => {
        expect(result.current.issues).toHaveLength(2);
      });

      const newIssue: WorkflowState = {
        issueId: 'issue-new',
        stage: 'Received',
        lastUpdated: '2024-01-10T00:00:00Z',
        detail: null,
      };

      mockUseSSE.mockReturnValue({ latestStates: [newIssue], isConnected: true });
      rerender();

      await waitFor(() => {
        expect(result.current.issues).toContainEqual(newIssue);
        expect(result.current.issues).toHaveLength(3);
      });
    });
  });

  describe('error handling', () => {
    it('sets error state when fetchIssues throws', async () => {
      const apiError: ApiError = { statusCode: 500, message: 'Server error' };
      mockFetchIssues.mockRejectedValue(apiError);

      const { result } = renderHook(() => useIssues());

      await waitFor(() => {
        expect(result.current.error).toEqual(apiError);
      });
    });

    it('sets isLoading to false even on error', async () => {
      mockFetchIssues.mockRejectedValue({ statusCode: 500, message: 'Server error' });

      const { result } = renderHook(() => useIssues());

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });
    });
  });
});

describe('mergeIssues', () => {
  it('merges updates into existing array by issueId', () => {
    const existing: WorkflowState[] = [
      { issueId: 'a', stage: 'Received', lastUpdated: '2024-01-01T00:00:00Z', detail: null },
      { issueId: 'b', stage: 'Classified', lastUpdated: '2024-01-02T00:00:00Z', detail: 'info' },
    ];
    const updates: WorkflowState[] = [
      { issueId: 'a', stage: 'Resolved', lastUpdated: '2024-01-05T00:00:00Z', detail: 'done' },
      { issueId: 'c', stage: 'Received', lastUpdated: '2024-01-03T00:00:00Z', detail: null },
    ];

    const result = mergeIssues(existing, updates);

    expect(result).toHaveLength(3);
    expect(result.find((i) => i.issueId === 'a')?.stage).toBe('Resolved');
    expect(result.find((i) => i.issueId === 'b')?.stage).toBe('Classified');
    expect(result.find((i) => i.issueId === 'c')?.stage).toBe('Received');
  });

  it('preserves existing issues not in updates', () => {
    const existing: WorkflowState[] = [
      { issueId: 'x', stage: 'Resolving', lastUpdated: '2024-01-01T00:00:00Z', detail: 'working' },
      { issueId: 'y', stage: 'TeamAssigned', lastUpdated: '2024-01-02T00:00:00Z', detail: null },
    ];
    const updates: WorkflowState[] = [
      { issueId: 'z', stage: 'Received', lastUpdated: '2024-01-03T00:00:00Z', detail: null },
    ];

    const result = mergeIssues(existing, updates);

    expect(result).toContainEqual(existing[0]);
    expect(result).toContainEqual(existing[1]);
    expect(result).toContainEqual(updates[0]);
  });

  it('overwrites existing issues with matching issueId', () => {
    const existing: WorkflowState[] = [
      { issueId: 'dup', stage: 'Received', lastUpdated: '2024-01-01T00:00:00Z', detail: null },
    ];
    const updates: WorkflowState[] = [
      { issueId: 'dup', stage: 'CodeChangeGenerated', lastUpdated: '2024-01-10T00:00:00Z', detail: 'PR created' },
    ];

    const result = mergeIssues(existing, updates);

    expect(result).toHaveLength(1);
    expect(result[0]).toEqual(updates[0]);
  });
});
