import { useState, useEffect, useRef } from 'react';
import type { WorkflowState } from '../types';
import { createSSEConnection } from '../api/sse';

export function useSSE(url: string) {
  const [latestStates, setLatestStates] = useState<WorkflowState[]>([]);
  const [isConnected, setIsConnected] = useState(false);
  const connectionRef = useRef<{ close: () => void } | null>(null);

  useEffect(() => {
    const connection = createSSEConnection(
      url,
      (states) => {
        setLatestStates(states);
        setIsConnected(true);
      },
      () => {
        setIsConnected(false);
      },
    );
    connectionRef.current = connection;
    setIsConnected(true);

    return () => {
      connection.close();
      connectionRef.current = null;
    };
  }, [url]);

  return { latestStates, isConnected };
}
