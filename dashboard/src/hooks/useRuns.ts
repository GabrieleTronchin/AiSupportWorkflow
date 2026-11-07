import { useEffect, useMemo, useState } from 'react';
import type { ApiError, RunSummary } from '../types';
import { fetchRuns } from '../api/client';
import { useGrpcStream } from './useGrpcStream';
import { applyUpdate, sortRuns } from '../utils/runs';

/**
 * Runs, newest first: initial REST snapshot merged with real-time stream updates
 * (upsert by run id). Optionally filtered by workflow.
 */
export function useRuns(workflowId?: string | null) {
  const [runs, setRuns] = useState<RunSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);
  const { lastUpdate } = useGrpcStream();

  useEffect(() => {
    let cancelled = false;
    async function loadRuns() {
      try {
        setIsLoading(true);
        const data = await fetchRuns();
        if (!cancelled) {
          setRuns(sortRuns(data));
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
    loadRuns();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!lastUpdate) return;
    setRuns((prev) => {
      const index = prev.findIndex((r) => r.id === lastUpdate.runId);
      if (index < 0) {
        return sortRuns([applyUpdate(undefined, lastUpdate), ...prev]);
      }
      const next = [...prev];
      next[index] = applyUpdate(prev[index], lastUpdate);
      return next;
    });
  }, [lastUpdate]);

  const filtered = useMemo(
    () => (workflowId ? runs.filter((r) => r.workflowId === workflowId) : runs),
    [runs, workflowId],
  );

  return { runs: filtered, isLoading, error };
}
