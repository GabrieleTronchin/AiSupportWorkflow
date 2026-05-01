import type { AgentStatus } from '../types';

/**
 * Returns Tailwind classes for an agent status badge based on its value.
 */
export function getStatusBadgeClasses(status: string): string {
  switch (status) {
    case 'Idle':
      return 'text-emerald-400 bg-emerald-400/10';
    case 'Working':
      return 'text-amber-400 bg-amber-400/10';
    default:
      return 'text-zinc-400 bg-zinc-400/10';
  }
}

interface AgentMonitorProps {
  agents: AgentStatus[];
  isLoading: boolean;
}

export function AgentMonitor({ agents, isLoading }: AgentMonitorProps) {
  if (isLoading) {
    return <p className="text-zinc-400">Loading agents...</p>;
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
              className={`inline-block px-2 py-0.5 rounded text-xs font-medium ${getStatusBadgeClasses(agent.status)}`}
            >
              {agent.status}
            </span>
          </div>
          <p className="text-zinc-400 text-xs mb-1">
            <span className="text-zinc-500">Team:</span> {agent.team} · <span className="text-zinc-500">Role:</span> {agent.role}
          </p>
          <p className="text-zinc-400 text-sm">
            {agent.lastAction ?? 'No recent activity'}
          </p>
        </div>
      ))}
    </div>
  );
}
