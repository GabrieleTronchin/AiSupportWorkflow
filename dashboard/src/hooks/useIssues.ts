import { useState, useEffect } from 'react';
import type { WorkflowState, ApiError } from '../types';
import { fetchIssues } from '../api/client';
import { useSSE } from './useSSE';

/**
 * Merges existing issues with SSE updates using upsert by issueId.
 * Exported separately for independent testability (property-based tests).
 */
export function mergeIssues(existing: WorkflowState[], updates: WorkflowState[]): WorkflowState[] {
  const map = new Map(existing.map((issue) => [issue.issueId, issue]));
  for (const state of updates) {
    map.set(state.issueId, state);
  }
  return Array.from(map.values());
}

export function useIssues() {
  const [issues, setIssues] = useState<WorkflowState[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);
  const { latestStates } = useSSE('/api/support/stream');

  // Fetch initial issues on mount
  useEffect(() => {
    let cancelled = false;

    async function loadIssues() {
      try {
        setIsLoading(true);
        const data = await fetchIssues();
        if (!cancelled) {
          setIssues(data);
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

    loadIssues();

    return () => {
      cancelled = true;
    };
  }, []);

  // Merge SSE updates into local state (upsert by issueId)
  useEffect(() => {
    if (latestStates.length === 0) return;

    setIssues((prev) => mergeIssues(prev, latestStates));
  }, [latestStates]);

  return { issues, isLoading, error };
}
