import type { RunSummary } from '../types';
import { formatStatus, getStatusBadgeClasses } from '../utils/badges';
import { formatRelativeTime } from '../utils/runs';
import { useWorkflowCatalog } from '../hooks/WorkflowCatalogContext';

interface RunsListProps {
  runs: RunSummary[];
  onSelectRun?: (run: RunSummary) => void;
}

export function RunsList({ runs, onSelectRun }: RunsListProps) {
  const { stepName, workflowName } = useWorkflowCatalog();

  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="text-zinc-400 border-b border-zinc-700">
          <th className="text-left py-2 px-3 font-medium">Run ID</th>
          <th className="text-left py-2 px-3 font-medium">Title</th>
          <th className="text-left py-2 px-3 font-medium">Workflow</th>
          <th className="text-left py-2 px-3 font-medium">Status</th>
          <th className="text-left py-2 px-3 font-medium">Step</th>
          <th className="text-left py-2 px-3 font-medium">Detail</th>
          <th className="text-left py-2 px-3 font-medium">Last Updated</th>
        </tr>
      </thead>
      <tbody>
        {runs.map((run) => (
          <tr
            key={run.id}
            onClick={() => onSelectRun?.(run)}
            className={`border-b border-zinc-800 hover:bg-zinc-800/50 ${onSelectRun ? 'cursor-pointer' : ''}`}
          >
            <td className="py-2 px-3 font-mono truncate max-w-[8ch]">{run.id.slice(0, 8)}</td>
            <td className="py-2 px-3 truncate max-w-xs text-zinc-100">{run.title ?? '—'}</td>
            <td className="py-2 px-3 text-zinc-400">{workflowName(run.workflowId)}</td>
            <td className="py-2 px-3">
              <span className={`inline-block px-2 py-0.5 rounded text-xs font-medium ${getStatusBadgeClasses(run.status)}`}>
                {formatStatus(run.status)}
              </span>
            </td>
            <td className="py-2 px-3 text-zinc-300">{stepName(run.workflowId, run.currentStepId)}</td>
            <td className="py-2 px-3 truncate max-w-xs text-zinc-300">{run.detail ?? '—'}</td>
            <td className="py-2 px-3 text-zinc-400">{formatRelativeTime(run.lastUpdated)}</td>
          </tr>
        ))}
        {runs.length === 0 && (
          <tr>
            <td colSpan={7} className="py-6 text-center text-zinc-400">
              No runs
            </td>
          </tr>
        )}
      </tbody>
    </table>
  );
}
