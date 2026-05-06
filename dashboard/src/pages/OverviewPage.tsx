import { Link } from 'react-router-dom';
import { useAgents } from '../hooks/useAgents';
import { useConfig } from '../hooks/useConfig';
import { useGrpcStream } from '../hooks/useGrpcStream';
import { PipelineVisualizer } from '../components/PipelineVisualizer';
import { EmailComposer } from '../components/EmailComposer';
import { getStageBadgeClasses } from '../utils/badges';

export function OverviewPage() {
  const { agents } = useAgents();
  const { latestStates, isConnected } = useGrpcStream();
  const { sequentialProcessing } = useConfig();

  const activeAgents = agents.filter((a) => a.status === 'Working').length;
  const totalIssues = latestStates.length;
  const recentFailures = latestStates.filter((i) => i.stage === 'Failed').length;

  // Filter to non-terminal issues for the PipelineVisualizer
  const terminalStages: string[] = ['CodeChangeGenerated', 'ClassifiedOutOfScope', 'Failed', 'ManualReviewRequired'];
  const activeIssues = latestStates.filter((s) => !terminalStages.includes(s.stage));

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-3">
          <h1 className="text-2xl font-bold text-zinc-100">Overview</h1>
          {sequentialProcessing && (
            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-amber-900/50 text-amber-300 border border-amber-700">
              Sequential Mode
            </span>
          )}
        </div>
        <div className="flex items-center gap-2">
          <span
            className={`inline-block w-2.5 h-2.5 rounded-full ${isConnected ? 'bg-emerald-400' : 'bg-red-400'}`}
          />
          <span className="text-xs text-zinc-400">
            {isConnected ? 'Connected' : 'Disconnected'}
          </span>
        </div>
      </div>

      {/* Stats cards */}
      <div className="grid grid-cols-3 gap-4 mb-4">
        <div className="bg-zinc-800 rounded-lg p-3 border border-zinc-700">
          <p className="text-zinc-400 text-xs">Total Issues</p>
          <p className="text-xl font-bold text-zinc-100">{totalIssues}</p>
        </div>
        <div className="bg-zinc-800 rounded-lg p-3 border border-zinc-700">
          <p className="text-zinc-400 text-xs">Active Agents</p>
          <p className="text-xl font-bold text-zinc-100">{activeAgents}</p>
        </div>
        <div className="bg-zinc-800 rounded-lg p-3 border border-zinc-700">
          <p className="text-zinc-400 text-xs">Recent Failures</p>
          <p className="text-xl font-bold text-zinc-100">{recentFailures}</p>
        </div>
      </div>

      {/* Pipeline status panel — always visible */}
      <div className="bg-zinc-800/60 rounded-lg border border-zinc-700 p-4 mb-4">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-sm font-semibold text-zinc-300 uppercase tracking-wide">
            Pipeline Status
          </h2>
          <Link
            to="/events"
            className="text-xs text-blue-400 hover:text-blue-300 transition-colors"
          >
            View Event Log →
          </Link>
        </div>

        {activeIssues.length === 0 ? (
          <p className="text-zinc-500 text-sm italic">
            No issues in progress — pipeline idle
          </p>
        ) : (
          <div className="space-y-2">
            {activeIssues.map((issue) => (
              <div
                key={issue.issueId}
                className="flex items-start gap-3 bg-zinc-900/60 rounded-md px-3 py-2.5"
              >
                <span className="font-mono text-xs text-zinc-500 shrink-0 mt-0.5">
                  {issue.issueId.slice(0, 8)}
                </span>
                <span className={`inline-block px-1.5 py-0.5 rounded text-xs font-medium shrink-0 ${getStageBadgeClasses(issue.stage)}`}>
                  {issue.stage}
                </span>
                <div className="flex flex-col gap-0.5 min-w-0 flex-1">
                  {issue.subject && (
                    <span className="text-sm text-zinc-100 font-medium truncate">
                      {issue.subject}
                    </span>
                  )}
                  <span className="text-xs text-zinc-400 truncate">
                    {issue.detail || '—'}
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Main content: graph + email composer */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 flex-1 min-h-0">
        <div className="lg:col-span-2 bg-zinc-900/40 rounded-lg border border-zinc-700 overflow-hidden">
          <PipelineVisualizer activeIssues={activeIssues} />
        </div>
        <div className="overflow-y-auto">
          <EmailComposer />
        </div>
      </div>
    </div>
  );
}
