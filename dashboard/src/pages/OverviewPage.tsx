import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAgents } from '../hooks/useAgents';
import { useConfig } from '../hooks/useConfig';
import { useGrpcStream } from '../hooks/useGrpcStream';
import { useWorkflowCatalog } from '../hooks/WorkflowCatalogContext';
import { PipelineVisualizer } from '../components/PipelineVisualizer';
import { RunSubmitForm } from '../components/RunSubmitForm';
import { LlmModeBadge } from '../components/LlmModeBadge';
import { ModuleWorkflowPicker } from '../components/ModuleWorkflowPicker';
import { formatStatus, getStatusBadgeClasses } from '../utils/badges';
import { abortRun } from '../api/client';
import { isTerminal } from '../types';

export function OverviewPage() {
  const { agents } = useAgents();
  const { latestRuns, isConnected } = useGrpcStream();
  const { selectedWorkflow, stepName } = useWorkflowCatalog();
  const { sequentialProcessing, demoMode, llmProvider, llmModel } = useConfig();
  const [abortingId, setAbortingId] = useState<string | null>(null);

  const workflowRuns = useMemo(
    () => (selectedWorkflow ? latestRuns.filter((r) => r.workflowId === selectedWorkflow.id) : []),
    [latestRuns, selectedWorkflow],
  );
  const activeRuns = useMemo(() => workflowRuns.filter((r) => !isTerminal(r.status)), [workflowRuns]);

  const activeAgents = agents.filter((a) => a.status === 'Working').length;
  const failures = workflowRuns.filter((r) => r.status === 'Failed' || r.status === 'Cancelled').length;
  const awaitingInput = latestRuns.filter((r) => r.status === 'AwaitingInput').length;

  const handleAbort = async (runId: string) => {
    if (!window.confirm('Are you sure you want to abort this run? This cannot be undone.')) {
      return;
    }
    setAbortingId(runId);
    try {
      await abortRun(runId);
    } catch {
      // The state will update via the stream
    } finally {
      setAbortingId(null);
    }
  };

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex items-start justify-between mb-4 gap-4">
        <div className="flex items-center gap-3 flex-wrap">
          <h1 className="text-2xl font-bold text-zinc-100">Overview</h1>
          <ModuleWorkflowPicker />
          {sequentialProcessing && (
            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-amber-900/50 text-amber-300 border border-amber-700">
              Sequential Mode
            </span>
          )}
          <LlmModeBadge demoMode={demoMode} llmProvider={llmProvider} llmModel={llmModel} />
        </div>
        <div className="flex items-center gap-2 shrink-0">
          <span className={`inline-block w-2.5 h-2.5 rounded-full ${isConnected ? 'bg-emerald-400' : 'bg-red-400'}`} />
          <span className="text-xs text-zinc-400">{isConnected ? 'Connected' : 'Disconnected'}</span>
        </div>
      </div>

      {/* Human input notification banner */}
      {awaitingInput > 0 && (
        <Link
          to="/approvals"
          className="block mb-4 px-4 py-3 rounded-lg bg-amber-900/30 border border-amber-700 text-amber-200 hover:bg-amber-900/50 transition-colors"
        >
          ⚠️ {awaitingInput} run(s) awaiting your approval — Review now →
        </Link>
      )}

      {/* Stats cards */}
      <div className="grid grid-cols-4 gap-4 mb-4">
        <div className="bg-zinc-800 rounded-lg p-3 border border-zinc-700">
          <p className="text-zinc-400 text-xs">Total Runs</p>
          <p className="text-xl font-bold text-zinc-100">{workflowRuns.length}</p>
        </div>
        <div className="bg-zinc-800 rounded-lg p-3 border border-zinc-700">
          <p className="text-zinc-400 text-xs">Active Agents</p>
          <p className="text-xl font-bold text-zinc-100">{activeAgents}</p>
        </div>
        <div className="bg-zinc-800 rounded-lg p-3 border border-zinc-700">
          <p className="text-zinc-400 text-xs">Awaiting Approval</p>
          <p className="text-xl font-bold text-amber-400">{awaitingInput}</p>
        </div>
        <div className="bg-zinc-800 rounded-lg p-3 border border-zinc-700">
          <p className="text-zinc-400 text-xs">Failures</p>
          <p className="text-xl font-bold text-zinc-100">{failures}</p>
        </div>
      </div>

      {/* Runs in progress */}
      <div className="bg-zinc-800/60 rounded-lg border border-zinc-700 p-4 mb-4">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-sm font-semibold text-zinc-300 uppercase tracking-wide">Pipeline Status</h2>
          <Link to="/events" className="text-xs text-blue-400 hover:text-blue-300 transition-colors">
            View Event Log →
          </Link>
        </div>

        {activeRuns.length === 0 ? (
          <p className="text-zinc-500 text-sm italic">No runs in progress — pipeline idle</p>
        ) : (
          <div className="space-y-2">
            {activeRuns.map((run) => (
              <div key={run.id} className="flex items-start gap-3 bg-zinc-900/60 rounded-md px-3 py-2.5">
                <span className="font-mono text-xs text-zinc-500 shrink-0 mt-0.5">{run.id.slice(0, 8)}</span>
                <span className={`inline-block px-1.5 py-0.5 rounded text-xs font-medium shrink-0 ${getStatusBadgeClasses(run.status)}`}>
                  {formatStatus(run.status)}
                </span>
                <span className="text-xs text-zinc-300 shrink-0 mt-0.5">{stepName(run.workflowId, run.currentStepId)}</span>
                <div className="flex flex-col gap-0.5 min-w-0 flex-1">
                  {run.title && <span className="text-sm text-zinc-100 font-medium truncate">{run.title}</span>}
                  <span className="text-xs text-zinc-400 truncate">{run.detail || '—'}</span>
                </div>
                <button
                  onClick={() => handleAbort(run.id)}
                  disabled={abortingId === run.id}
                  className="shrink-0 px-2 py-1 text-xs font-medium text-red-400 bg-red-900/30 border border-red-800 rounded hover:bg-red-900/60 disabled:opacity-50 transition-colors"
                >
                  {abortingId === run.id ? 'Aborting…' : 'Abort'}
                </button>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Graph + submission form */}
      {selectedWorkflow ? (
        <div className="flex flex-col gap-4">
          {/* Full width: workflows are long chains, labels must stay readable at the default zoom */}
          <div className="h-[320px] shrink-0 bg-zinc-900/40 rounded-lg border border-zinc-700 overflow-hidden">
            <PipelineVisualizer graph={selectedWorkflow.graph} activeRuns={activeRuns} />
          </div>
          <div className="bg-zinc-800/40 rounded-lg border border-zinc-700 p-4">
            <RunSubmitForm workflow={selectedWorkflow} />
          </div>
        </div>
      ) : (
        <p className="text-zinc-500 text-sm">No workflow available yet. Start a module service to load its workflows.</p>
      )}
    </div>
  );
}
