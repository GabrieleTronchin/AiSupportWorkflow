import { useState } from 'react';
import { useIssues } from '../hooks/useIssues';
import { useAgents } from '../hooks/useAgents';
import { PipelineVisualizer } from '../components/PipelineVisualizer';
import type { WorkflowState } from '../types';

export function OverviewPage() {
  const { issues } = useIssues();
  const { agents } = useAgents();
  const [selectedIssue] = useState<WorkflowState | undefined>();

  const totalIssues = issues.length;
  const activeAgents = agents.filter((a) => a.status === 'Working').length;
  const recentFailures = issues.filter((i) => i.stage === 'Failed').length;

  return (
    <div>
      <h1 className="text-2xl font-bold text-zinc-100 mb-6">Overview</h1>

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

      <PipelineVisualizer selectedIssue={selectedIssue} />
    </div>
  );
}
