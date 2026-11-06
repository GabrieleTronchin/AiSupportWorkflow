import { useWorkflowCatalog } from '../hooks/WorkflowCatalogContext';
import { getModuleStatusBadgeClasses } from '../utils/badges';

/**
 * Selects the module and workflow shown by the dashboard (remembered across reloads)
 * and shows the module availability and validation errors.
 */
export function ModuleWorkflowPicker() {
  const { modules, workflows, selection, selectedModule, select, isLoading, error } = useWorkflowCatalog();

  if (isLoading) {
    return <span className="text-xs text-zinc-500">Loading workflows…</span>;
  }

  if (error && workflows.length === 0) {
    return <span className="text-xs text-red-400" role="alert">Engine unavailable: {error}</span>;
  }

  const moduleWorkflows = workflows.filter((w) => w.moduleId === selection?.moduleId);
  const blockingErrors = selectedModule?.errors.filter((e) => e.severity === 'Error') ?? [];

  return (
    <div className="flex flex-col gap-1">
      <div className="flex items-center gap-2">
        <select
          aria-label="Module"
          value={selection?.moduleId ?? ''}
          onChange={(e) => {
            const first = workflows.find((w) => w.moduleId === e.target.value);
            if (first) select({ moduleId: first.moduleId, workflowId: first.id });
          }}
          className="rounded-md bg-zinc-800 border border-zinc-700 text-zinc-100 px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          {modules.map((m) => (
            <option key={m.id} value={m.id} disabled={!workflows.some((w) => w.moduleId === m.id)}>
              {m.name}
            </option>
          ))}
        </select>
        <select
          aria-label="Workflow"
          value={selection?.workflowId ?? ''}
          onChange={(e) => selection && select({ moduleId: selection.moduleId, workflowId: e.target.value })}
          className="rounded-md bg-zinc-800 border border-zinc-700 text-zinc-100 px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          {moduleWorkflows.map((w) => (
            <option key={w.id} value={w.id}>
              {w.name}
            </option>
          ))}
        </select>
        {selectedModule && (
          <span
            className={`inline-block px-2 py-0.5 rounded text-xs font-medium ${getModuleStatusBadgeClasses(selectedModule.status)}`}
            title={selectedModule.lastError ?? undefined}
          >
            {selectedModule.status}
          </span>
        )}
      </div>
      {modules.filter((m) => m.status !== 'Available').map((m) => (
        <p key={m.id} className="text-xs text-amber-300" role="status">
          Module {m.name}: {m.status}{m.lastError ? ` — ${m.lastError}` : ''}
        </p>
      ))}
      {blockingErrors.length > 0 && (
        <ul className="text-xs text-red-400 list-disc pl-4" aria-label="Validation errors">
          {blockingErrors.map((e, i) => (
            <li key={i}>
              {e.file}{e.line ? `:${e.line}` : ''}{e.stepId ? ` [${e.stepId}]` : ''} {e.message}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
