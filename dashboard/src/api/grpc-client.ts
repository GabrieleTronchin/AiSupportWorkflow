import { createClient } from '@connectrpc/connect';
import { createGrpcWebTransport } from '@connectrpc/connect-web';
import { RunMonitor } from '../gen/run_monitor_pb';
import type { RunEventKind, RunStatus, RunUpdate } from '../types';

const transport = createGrpcWebTransport({ baseUrl: window.location.origin });
const client = createClient(RunMonitor, transport);

const INITIAL_RECONNECT_DELAY = 2000;
const MAX_RECONNECT_DELAY = 30000;

export interface GrpcStreamClient {
  subscribe(onUpdate: (update: RunUpdate) => void, onReconnect?: () => void): void;
  disconnect(): void;
  isConnected: boolean;
}

/**
 * Creates a streaming client for the engine RunMonitor gRPC-Web service (through the gateway).
 * Receives real-time run transitions via server streaming, reconnects with exponential backoff
 * and calls onReconnect when the stream comes back after a disconnection.
 */
export function createStreamClient(): GrpcStreamClient {
  let connected = false;
  let abortController: AbortController | null = null;
  let reconnectDelay = INITIAL_RECONNECT_DELAY;
  let reconnectTimeoutId: ReturnType<typeof setTimeout> | null = null;
  let hasConnectedBefore = false;

  async function startStream(onUpdate: (update: RunUpdate) => void, onReconnect?: () => void) {
    if (!abortController || abortController.signal.aborted) return;

    try {
      const stream = client.subscribe({}, { signal: abortController.signal });

      connected = true;
      reconnectDelay = INITIAL_RECONNECT_DELAY;

      // If this is a reconnection (not the first connect), notify the caller
      if (hasConnectedBefore && onReconnect) {
        onReconnect();
      }
      hasConnectedBefore = true;

      for await (const update of stream) {
        if (abortController?.signal.aborted) return;

        onUpdate({
          runId: update.runId,
          moduleId: update.moduleId,
          workflowId: update.workflowId,
          stepId: update.stepId || null,
          status: update.status as RunStatus,
          eventKind: update.eventKind as RunEventKind,
          timestamp: update.timestamp,
          detail: update.detail ?? null,
          title: update.title ?? null,
        });
      }

      // Stream ended normally (server closed) — reconnect
      if (!abortController?.signal.aborted) {
        connected = false;
        scheduleReconnect(onUpdate, onReconnect);
      }
    } catch {
      if (abortController?.signal.aborted) return;

      connected = false;
      scheduleReconnect(onUpdate, onReconnect);
    }
  }

  function scheduleReconnect(onUpdate: (update: RunUpdate) => void, onReconnect?: () => void) {
    reconnectTimeoutId = setTimeout(() => {
      reconnectTimeoutId = null;
      startStream(onUpdate, onReconnect);
    }, reconnectDelay);

    reconnectDelay = Math.min(reconnectDelay * 2, MAX_RECONNECT_DELAY);
  }

  return {
    get isConnected() {
      return connected;
    },

    subscribe(onUpdate: (update: RunUpdate) => void, onReconnect?: () => void) {
      abortController = new AbortController();
      reconnectDelay = INITIAL_RECONNECT_DELAY;
      hasConnectedBefore = false;
      startStream(onUpdate, onReconnect);
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
