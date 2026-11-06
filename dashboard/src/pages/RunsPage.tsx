import { useMemo, useState } from 'react';
import { useRuns } from '../hooks/useRuns';
import { useWorkflowCatalog } from '../hooks/WorkflowCatalogContext';
import { RunsList } from '../components/RunsList';
import { ModuleWorkflowPicker } from '../components/ModuleWorkflowPicker';
import { RUN_STATUSES, type RunStatus } from '../types';
import { formatStatus } from '../utils/badges';

/** Runs of the selected workflow, filterable by status and by step (steps come from the workflow graph). */
export function RunsPage() {
  const { selectedWorkflow } = useWorkflowCatalog();
  const { runs } = useRuns(selectedWorkflow?.id);
  const [statusFilter, setStatusFilter] = useState<RunStatus | 'All'>('All');
  const [stepFilter, setStepFilter] = useState<string>('All');

  const steps = selectedWorkflow?.graph.nodes ?? [];
  const filtered = useMemo(
    () => runs.filter((run) =>
      (statusFilter === 'All' || run.status === statusFilter)
      && (stepFilter === 'All' || run.currentStepId === stepFilter)),
    [runs, statusFilter, stepFilter],
  );

  return (
    <div>
      <div className="flex items-center justify-between mb-6 gap-4 flex-wrap">
        <div className="flex items-center gap-3">
          <h1 className="text-2xl font-bold text-zinc-100">Runs</h1>
          <ModuleWorkflowPicker />
        </div>
        <div className="flex items-center gap-2">
          <select
            aria-label="Filter by status"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as RunStatus | 'All')}
            className="rounded-md bg-zinc-800 border border-zinc-700 text-zinc-100 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="All">All Statuses</option>
            {RUN_STATUSES.map((status) => (
              <option key={status} value={status}>{formatStatus(status)}</option>
            ))}
          </select>
          <select
            aria-label="Filter by step"
            value={stepFilter}
            onChange={(e) => setStepFilter(e.target.value)}
            className="rounded-md bg-zinc-800 border border-zinc-700 text-zinc-100 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="All">All Steps</option>
            {steps.map((step) => (
              <option key={step.id} value={step.id}>{step.name}</option>
            ))}
          </select>
        </div>
      </div>
      <RunsList runs={filtered} />
    </div>
  );
}
