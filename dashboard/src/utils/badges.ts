import type { InboxStatus, ModuleStatus, RunStatus } from '../types';

/**
 * Returns Tailwind classes for a run status badge. Derived from the generic RunStatus,
 * so it works for every workflow.
 */
export function getStatusBadgeClasses(status: RunStatus): string {
  switch (status) {
    case 'Failed':
    case 'Cancelled':
      return 'text-red-400 bg-red-400/10';
    case 'Completed':
      return 'text-emerald-400 bg-emerald-400/10';
    case 'AwaitingInput':
      return 'text-amber-400 bg-amber-400/10';
    default:
      return 'text-blue-400 bg-blue-400/10';
  }
}

/** Human-readable status label. */
export function formatStatus(status: RunStatus): string {
  return status === 'AwaitingInput' ? 'Awaiting input' : status;
}

/**
 * Returns Tailwind classes for an agent status badge.
 */
export function getAgentStatusBadgeClasses(status: string): string {
  switch (status) {
    case 'Idle':
      return 'text-emerald-400 bg-emerald-400/10';
    case 'Working':
      return 'text-amber-400 bg-amber-400/10';
    default:
      return 'text-zinc-400 bg-zinc-400/10';
  }
}

/**
 * Returns Tailwind classes for an inbox status badge.
 */
export function getInboxStatusBadgeClasses(status: InboxStatus): string {
  switch (status) {
    case 'queued':
      return 'text-amber-400 bg-amber-400/10';
    case 'processed':
      return 'text-emerald-400 bg-emerald-400/10';
    case 'failed':
      return 'text-red-400 bg-red-400/10';
  }
}

/** Returns Tailwind classes for a module availability badge. */
export function getModuleStatusBadgeClasses(status: ModuleStatus): string {
  switch (status) {
    case 'Available':
      return 'text-emerald-400 bg-emerald-400/10';
    case 'Loading':
      return 'text-blue-400 bg-blue-400/10';
    case 'Invalid':
    case 'Unavailable':
      return 'text-red-400 bg-red-400/10';
  }
}
