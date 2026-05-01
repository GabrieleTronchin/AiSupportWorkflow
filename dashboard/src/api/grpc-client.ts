import { createClient } from '@connectrpc/connect';
import { createGrpcWebTransport } from '@connectrpc/connect-web';
import { WorkflowMonitor } from '../gen/workflow_monitor_pb';
import type { WorkflowState, WorkflowStage } from '../types';

const transport = createGrpcWebTransport({ baseUrl: window.location.origin });
const client = createClient(WorkflowMonitor, transport);

const INITIAL_RECONNECT_DELAY = 2000;
const MAX_RECONNECT_DELAY = 30000;

export interface GrpcStreamClient {
  subscribe(onUpdate: (state: WorkflowState) => void): void;
  disconnect(): void;
  isConnected: boolean;
}

/**
 * Creates a streaming client that connects to the backend WorkflowMonitor gRPC-Web service.
 * Receives real-time WorkflowStateUpdate messages via server streaming.
 * Implements auto-reconnect with exponential backoff on stream errors.
 */
export function createStreamClient(): GrpcStreamClient {
  let connected = false;
  let abortController: AbortController | null = null;
  let reconnectDelay = INITIAL_RECONNECT_DELAY;
  let reconnectTimeoutId: ReturnType<typeof setTimeout> | null = null;

  async function startStream(onUpdate: (state: WorkflowState) => void) {
    if (!abortController || abortController.signal.aborted) return;

    try {
      const stream = client.subscribeToUpdates(
        {},
        { signal: abortController.signal },
      );

      connected = true;
      reconnectDelay = INITIAL_RECONNECT_DELAY;

      for await (const update of stream) {
        if (abortController?.signal.aborted) return;

        onUpdate({
          issueId: update.issueId,
          stage: update.stage as WorkflowStage,
          lastUpdated: update.lastUpdated,
          detail: update.detail ?? null,
        });
      }

      // Stream ended normally (server closed) — reconnect
      if (!abortController?.signal.aborted) {
        connected = false;
        scheduleReconnect(onUpdate);
      }
    } catch (err: unknown) {
      if (abortController?.signal.aborted) return;

      connected = false;
      scheduleReconnect(onUpdate);
    }
  }

  function scheduleReconnect(onUpdate: (state: WorkflowState) => void) {
    reconnectTimeoutId = setTimeout(() => {
      reconnectTimeoutId = null;
      startStream(onUpdate);
    }, reconnectDelay);

    reconnectDelay = Math.min(reconnectDelay * 2, MAX_RECONNECT_DELAY);
  }

  return {
    get isConnected() {
      return connected;
    },

    subscribe(onUpdate: (state: WorkflowState) => void) {
      abortController = new AbortController();
      reconnectDelay = INITIAL_RECONNECT_DELAY;
      startStream(onUpdate);
    },

    disconnect() {
      if (reconnectTimeoutId) {
        clearTimeout(reconnectTimeoutId);
        reconnectTimeoutId = null;
      }
      if (abortController) {
        abortController.abort();
        abortController = null;
      }
      connected = false;
    },
  };
}
