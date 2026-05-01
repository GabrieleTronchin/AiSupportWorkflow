import type { WorkflowState, WorkflowStage } from '../types';

/**
 * Formats an ISO 8601 timestamp as a relative time string.
 * Exported for independent testability.
 */
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

const terminalStages: WorkflowStage[] = ['Failed', 'ClassifiedOutOfScope', 'ManualReviewRequired'];
const completedStages: WorkflowStage[] = ['CodeChangeGenerated', 'Resolved'];

/**
 * Returns Tailwind classes for a stage badge based on its category.
 */
function getStageBadgeClasses(stage: WorkflowStage): string {
  if (terminalStages.includes(stage)) {
    return 'text-red-400 bg-red-400/10';
  }
  if (completedStages.includes(stage)) {
    return 'text-emerald-400 bg-emerald-400/10';
  }
  return 'text-blue-400 bg-blue-400/10';
}

interface IssuesListProps {
  issues: WorkflowState[];
  onSelectIssue: (issue: WorkflowState) => void;
}

export function IssuesList({ issues, onSelectIssue }: IssuesListProps) {
  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="text-zinc-400 border-b border-zinc-700">
          <th className="text-left py-2 px-3 font-medium">Issue ID</th>
          <th className="text-left py-2 px-3 font-medium">Stage</th>
          <th className="text-left py-2 px-3 font-medium">Detail</th>
          <th className="text-left py-2 px-3 font-medium">Last Updated</th>
        </tr>
      </thead>
      <tbody>
        {issues.map((issue) => (
          <tr
            key={issue.issueId}
            onClick={() => onSelectIssue(issue)}
            className="border-b border-zinc-800 hover:bg-zinc-800/50 cursor-pointer"
          >
            <td className="py-2 px-3 font-mono truncate max-w-[8ch]">
              {issue.issueId.slice(0, 8)}
            </td>
            <td className="py-2 px-3">
              <span
                className={`inline-block px-2 py-0.5 rounded text-xs font-medium ${getStageBadgeClasses(issue.stage)}`}
              >
                {issue.stage}
              </span>
            </td>
            <td className="py-2 px-3 truncate max-w-xs text-zinc-300">
              {issue.detail ?? '—'}
            </td>
            <td className="py-2 px-3 text-zinc-400">
              {formatRelativeTime(issue.lastUpdated)}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
