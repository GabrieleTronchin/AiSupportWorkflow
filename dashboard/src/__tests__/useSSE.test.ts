import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useSSE } from '../hooks/useSSE';
import type { WorkflowState } from '../types';

vi.mock('../api/sse', () => ({
  createSSEConnection: vi.fn(),
}));

import { createSSEConnection } from '../api/sse';

const mockCreateSSEConnection = vi.mocked(createSSEConnection);

describe('useSSE', () => {
  let mockClose: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    mockClose = vi.fn();
    mockCreateSSEConnection.mockImplementation((_url, _onMessage, _onError) => {
      return { close: mockClose };
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('connection lifecycle', () => {
    it('creates EventSource connection on mount with correct URL', () => {
      renderHook(() => useSSE('/api/support/stream'));

      expect(mockCreateSSEConnection).toHaveBeenCalledWith(
        '/api/support/stream',
        expect.any(Function),
        expect.any(Function),
      );
    });

    it('closes connection on unmount', () => {
      const { unmount } = renderHook(() => useSSE('/api/support/stream'));

      unmount();

      expect(mockClose).toHaveBeenCalled();
    });

    it('returns isConnected as true initially', () => {
      const { result } = renderHook(() => useSSE('/api/support/stream'));

      expect(result.current.isConnected).toBe(true);
    });
  });

  describe('message parsing', () => {
    it('updates latestStates when a message is received', () => {
      let capturedOnMessage: ((states: WorkflowState[]) => void) | undefined;

      mockCreateSSEConnection.mockImplementation((_url, onMessage, _onError) => {
        capturedOnMessage = onMessage;
        return { close: mockClose };
      });

      const { result } = renderHook(() => useSSE('/api/support/stream'));

      const states: WorkflowState[] = [
        { issueId: 'issue-1', stage: 'Received', lastUpdated: '2024-01-01T00:00:00Z', detail: null },
        { issueId: 'issue-2', stage: 'Classified', lastUpdated: '2024-01-02T00:00:00Z', detail: 'Code related' },
      ];

      act(() => {
        capturedOnMessage!(states);
      });

      expect(result.current.latestStates).toEqual(states);
    });

    it('sets isConnected to true on message receipt', () => {
      let capturedOnMessage: ((states: WorkflowState[]) => void) | undefined;
      let capturedOnError: ((event: Event) => void) | undefined;

      mockCreateSSEConnection.mockImplementation((_url, onMessage, onError) => {
        capturedOnMessage = onMessage;
        capturedOnError = onError;
        return { close: mockClose };
      });

      const { result } = renderHook(() => useSSE('/api/support/stream'));

      // Simulate an error first to set isConnected to false
      act(() => {
        capturedOnError!(new Event('error'));
      });

      expect(result.current.isConnected).toBe(false);

      // Now simulate a message to confirm isConnected goes back to true
      act(() => {
        capturedOnMessage!([
          { issueId: 'issue-1', stage: 'Received', lastUpdated: '2024-01-01T00:00:00Z', detail: null },
        ]);
      });

      expect(result.current.isConnected).toBe(true);
    });
  });

  describe('error handling', () => {
    it('sets isConnected to false on error event', () => {
      let capturedOnError: ((event: Event) => void) | undefined;

      mockCreateSSEConnection.mockImplementation((_url, _onMessage, onError) => {
        capturedOnError = onError;
        return { close: mockClose };
      });

      const { result } = renderHook(() => useSSE('/api/support/stream'));

      act(() => {
        capturedOnError!(new Event('error'));
      });

      expect(result.current.isConnected).toBe(false);
    });
  });
});
