import { useCallback, useEffect, useState } from 'react';
import type { HumanInput } from '../types';
import { fetchHumanInputs } from '../api/client';
import { useGrpcStream } from './useGrpcStream';

const POLLING_INTERVAL_MS = 5000;

/** Runs awaiting human input (all modules), refreshed on stream updates and every 5 seconds. */
export function useHumanInputs() {
  const [inputs, setInputs] = useState<HumanInput[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { lastUpdate } = useGrpcStream();

  const load = useCallback(async () => {
    try {
      setInputs(await fetchHumanInputs());
      setError(null);
    } catch (err) {
      setError((err as { message?: string }).message ?? 'Failed to load approvals');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
    const interval = setInterval(load, POLLING_INTERVAL_MS);
    return () => clearInterval(interval);
  }, [load]);

  useEffect(() => {
    if (lastUpdate) load();
  }, [lastUpdate, load]);

  const remove = useCallback((runId: string) => {
    setInputs((prev) => prev.filter((i) => i.runId !== runId));
  }, []);

  return { inputs, isLoading, error, setError, remove, reload: load };
}
