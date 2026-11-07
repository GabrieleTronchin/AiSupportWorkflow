import type { RunSummary, RunUpdate } from '../types';

/** Applies a stream update to a run summary (creating it when the run is new). */
export function applyUpdate(run: RunSummary | undefined, update: RunUpdate): RunSummary {
  return {
    id: update.runId,
    moduleId: update.moduleId,
    workflowId: update.workflowId,
    workflowVersion: run?.workflowVersion ?? 0,
    title: update.title ?? run?.title ?? null,
    status: update.status,
    currentStepId: update.stepId ?? run?.currentStepId ?? null,
    detail: update.detail,
    assignedAgentId: run?.assignedAgentId ?? null,
    createdAt: run?.createdAt ?? update.timestamp,
    lastUpdated: update.timestamp,
  };
}

/**
 * Merges runs with stream updates (upsert by run id). Newer data wins: an update older than the
 * stored run is ignored, so out-of-order updates never move a run backwards.
 */
export function mergeRuns(existing: RunSummary[], updates: RunUpdate[]): RunSummary[] {
  const map = new Map(existing.map((run) => [run.id, run]));
  for (const update of updates) {
    const current = map.get(update.runId);
    if (current && new Date(update.timestamp).getTime() < new Date(current.lastUpdated).getTime()) {
      continue;
    }
    map.set(update.runId, applyUpdate(current, update));
  }
  return Array.from(map.values());
}

/** Newest first, by creation time. */
export function sortRuns(runs: RunSummary[]): RunSummary[] {
  return [...runs].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
}

/** Formats an ISO 8601 timestamp as a relative time string. */
export function formatRelativeTime(isoTimestamp: string): string {
  const now = Date.now();
  const then = new Date(isoTimestamp).getTime();
  const diffMs = now - then;

  if (diffMs < 0) return 'just now';

  const seconds = Math.floor(diffMs / 1000);
  if (seconds < 60) return `${seconds} sec ago`;

  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} min ago`;

  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} hour${hours > 1 ? 's' : ''} ago`;

  const days = Math.floor(hours / 24);
  return `${days} day${days > 1 ? 's' : ''} ago`;
}
