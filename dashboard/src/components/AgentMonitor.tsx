import type { AgentStatus } from '../types';
import { getAgentStatusBadgeClasses } from '../utils/badges';

/**
 * Returns a description of the agent's current activity based on its status and email info.
 * Exported for testability (property tests).
 */
export function renderAgentActivity(agent: AgentStatus): string {
  if (agent.status === 'Working' && agent.currentIssueId) {
    const parts: string[] = [];
    parts.push(`Issue: ${agent.currentIssueId}`);
    if (agent.currentSubject) {
      parts.push(`Subject: ${agent.currentSubject}`);
    }
    if (agent.currentStage) {
      parts.push(`Stage: ${agent.currentStage}`);
    }
    return parts.join(' | ');
  }
  return 'No recent activity';
}

interface AgentMonitorProps {
  agents: AgentStatus[];
  isLoading: boolean;
}

function SkeletonCard() {
  return (
    <div className="bg-zinc-800 rounded-lg p-4 border border-zinc-700 animate-pulse">
      <div className="flex items-center justify-between mb-2">
        <div className="h-4 w-40 bg-zinc-700 rounded" />
        <div className="h-5 w-16 bg-zinc-700 rounded" />
      </div>
      <div className="h-3 w-32 bg-zinc-700 rounded mb-2" />
      <div className="h-3 w-48 bg-zinc-700 rounded" />
    </div>
  );
}

export function AgentMonitor({ agents, isLoading }: AgentMonitorProps) {
  if (isLoading) {
    return (
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <SkeletonCard />
        <SkeletonCard />
        <SkeletonCard />
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      {agents.map((agent) => (
        <div
          key={agent.agentId}
          className="bg-zinc-800 rounded-lg p-4 border border-zinc-700"
        >
          <div className="flex items-center justify-between mb-2">
            <span className="font-medium text-zinc-100">{agent.agentId}</span>
            <span
              className={`inline-block px-2 py-0.5 rounded text-xs font-medium ${getAgentStatusBadgeClasses(agent.status)}`}
            >
              {agent.status}
            </span>
          </div>
          <p className="text-zinc-400 text-xs mb-1">
            <span className="text-zinc-500">Team:</span> {agent.team} · <span className="text-zinc-500">Role:</span> {agent.role}
          </p>
          <p className="text-zinc-400 text-sm">
            {renderAgentActivity(agent)}
          </p>
        </div>
      ))}
    </div>
  );
}
