import { useState } from 'react';
import { useIssues } from '../hooks/useIssues';
import { IssuesList } from '../components/IssuesList';
import type { WorkflowState, WorkflowStage } from '../types';

const allStages: WorkflowStage[] = [
  'Received',
  'Classified',
  'ClassifiedOutOfScope',
  'TeamAssigned',
  'AgentAssigned',
  'Resolving',
  'Resolved',
  'CodeChangeGenerated',
  'Failed',
  'ManualReviewRequired',
];

export function IssuesPage() {
  const { issues } = useIssues();
  const [_selectedIssue, setSelectedIssue] = useState<WorkflowState | null>(null);
  const [stageFilter, setStageFilter] = useState<WorkflowStage | 'All'>('All');

  const filteredIssues = stageFilter === 'All'
    ? issues
    : issues.filter((issue) => issue.stage === stageFilter);

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-zinc-100">Issues</h1>
        <select
          value={stageFilter}
          onChange={(e) => setStageFilter(e.target.value as WorkflowStage | 'All')}
          className="rounded-md bg-zinc-800 border border-zinc-700 text-zinc-100 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          <option value="All">All Stages</option>
          {allStages.map((stage) => (
            <option key={stage} value={stage}>{stage}</option>
          ))}
        </select>
      </div>
      <IssuesList issues={filteredIssues} onSelectIssue={setSelectedIssue} />
    </div>
  );
}
