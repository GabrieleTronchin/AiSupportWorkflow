import { useState, useEffect, useCallback } from 'react';
import type { AgentStatus, ApiError } from '../types';
import { fetchAgents } from '../api/client';

export function useAgents(pollInterval = 10000) {
  const [agents, setAgents] = useState<AgentStatus[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);
  const [retryCount, setRetryCount] = useState(0);

  useEffect(() => {
    let cancelled = false;

    async function loadAgents() {
      try {
        const data = await fetchAgents();
        if (!cancelled) {
          setAgents(data);
          setError(null);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err as ApiError);
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    // Initial fetch
    loadAgents();

    // Set up polling interval
    const intervalId = setInterval(loadAgents, pollInterval);

    return () => {
      cancelled = true;
      clearInterval(intervalId);
    };
  }, [pollInterval, retryCount]);

  const retry = useCallback(() => {
    setError(null);
    setIsLoading(true);
    setRetryCount((c) => c + 1);
  }, []);

  return { agents, isLoading, error, retry };
}
