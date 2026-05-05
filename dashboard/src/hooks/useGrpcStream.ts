import { useState, useEffect, useRef, useCallback } from 'react';
import type { WorkflowState } from '../types';
import { createStreamClient, type GrpcStreamClient } from '../api/grpc-client';

export function useGrpcStream() {
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

  useEffect(() => {
    const client = createStreamClient();
    clientRef.current = client;

    client.subscribe((state) => {
      handleUpdate(state);
      setIsConnected(client.isConnected);
    });

    // Poll connection status
    const statusInterval = setInterval(() => {
      setIsConnected(client.isConnected);
    }, 2000);

    return () => {
      client.disconnect();
      clientRef.current = null;
      clearInterval(statusInterval);
    };
  }, [handleUpdate]);

  return { latestStates, isConnected };
}
