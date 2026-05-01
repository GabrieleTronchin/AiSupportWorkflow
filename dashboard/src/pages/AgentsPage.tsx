import { useAgents } from '../hooks/useAgents';
import { AgentMonitor } from '../components/AgentMonitor';

export function AgentsPage() {
  const { agents, isLoading } = useAgents();

  return (
    <div>
      <h1 className="text-2xl font-bold text-zinc-100 mb-6">Agents</h1>
      <AgentMonitor agents={agents} isLoading={isLoading} />
    </div>
  );
}
