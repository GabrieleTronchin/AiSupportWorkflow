import { useState, useEffect, useCallback, useRef } from 'react';
import type { AgentStatus, ApiError } from '../types';
import { fetchAgents } from '../api/client';

const POLLING_INTERVAL_MS = 3000;

export function useAgents() {
  const [agents, setAgents] = useState<AgentStatus[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);
  const [retryCount, setRetryCount] = useState(0);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const loadAgents = useCallback(async () => {
    try {
      const data = await fetchAgents();
      setAgents(data);
      setError(null);
    } catch (err) {
      setError(err as ApiError);
    } finally {
      setIsLoading(false);
    }
  }, []);

  // Fetch on mount and start polling
  useEffect(() => {
    loadAgents();

    intervalRef.current = setInterval(loadAgents, POLLING_INTERVAL_MS);

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
      }
    };
  }, [loadAgents, retryCount]);

  const retry = useCallback(() => {
    setError(null);
    setIsLoading(true);
    setRetryCount((c) => c + 1);
  }, []);

  return { agents, isLoading, error, retry };
}
