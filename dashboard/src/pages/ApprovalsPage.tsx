import { useState } from 'react';
import { useHumanInputs } from '../hooks/useHumanInputs';
import { useWorkflowCatalog } from '../hooks/WorkflowCatalogContext';
import { approveRun, abortRun, rejectRun } from '../api/client';

/** Runs of every workflow waiting for a human decision, rendered from their generic review payload. */
export function ApprovalsPage() {
  const { inputs, isLoading, error, setError, remove } = useHumanInputs();
  const { workflowName, stepName } = useWorkflowCatalog();
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);

  const act = async (runId: string, action: () => Promise<void>, failure: string) => {
    setActionInProgress(runId);
    try {
      await action();
      remove(runId);
    } catch {
      setError(failure);
    } finally {
      setActionInProgress(null);
    }
  };

  const handleApprove = (runId: string) => act(runId, () => approveRun(runId), `Failed to approve run ${runId}`);

  const handleReject = (runId: string) => {
    const reason = window.prompt('Rejection reason (optional):');
    return act(runId, () => rejectRun(runId, reason ?? undefined), `Failed to reject run ${runId}`);
  };

  const handleAbort = (runId: string) => {
    if (!window.confirm('Are you sure you want to abort this run? This cannot be undone.')) {
      return Promise.resolve();
    }
    return act(runId, () => abortRun(runId), `Failed to abort run ${runId}`);
  };

  if (isLoading) {
    return (
      <div>
        <h1 className="text-2xl font-bold text-zinc-100 mb-6">Pending Approvals</h1>
        <p className="text-zinc-400">Loading...</p>
      </div>
    );
  }

  return (
    <div>
      <h1 className="text-2xl font-bold text-zinc-100 mb-6">Pending Approvals</h1>

      {error && (
        <div className="bg-red-900/20 border border-red-700 rounded-lg p-4 mb-4">
          <p className="text-red-400 text-sm">{error}</p>
        </div>
      )}

      {inputs.length === 0 ? (
        <div className="bg-zinc-800 border border-zinc-700 rounded-lg p-6 text-center">
          <p className="text-zinc-300 font-medium mb-2">No pending approvals</p>
          <p className="text-zinc-500 text-sm">Runs awaiting human approval will appear here.</p>
        </div>
      ) : (
        <div className="space-y-4">
          {inputs.map((input) => (
            <article key={input.runId} className="bg-zinc-800 border border-zinc-700 rounded-lg p-5" aria-label={input.review.title}>
              <div className="flex items-start justify-between mb-3 gap-3">
                <div className="flex flex-col gap-1 min-w-0">
                  <h2 className="text-zinc-100 font-semibold">{input.review.title}</h2>
                  <div className="flex items-center gap-2 text-xs text-zinc-500 flex-wrap">
                    <span className="font-mono">{input.runId.slice(0, 8)}</span>
                    <span>{workflowName(input.workflowId)}</span>
                    <span>·</span>
                    <span>{stepName(input.workflowId, input.stepId)}</span>
                    {input.runTitle && (
                      <>
                        <span>·</span>
                        <span className="text-zinc-300 truncate">{input.runTitle}</span>
                      </>
                    )}
                  </div>
                </div>
              </div>

              <dl className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-4 text-sm">
                {input.review.fields.map((field) => (
                  <div key={field.label}>
                    <dt className="text-zinc-500 text-xs mb-0.5">{field.label}</dt>
                    <dd className="text-zinc-300 whitespace-pre-wrap">{field.value || '—'}</dd>
                  </div>
                ))}
              </dl>

              <div className="flex gap-2">
                <button
                  onClick={() => handleApprove(input.runId)}
                  disabled={actionInProgress === input.runId}
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white rounded text-sm font-medium transition-colors"
                >
                  Approve
                </button>
                <button
                  onClick={() => handleReject(input.runId)}
                  disabled={actionInProgress === input.runId}
                  className="px-4 py-2 bg-red-600 hover:bg-red-500 disabled:opacity-50 text-white rounded text-sm font-medium transition-colors"
                >
                  Reject
                </button>
                <button
                  onClick={() => handleAbort(input.runId)}
                  disabled={actionInProgress === input.runId}
                  className="px-4 py-2 bg-zinc-700 hover:bg-zinc-600 disabled:opacity-50 text-zinc-100 rounded text-sm font-medium transition-colors"
                >
                  Abort
                </button>
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  );
}
