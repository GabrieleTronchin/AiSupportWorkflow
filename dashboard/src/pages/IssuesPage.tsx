import { useState } from 'react';
import { useIssues } from '../hooks/useIssues';
import { IssuesList } from '../components/IssuesList';
import type { WorkflowState } from '../types';

export function IssuesPage() {
  const { issues } = useIssues();
  const [_selectedIssue, setSelectedIssue] = useState<WorkflowState | null>(null);

  return (
    <div>
      <h1 className="text-2xl font-bold text-zinc-100 mb-6">Issues</h1>
      <IssuesList issues={issues} onSelectIssue={setSelectedIssue} />
    </div>
  );
}
