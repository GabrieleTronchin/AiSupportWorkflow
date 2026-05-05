import { useAgents } from '../hooks/useAgents';
import { AgentMonitor } from '../components/AgentMonitor';

export function AgentsPage() {
  const { agents, isLoading, error, retry } = useAgents();

  return (
    <div>
      <h1 className="text-2xl font-bold text-zinc-100 mb-6">Agents</h1>

      {error ? (
        <div className="bg-red-900/20 border border-red-700 rounded-lg p-6 text-center">
          <p className="text-red-400 font-medium mb-2">
            Error {error.statusCode}: {error.message}
          </p>
          <button
            onClick={retry}
            className="mt-2 px-4 py-2 bg-red-600 hover:bg-red-500 text-white rounded text-sm font-medium transition-colors"
          >
            Retry
          </button>
        </div>
      ) : !isLoading && agents.length === 0 ? (
        <div className="bg-zinc-800 border border-zinc-700 rounded-lg p-6 text-center">
          <p className="text-zinc-300 font-medium mb-2">No agents configured</p>
          <p className="text-zinc-500 text-sm">
            Enable <code className="text-zinc-400 bg-zinc-700 px-1 rounded">EnableVisualization</code> in the configuration to activate agents.
          </p>
        </div>
      ) : (
        <AgentMonitor agents={agents} isLoading={isLoading} />
      )}
    </div>
  );
}
