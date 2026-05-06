import { createContext, useContext, useState, useEffect, useRef, useCallback, type ReactNode } from 'react';
import type { WorkflowState } from '../types';
import { createStreamClient, type GrpcStreamClient } from '../api/grpc-client';
import { fetchIssues } from '../api/client';

interface GrpcStreamContextValue {
  latestStates: WorkflowState[];
  isConnected: boolean;
}

const GrpcStreamCtx = createContext<GrpcStreamContextValue>({
  latestStates: [],
  isConnected: false,
});

/**
 * Provides a single gRPC stream connection that persists across page navigations.
 * On reconnection (e.g. after backend restart), resets state and fetches current issues
 * from the REST API to re-sync.
 */
export function GrpcStreamProvider({ children }: { children: ReactNode }) {
  const [latestStates, setLatestStates] = useState<WorkflowState[]>([]);
  const [isConnected, setIsConnected] = useState(false);
  const clientRef = useRef<GrpcStreamClient | null>(null);

  const handleUpdate = useCallback((state: WorkflowState) => {
    setLatestStates((prev) => {
      const existing = prev.findIndex((s) => s.issueId === state.issueId);
      if (existing >= 0) {
        const updated = [...prev];
        updated[existing] = state;
        return updated;
      }
      return [state, ...prev];
    });
  }, []);

  const handleReconnect = useCallback(async () => {
    // Backend was restarted — fetch current state from REST API
    try {
      const issues = await fetchIssues();
      setLatestStates(issues);
    } catch {
      // If fetch fails, just clear stale state
      setLatestStates([]);
    }
  }, []);

  // Also fetch initial state on mount from REST API
  useEffect(() => {
    fetchIssues()
      .then((issues) => setLatestStates(issues))
      .catch(() => {/* ignore — stream will populate */});
  }, []);

  useEffect(() => {
    const client = createStreamClient();
    clientRef.current = client;

    client.subscribe((state) => {
      handleUpdate(state);
      setIsConnected(client.isConnected);
    }, handleReconnect);

    const statusInterval = setInterval(() => {
      setIsConnected(client.isConnected);
    }, 2000);

    return () => {
      client.disconnect();
      clientRef.current = null;
      clearInterval(statusInterval);
    };
  }, [handleUpdate, handleReconnect]);

  return (
    <GrpcStreamCtx.Provider value={{ latestStates, isConnected }}>
      {children}
    </GrpcStreamCtx.Provider>
  );
}

/**
 * Hook to consume the shared gRPC stream state.
 * Uses the app-level context so the stream survives page transitions.
 */
export function useGrpcStreamContext(): GrpcStreamContextValue {
  return useContext(GrpcStreamCtx);
}
