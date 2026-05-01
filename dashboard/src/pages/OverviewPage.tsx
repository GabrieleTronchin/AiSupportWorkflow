import { useAgents } from '../hooks/useAgents';
import { useGrpcStream } from '../hooks/useGrpcStream';
import { PipelineVisualizer } from '../components/PipelineVisualizer';
import { EmailComposer } from '../components/EmailComposer';
import type { WorkflowState } from '../types';

export function OverviewPage() {
  const { agents } = useAgents();
  const { latestStates, isConnected } = useGrpcStream();

  const activeAgents = agents.filter((a) => a.status === 'Working').length;
  const totalIssues = latestStates.length;
  const recentFailures = latestStates.filter((i) => i.stage === 'Failed').length;

  // Auto-select the most recent issue for the PipelineVisualizer
  const mostRecentIssue: WorkflowState | undefined = latestStates.length > 0
    ? latestStates.reduce((latest, current) =>
        new Date(current.lastUpdated) > new Date(latest.lastUpdated) ? current : latest
      )
    : undefined;

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-zinc-100">Overview</h1>
        <div className="flex items-center gap-2">
          <span
            className={`inline-block w-2.5 h-2.5 rounded-full ${isConnected ? 'bg-emerald-400' : 'bg-red-400'}`}
          />
          <span className="text-xs text-zinc-400">
            {isConnected ? 'Connected' : 'Disconnected'}
          </span>
        </div>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
        <div className="bg-zinc-800 rounded-lg p-4 border border-zinc-700">
          <p className="text-zinc-400 text-sm">Total Issues</p>
          <p className="text-2xl font-bold text-zinc-100">{totalIssues}</p>
        </div>
        <div className="bg-zinc-800 rounded-lg p-4 border border-zinc-700">
          <p className="text-zinc-400 text-sm">Active Agents</p>
          <p className="text-2xl font-bold text-zinc-100">{activeAgents}</p>
        </div>
        <div className="bg-zinc-800 rounded-lg p-4 border border-zinc-700">
          <p className="text-zinc-400 text-sm">Recent Failures</p>
          <p className="text-2xl font-bold text-zinc-100">{recentFailures}</p>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2">
          <PipelineVisualizer selectedIssue={mostRecentIssue} />
        </div>
        <div>
          <EmailComposer />
        </div>
      </div>
    </div>
  );
}
