import type { WorkflowState } from '../types';

const BASE_URL = '/api/support';
const RECONNECT_DELAYS = [1000, 2000, 4000, 8000, 16000, 30000];

export interface GrpcStreamClient {
  subscribe(onUpdate: (state: WorkflowState) => void): void;
  disconnect(): void;
  isConnected: boolean;
}

/**
 * Creates a streaming client that polls the issues endpoint for real-time updates.
 * Uses polling as a gRPC-Web compatible fallback until full proto generation is configured.
 * Implements auto-reconnect with exponential backoff.
 */
export function createStreamClient(): GrpcStreamClient {
  let connected = false;
  let abortController: AbortController | null = null;
  let reconnectAttempt = 0;
  let timeoutId: ReturnType<typeof setTimeout> | null = null;
  let onUpdateCallback: ((state: WorkflowState) => void) | null = null;
  let previousStates: Map<string, string> = new Map();

  async function poll() {
    if (!onUpdateCallback) return;

    try {
      const response = await fetch(`${BASE_URL}/issues`, {
        signal: abortController?.signal,
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const states = (await response.json()) as WorkflowState[];
      connected = true;
      reconnectAttempt = 0;

      // Detect changes and emit updates
      for (const state of states) {
        const prevStage = previousStates.get(state.issueId);
        if (prevStage !== state.stage) {
          previousStates.set(state.issueId, state.stage);
          onUpdateCallback(state);
        }
      }

      // Schedule next poll
      if (abortController && !abortController.signal.aborted) {
        timeoutId = setTimeout(poll, 1000);
      }
    } catch (error) {
      if (abortController?.signal.aborted) return;

      connected = false;
      const delay = RECONNECT_DELAYS[Math.min(reconnectAttempt, RECONNECT_DELAYS.length - 1)];
      reconnectAttempt++;
      timeoutId = setTimeout(poll, delay);
    }
  }

  return {
    get isConnected() {
      return connected;
    },

    subscribe(onUpdate: (state: WorkflowState) => void) {
      onUpdateCallback = onUpdate;
      abortController = new AbortController();
      previousStates = new Map();
      reconnectAttempt = 0;
      poll();
    },

    disconnect() {
      onUpdateCallback = null;
      if (timeoutId) {
        clearTimeout(timeoutId);
        timeoutId = null;
      }
      if (abortController) {
        abortController.abort();
        abortController = null;
      }
      connected = false;
      previousStates.clear();
    },
  };
}
