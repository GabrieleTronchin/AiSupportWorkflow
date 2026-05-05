import { useState, useEffect, useCallback } from 'react';
import type { AgentStatus, ApiError } from '../types';
import { fetchAgents } from '../api/client';
import { useGrpcStream } from './useGrpcStream';

export function useAgents() {
  const [agents, setAgents] = useState<AgentStatus[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);
  const [retryCount, setRetryCount] = useState(0);
  const { latestStates } = useGrpcStream();

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

  // Fetch initial agents on mount (and on retry)
  useEffect(() => {
    loadAgents();
  }, [loadAgents, retryCount]);

  // Re-fetch agents when gRPC stream reports a stage change
  useEffect(() => {
    if (latestStates.length === 0) return;
    loadAgents();
  }, [latestStates, loadAgents]);

  const retry = useCallback(() => {
    setError(null);
    setIsLoading(true);
    setRetryCount((c) => c + 1);
  }, []);

  return { agents, isLoading, error, retry };
}
