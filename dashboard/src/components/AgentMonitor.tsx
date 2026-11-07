import type { AgentStatus } from '../types';
import { getAgentStatusBadgeClasses } from '../utils/badges';

/**
 * Describes the agent's current activity from its status and current run.
 * Exported for testability (property tests).
 */
export function renderAgentActivity(agent: AgentStatus, stepName?: (stepId: string) => string): string {
  if (agent.status === 'Working' && agent.currentRunId) {
    const parts: string[] = [`Run: ${agent.currentRunId}`];
    if (agent.currentRunTitle) {
      parts.push(`Title: ${agent.currentRunTitle}`);
    }
    if (agent.currentStepId) {
      parts.push(`Step: ${stepName ? stepName(agent.currentStepId) : agent.currentStepId}`);
    }
    return parts.join(' | ');
  }
  return 'No recent activity';
}

interface AgentMonitorProps {
  agents: AgentStatus[];
  isLoading: boolean;
  stepName?: (agent: AgentStatus, stepId: string) => string;
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

export function AgentMonitor({ agents, isLoading, stepName }: AgentMonitorProps) {
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
        <div key={`${agent.moduleId}/${agent.agentId}`} className="bg-zinc-800 rounded-lg p-4 border border-zinc-700">
          <div className="flex items-center justify-between mb-2">
            <span className="font-medium text-zinc-100">{agent.agentId}</span>
            <span className={`inline-block px-2 py-0.5 rounded text-xs font-medium ${getAgentStatusBadgeClasses(agent.status)}`}>
              {agent.status}
            </span>
          </div>
          <p className="text-zinc-400 text-xs mb-1">
            {agent.name}
            {agent.group && (
              <>
                {' · '}<span className="text-zinc-500">Group:</span> {agent.group}
              </>
            )}
            {agent.role && (
              <>
                {' · '}<span className="text-zinc-500">Role:</span> {agent.role}
              </>
            )}
          </p>
          <p className="text-zinc-400 text-sm">
            {renderAgentActivity(agent, stepName ? (stepId) => stepName(agent, stepId) : undefined)}
          </p>
        </div>
      ))}
    </div>
  );
}
