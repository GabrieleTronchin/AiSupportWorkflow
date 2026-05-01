import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import type { AgentStatus, ApiError } from '../types';
import { useAgents } from '../hooks/useAgents';

vi.mock('../api/client', () => ({
  fetchAgents: vi.fn(),
}));

import { fetchAgents } from '../api/client';

const mockFetchAgents = vi.mocked(fetchAgents);

const agentA: AgentStatus = {
  agentId: 'TeamA_BackendDeveloper',
  team: 'TeamA',
  role: 'BackendDeveloper',
  status: 'Idle',
  lastAction: null,
};

const agentB: AgentStatus = {
  agentId: 'TeamB_FrontendDeveloper',
  team: 'TeamB',
  role: 'FrontendDeveloper',
  status: 'Working',
  lastAction: 'Analyzing issue-123',
};

describe('useAgents', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    mockFetchAgents.mockResolvedValue([agentA, agentB]);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  describe('initial fetch', () => {
    it('fetches agents on mount', async () => {
      renderHook(() => useAgents());

      // Flush the initial async loadAgents call
      await act(async () => {
        await Promise.resolve();
      });

      expect(mockFetchAgents).toHaveBeenCalled();
    });

    it('sets isLoading to false after fetch', async () => {
      const { result } = renderHook(() => useAgents());

      expect(result.current.isLoading).toBe(true);

      await act(async () => {
        await Promise.resolve();
      });

      expect(result.current.isLoading).toBe(false);
    });

    it('sets agents in state', async () => {
      const { result } = renderHook(() => useAgents());

      await act(async () => {
        await Promise.resolve();
      });

      expect(result.current.agents).toEqual([agentA, agentB]);
    });
  });

  describe('polling', () => {
    it('polls at the configured interval', async () => {
      const pollInterval = 3000;
      renderHook(() => useAgents(pollInterval));

      // Flush initial fetch
      await act(async () => {
        await Promise.resolve();
      });

      const initialCallCount = mockFetchAgents.mock.calls.length;

      // Advance by the configured interval
      await act(async () => {
        vi.advanceTimersByTime(pollInterval);
        await Promise.resolve();
      });

      expect(mockFetchAgents).toHaveBeenCalledTimes(initialCallCount + 1);

      // Advance again
      await act(async () => {
        vi.advanceTimersByTime(pollInterval);
        await Promise.resolve();
      });

      expect(mockFetchAgents).toHaveBeenCalledTimes(initialCallCount + 2);
    });

    it('uses default 5000ms interval when not specified', async () => {
      renderHook(() => useAgents());

      // Flush initial fetch
      await act(async () => {
        await Promise.resolve();
      });

      const callCountAfterInit = mockFetchAgents.mock.calls.length;

      // Advance less than 5000ms - should not poll yet
      await act(async () => {
        vi.advanceTimersByTime(4999);
        await Promise.resolve();
      });

      expect(mockFetchAgents).toHaveBeenCalledTimes(callCountAfterInit);

      // Advance to 5000ms - should poll
      await act(async () => {
        vi.advanceTimersByTime(1);
        await Promise.resolve();
      });

      expect(mockFetchAgents).toHaveBeenCalledTimes(callCountAfterInit + 1);
    });
  });

  describe('cleanup', () => {
    it('clears interval on unmount', async () => {
      const { unmount } = renderHook(() => useAgents(2000));

      // Flush initial fetch
      await act(async () => {
        await Promise.resolve();
      });

      const callCountAfterInit = mockFetchAgents.mock.calls.length;

      unmount();

      // Advance timers after unmount - should not trigger more fetches
      await act(async () => {
        vi.advanceTimersByTime(10000);
        await Promise.resolve();
      });

      expect(mockFetchAgents).toHaveBeenCalledTimes(callCountAfterInit);
    });

    it('does not update state after unmount', async () => {
      const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      let resolvePromise: (value: AgentStatus[]) => void;
      mockFetchAgents.mockImplementation(
        () => new Promise<AgentStatus[]>((resolve) => { resolvePromise = resolve; })
      );

      const { unmount } = renderHook(() => useAgents());

      // Unmount before the fetch resolves
      unmount();

      // Resolve the pending fetch after unmount
      await act(async () => {
        resolvePromise!([agentA, agentB]);
        await Promise.resolve();
      });

      // No React warnings about updating unmounted component state
      // The cancelled flag in the hook prevents state updates
      expect(consoleErrorSpy).not.toHaveBeenCalled();

      consoleErrorSpy.mockRestore();
    });
  });

  describe('error handling', () => {
    it('sets error state when fetchAgents throws', async () => {
      const apiError: ApiError = { statusCode: 503, message: 'Service unavailable' };
      mockFetchAgents.mockRejectedValue(apiError);

      const { result } = renderHook(() => useAgents());

      // Flush microtasks to let the rejected promise settle and state update
      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(result.current.error).toEqual(apiError);
    });

    it('sets isLoading to false on error', async () => {
      mockFetchAgents.mockRejectedValue({ statusCode: 500, message: 'Internal error' });

      const { result } = renderHook(() => useAgents());

      expect(result.current.isLoading).toBe(true);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(result.current.isLoading).toBe(false);
    });
  });
});
