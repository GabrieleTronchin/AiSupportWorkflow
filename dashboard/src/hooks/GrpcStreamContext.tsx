import { createContext, useContext, useState, useEffect, useRef, useCallback, type ReactNode } from 'react';
import type { RunSummary, RunUpdate } from '../types';
import { createStreamClient, type GrpcStreamClient } from '../api/grpc-client';
import { fetchRuns } from '../api/client';
import { mergeRuns, sortRuns } from '../utils/runs';

interface GrpcStreamContextValue {
  /** Latest known state of every run (REST snapshot merged with stream updates), newest first. */
  latestRuns: RunSummary[];
  /** Last update received from the stream (null before the first one). */
  lastUpdate: RunUpdate | null;
  isConnected: boolean;
}

const GrpcStreamCtx = createContext<GrpcStreamContextValue>({
  latestRuns: [],
  lastUpdate: null,
  isConnected: false,
});

/**
 * Provides a single gRPC stream connection that persists across page navigations.
 * On (re)connection it loads the current runs from the REST API to re-sync.
 */
export function GrpcStreamProvider({ children }: { children: ReactNode }) {
  const [latestRuns, setLatestRuns] = useState<RunSummary[]>([]);
  const [lastUpdate, setLastUpdate] = useState<RunUpdate | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const clientRef = useRef<GrpcStreamClient | null>(null);

  const handleUpdate = useCallback((update: RunUpdate) => {
    setLatestRuns((prev) => sortRuns(mergeRuns(prev, [update])));
    setLastUpdate(update);
  }, []);

  const loadRuns = useCallback(async () => {
    try {
      setLatestRuns(sortRuns(await fetchRuns()));
    } catch {
      // Keep the current state; the stream will populate it
    }
  }, []);

  useEffect(() => {
    loadRuns();
  }, [loadRuns]);

  useEffect(() => {
    const client = createStreamClient();
    clientRef.current = client;

    client.subscribe((update) => {
      handleUpdate(update);
      setIsConnected(client.isConnected);
    }, loadRuns);

    const statusInterval = setInterval(() => {
      setIsConnected(client.isConnected);
    }, 2000);

    return () => {
      client.disconnect();
      clientRef.current = null;
      clearInterval(statusInterval);
    };
  }, [handleUpdate, loadRuns]);

  return (
    <GrpcStreamCtx.Provider value={{ latestRuns, lastUpdate, isConnected }}>
      {children}
    </GrpcStreamCtx.Provider>
  );
}

/** Shared gRPC stream state (app-level, survives page transitions). */
export function useGrpcStreamContext(): GrpcStreamContextValue {
  return useContext(GrpcStreamCtx);
}
