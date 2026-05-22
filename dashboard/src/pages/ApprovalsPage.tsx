import { useCallback, useEffect, useState } from 'react';
import type { PendingApproval } from '../types';
import { fetchPendingApprovals, approveWorkflow, rejectWorkflow } from '../api/client';

export function ApprovalsPage() {
  const [approvals, setApprovals] = useState<PendingApproval[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);

  const loadApprovals = useCallback(async () => {
    try {
      const data = await fetchPendingApprovals();
      setApprovals(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load approvals');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadApprovals();
    const interval = setInterval(loadApprovals, 5000);
    return () => clearInterval(interval);
  }, [loadApprovals]);

  const handleApprove = async (issueId: string) => {
    setActionInProgress(issueId);
    try {
      await approveWorkflow(issueId);
      setApprovals((prev) => prev.filter((a) => a.issueId !== issueId));
    } catch {
      setError(`Failed to approve workflow ${issueId}`);
    } finally {
      setActionInProgress(null);
    }
  };

  const handleReject = async (issueId: string) => {
    const reason = window.prompt('Rejection reason (optional):');
    setActionInProgress(issueId);
    try {
      await rejectWorkflow(issueId, reason ?? undefined);
      setApprovals((prev) => prev.filter((a) => a.issueId !== issueId));
    } catch {
      setError(`Failed to reject workflow ${issueId}`);
    } finally {
      setActionInProgress(null);
    }
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

      {approvals.length === 0 ? (
        <div className="bg-zinc-800 border border-zinc-700 rounded-lg p-6 text-center">
          <p className="text-zinc-300 font-medium mb-2">No pending approvals</p>
          <p className="text-zinc-500 text-sm">
            Workflows awaiting human approval will appear here.
          </p>
        </div>
      ) : (
        <div className="space-y-4">
          {approvals.map((approval) => (
            <div
              key={approval.issueId}
              className="bg-zinc-800 border border-zinc-700 rounded-lg p-5"
            >
              <div className="flex items-start justify-between mb-3">
                <div className="flex items-center gap-2">
                  <span className="font-mono text-xs text-zinc-500">
                    {approval.issueId.slice(0, 8)}
                  </span>
                  <span className="inline-block px-2 py-0.5 rounded text-xs font-medium text-amber-400 bg-amber-400/10">
                    {approval.report.severityAssessment}
                  </span>
                  {approval.report.requiresEscalation && (
                    <span className="inline-block px-2 py-0.5 rounded text-xs font-medium text-red-400 bg-red-400/10">
                      Escalation Required
                    </span>
                  )}
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-4 text-sm">
                <div>
                  <p className="text-zinc-500 text-xs mb-0.5">Root Cause</p>
                  <p className="text-zinc-300">{approval.report.rootCauseDescription}</p>
                </div>
                <div>
                  <p className="text-zinc-500 text-xs mb-0.5">Affected Component</p>
                  <p className="text-zinc-300">{approval.report.affectedComponent}</p>
                </div>
                <div>
                  <p className="text-zinc-500 text-xs mb-0.5">Proposed Fix</p>
                  <p className="text-zinc-300">{approval.report.proposedFixSummary}</p>
                </div>
                {approval.report.escalationReason && (
                  <div>
                    <p className="text-zinc-500 text-xs mb-0.5">Escalation Reason</p>
                    <p className="text-zinc-300">{approval.report.escalationReason}</p>
                  </div>
                )}
              </div>

              <div className="flex gap-2">
                <button
                  onClick={() => handleApprove(approval.issueId)}
                  disabled={actionInProgress === approval.issueId}
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white rounded text-sm font-medium transition-colors"
                >
                  Approve
                </button>
                <button
                  onClick={() => handleReject(approval.issueId)}
                  disabled={actionInProgress === approval.issueId}
                  className="px-4 py-2 bg-red-600 hover:bg-red-500 disabled:opacity-50 text-white rounded text-sm font-medium transition-colors"
                >
                  Reject
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
