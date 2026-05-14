import { useCallback, useEffect, useState } from 'react';
import { useAgents } from '../hooks/useAgents';
import { AgentMonitor } from '../components/AgentMonitor';
import { fetchAgentTelemetry, fetchTelemetrySummary } from '../api/client';
import type { AgentTelemetry, TelemetrySummary } from '../types';

export function calculateCost(tokens: number, ratePerThousand: number): number {
  return Math.round((tokens / 1000) * ratePerThousand * 100) / 100;
}

export function AgentsPage() {
  const { agents, isLoading, error, retry } = useAgents();
  const [telemetryMap, setTelemetryMap] = useState<Record<string, AgentTelemetry>>({});
  const [summary, setSummary] = useState<TelemetrySummary | null>(null);
  const [costRate, setCostRate] = useState(0.03); // $ per 1K tokens

  const loadTelemetry = useCallback(async () => {
    if (agents.length === 0) return;
    try {
      const [summaryData, ...agentData] = await Promise.all([
        fetchTelemetrySummary(),
        ...agents.map((a) => fetchAgentTelemetry(a.agentId).catch(() => null)),
      ]);
      setSummary(summaryData);
      const map: Record<string, AgentTelemetry> = {};
      agents.forEach((agent, i) => {
        const data = agentData[i];
        if (data) map[agent.agentId] = data;
      });
      setTelemetryMap(map);
    } catch {
      // Telemetry is best-effort; don't block the page
    }
  }, [agents]);

  useEffect(() => {
    loadTelemetry();
    const interval = setInterval(loadTelemetry, 5000);
    return () => clearInterval(interval);
  }, [loadTelemetry]);

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-zinc-100">Agents</h1>
        <div className="flex items-center gap-2">
          <label className="text-xs text-zinc-400">Cost rate ($/1K tokens):</label>
          <input
            type="number"
            min="0"
            step="0.001"
            value={costRate}
            onChange={(e) => setCostRate(Number(e.target.value))}
            className="w-20 px-2 py-1 bg-zinc-800 border border-zinc-700 rounded text-xs text-zinc-100"
          />
        </div>
      </div>

      {/* Telemetry Summary */}
      {summary && (
        <div className="grid grid-cols-4 gap-4 mb-6">
          <div className="bg-zinc-800 rounded-lg p-3 border border-zinc-700">
            <p className="text-zinc-400 text-xs">Total Tokens</p>
            <p className="text-xl font-bold text-zinc-100">{summary.totalTokens.toLocaleString()}</p>
          </div>
          <div className="bg-zinc-800 rounded-lg p-3 border border-zinc-700">
            <p className="text-zinc-400 text-xs">Total Calls</p>
            <p className="text-xl font-bold text-zinc-100">{summary.totalCalls}</p>
          </div>
          <div className="bg-zinc-800 rounded-lg p-3 border border-zinc-700">
            <p className="text-zinc-400 text-xs">Avg Latency</p>
            <p className="text-xl font-bold text-zinc-100">{summary.averageLatencyMs.toFixed(0)}ms</p>
          </div>
          <div className="bg-zinc-800 rounded-lg p-3 border border-zinc-700">
            <p className="text-zinc-400 text-xs">Est. Cost</p>
            <p className="text-xl font-bold text-emerald-400">
              ${calculateCost(summary.totalTokens, costRate)}
            </p>
          </div>
        </div>
      )}

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
        <>
          <AgentMonitor agents={agents} isLoading={isLoading} />

          {/* Per-agent telemetry details */}
          {Object.keys(telemetryMap).length > 0 && (
            <div className="mt-6">
              <h2 className="text-lg font-semibold text-zinc-200 mb-4">Agent Telemetry</h2>
              <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
                {agents.map((agent) => {
                  const telemetry = telemetryMap[agent.agentId];
                  if (!telemetry) return null;
                  const totalTokens = telemetry.totalPromptTokens + telemetry.totalCompletionTokens;
                  return (
                    <div
                      key={agent.agentId}
                      className="bg-zinc-800 rounded-lg p-4 border border-zinc-700"
                    >
                      <h3 className="text-sm font-semibold text-zinc-100 mb-3">
                        {agent.agentId}
                      </h3>
                      <div className="grid grid-cols-2 gap-2 text-xs mb-3">
                        <div>
                          <span className="text-zinc-500">Prompt Tokens:</span>{' '}
                          <span className="text-zinc-200">{telemetry.totalPromptTokens.toLocaleString()}</span>
                        </div>
                        <div>
                          <span className="text-zinc-500">Completion Tokens:</span>{' '}
                          <span className="text-zinc-200">{telemetry.totalCompletionTokens.toLocaleString()}</span>
                        </div>
                        <div>
                          <span className="text-zinc-500">Total Calls:</span>{' '}
                          <span className="text-zinc-200">{telemetry.totalCalls}</span>
                        </div>
                        <div>
                          <span className="text-zinc-500">Avg Latency:</span>{' '}
                          <span className="text-zinc-200">{telemetry.averageLatencyMs.toFixed(0)}ms</span>
                        </div>
                        <div>
                          <span className="text-zinc-500">Est. Cost:</span>{' '}
                          <span className="text-emerald-400">${calculateCost(totalTokens, costRate)}</span>
                        </div>
                      </div>

                      {telemetry.lastCall && (
                        <div className="border-t border-zinc-700 pt-2 mt-2">
                          <p className="text-xs text-zinc-500 mb-1">Last LLM Call</p>
                          <div className="grid grid-cols-2 gap-1 text-xs">
                            <div>
                              <span className="text-zinc-500">Model:</span>{' '}
                              <span className="text-zinc-200">{telemetry.lastCall.modelName}</span>
                            </div>
                            <div>
                              <span className="text-zinc-500">Tokens:</span>{' '}
                              <span className="text-zinc-200">
                                {telemetry.lastCall.promptTokens}+{telemetry.lastCall.completionTokens}
                              </span>
                            </div>
                            <div>
                              <span className="text-zinc-500">Latency:</span>{' '}
                              <span className="text-zinc-200">{telemetry.lastCall.latencyMs}ms</span>
                            </div>
                            <div>
                              <span className="text-zinc-500">Status:</span>{' '}
                              <span className={telemetry.lastCall.success ? 'text-emerald-400' : 'text-red-400'}>
                                {telemetry.lastCall.success ? 'Success' : 'Failed'}
                              </span>
                            </div>
                          </div>
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
