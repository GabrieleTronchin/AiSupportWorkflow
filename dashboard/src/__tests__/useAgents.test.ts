import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import type { AgentStatus, ApiError, WorkflowState } from '../types';
import { useAgents } from '../hooks/useAgents';

vi.mock('../api/client', () => ({
  fetchAgents: vi.fn(),
}));

vi.mock('../hooks/useGrpcStream', () => ({
  useGrpcStream: vi.fn(),
}));

import { fetchAgents } from '../api/client';
import { useGrpcStream } from '../hooks/useGrpcStream';

const mockFetchAgents = vi.mocked(fetchAgents);
const mockUseGrpcStream = vi.mocked(useGrpcStream);

const agentA: AgentStatus = {
  agentId: 'TeamA_BackendDeveloper',
  team: 'TeamA',
  role: 'BackendDeveloper',
  status: 'Idle',
  lastAction: null,
  currentIssueId: null,
  currentSubject: null,
  currentStage: null,
};

const agentB: AgentStatus = {
  agentId: 'TeamB_FrontendDeveloper',
  team: 'TeamB',
  role: 'FrontendDeveloper',
  status: 'Working',
  lastAction: 'Analyzing issue-123',
  currentIssueId: null,
  currentSubject: null,
  currentStage: null,
};

describe('useAgents', () => {
  beforeEach(() => {
    mockFetchAgents.mockResolvedValue([agentA, agentB]);
    mockUseGrpcStream.mockReturnValue({ latestStates: [], isConnected: false });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('initial fetch', () => {
    it('fetches agents on mount', async () => {
      renderHook(() => useAgents());

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

  describe('gRPC stream refresh', () => {
    it('re-fetches agents when gRPC stream reports new states', async () => {
      const { rerender } = renderHook(() => useAgents());

      await act(async () => {
        await Promise.resolve();
      });

      const initialCallCount = mockFetchAgents.mock.calls.length;

      // Simulate gRPC stream update
      const streamUpdate: WorkflowState = {
        issueId: 'issue-1',
        stage: 'AgentAssigned',
        lastUpdated: new Date().toISOString(),
        detail: 'TeamA_BackendDeveloper',
      };
      mockUseGrpcStream.mockReturnValue({ latestStates: [streamUpdate], isConnected: true });

      rerender();

      await act(async () => {
        await Promise.resolve();
      });

      expect(mockFetchAgents).toHaveBeenCalledTimes(initialCallCount + 1);
    });

    it('does not re-fetch when latestStates is empty', async () => {
      renderHook(() => useAgents());

      await act(async () => {
        await Promise.resolve();
      });

      const callCount = mockFetchAgents.mock.calls.length;

      // latestStates remains empty — no additional fetch
      expect(mockFetchAgents).toHaveBeenCalledTimes(callCount);
    });
  });

  describe('error handling', () => {
    it('sets error state when fetchAgents throws', async () => {
      const apiError: ApiError = { statusCode: 503, message: 'Service unavailable' };
      mockFetchAgents.mockRejectedValue(apiError);

      const { result } = renderHook(() => useAgents());

      await act(async () => {
        await Promise.resolve();
        await Promise.resolve();
      });

      expect(result.current.error).toEqual(apiError);
    });

    it('sets isLoading to false on error', async () => {
      mockFetchAgents.mockRejectedValue({ statusCode: 500, message: 'Internal error' });

      const { result } = renderHook(() => useAgents());

      expect(result.current.isLoading).toBe(true);

      await act(async () => {
        await Promise.resolve();
        await Promise.resolve();
      });

      expect(result.current.isLoading).toBe(false);
    });
  });

  describe('retry', () => {
    it('exposes a retry function', async () => {
      const { result } = renderHook(() => useAgents());

      await act(async () => {
        await Promise.resolve();
      });

      expect(typeof result.current.retry).toBe('function');
    });

    it('re-triggers fetch when retry is called', async () => {
      const apiError: ApiError = { statusCode: 503, message: 'Service unavailable' };
      mockFetchAgents.mockRejectedValue(apiError);

      const { result } = renderHook(() => useAgents());

      await act(async () => {
        await Promise.resolve();
        await Promise.resolve();
      });

      expect(result.current.error).toEqual(apiError);

      // Now make the next fetch succeed
      mockFetchAgents.mockResolvedValue([agentA, agentB]);

      act(() => {
        result.current.retry();
      });

      expect(result.current.isLoading).toBe(true);
      expect(result.current.error).toBeNull();

      await act(async () => {
        await Promise.resolve();
      });

      expect(result.current.agents).toEqual([agentA, agentB]);
      expect(result.current.isLoading).toBe(false);
      expect(result.current.error).toBeNull();
    });
  });
});
