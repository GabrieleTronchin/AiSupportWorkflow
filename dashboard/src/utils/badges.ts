import type { WorkflowStage, InboxStatus } from '../types';

const terminalStages: WorkflowStage[] = ['Failed', 'ClassifiedOutOfScope', 'ManualReviewRequired'];
const completedStages: WorkflowStage[] = ['CodeChangeGenerated', 'Resolved'];
const awaitingStages: WorkflowStage[] = ['AwaitingApproval'];

/**
 * Returns Tailwind classes for a workflow stage badge.
 */
export function getStageBadgeClasses(stage: WorkflowStage): string {
  if (terminalStages.includes(stage)) {
    return 'text-red-400 bg-red-400/10';
  }
  if (completedStages.includes(stage)) {
    return 'text-emerald-400 bg-emerald-400/10';
  }
  if (awaitingStages.includes(stage)) {
    return 'text-amber-400 bg-amber-400/10';
  }
  return 'text-blue-400 bg-blue-400/10';
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
