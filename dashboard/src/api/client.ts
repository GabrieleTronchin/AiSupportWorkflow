import type {
  AgentStatus,
  AgentTelemetry,
  ApiError,
  HumanInput,
  InboxMessage,
  IntakeAccepted,
  ModuleInfo,
  RunDetail,
  RunEvent,
  RunStatus,
  RunSummary,
  TelemetrySummary,
  WorkflowInfo,
} from '../types';

/** All dashboard calls go to the engine API through the gateway (proxied by Vite in development). */
export const ENGINE_API = '/api/engine';

/** Error message from a problem-details (`detail`/`title`) or legacy (`message`/`error`) body. */
export function extractErrorMessage(body: unknown, status: number): string {
  if (body && typeof body === 'object') {
    const record = body as Record<string, unknown>;
    for (const key of ['detail', 'message', 'error', 'title']) {
      const value = record[key];
      if (typeof value === 'string' && value.trim() !== '') {
        return value;
      }
    }
  }
  return `Request failed with status ${status}`;
}

async function toApiError(response: Response): Promise<ApiError> {
  let body: unknown = null;
  try {
    body = await response.json();
  } catch {
    // Use the default message if the body is not JSON
  }
  return { statusCode: response.status, message: extractErrorMessage(body, response.status) };
}

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw await toApiError(response);
  }
  return response.json() as Promise<T>;
}

async function ensureOk(response: Response): Promise<void> {
  if (!response.ok) {
    throw await toApiError(response);
  }
}

function query(params: Record<string, string | number | null | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== null && value !== undefined && value !== '') {
      search.set(key, String(value));
    }
  }
  const text = search.toString();
  return text ? `?${text}` : '';
}

export interface RuntimeConfig {
  sequentialProcessing: boolean;
  /** True when the engine uses simulated LLM responses (Flowgent:Llm:Provider = "Demo"). */
  demoMode: boolean;
  /** Configured LLM provider, e.g. "Demo" or "OpenAI" (null if unknown). */
  llmProvider: string | null;
  /** Model used for LLM calls, e.g. "gpt-4o-mini" (null if unknown). */
  llmModel: string | null;
}

export async function fetchConfig(): Promise<RuntimeConfig> {
  return handleResponse<RuntimeConfig>(await fetch(`${ENGINE_API}/config`));
}

export async function fetchModules(): Promise<ModuleInfo[]> {
  return handleResponse<ModuleInfo[]>(await fetch(`${ENGINE_API}/modules`));
}

export async function fetchWorkflows(moduleId?: string): Promise<WorkflowInfo[]> {
  return handleResponse<WorkflowInfo[]>(await fetch(`${ENGINE_API}/workflows${query({ moduleId })}`));
}

/** Submits an input to a workflow (async intake: 202 with message and run ids). */
export async function submitRun(workflowId: string, input: Record<string, unknown>): Promise<IntakeAccepted> {
  const response = await fetch(`${ENGINE_API}/workflows/${encodeURIComponent(workflowId)}/runs`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  });
  return handleResponse<IntakeAccepted>(response);
}

export interface RunFilters {
  moduleId?: string;
  workflowId?: string;
  status?: RunStatus;
  limit?: number;
}

export async function fetchRuns(filters: RunFilters = {}): Promise<RunSummary[]> {
  return handleResponse<RunSummary[]>(await fetch(`${ENGINE_API}/runs${query({ ...filters })}`));
}

export async function fetchRun(runId: string): Promise<RunDetail> {
  return handleResponse<RunDetail>(await fetch(`${ENGINE_API}/runs/${runId}`));
}

export async function abortRun(runId: string): Promise<void> {
  await ensureOk(await fetch(`${ENGINE_API}/runs/${runId}/abort`, { method: 'POST' }));
}

export async function fetchHumanInputs(): Promise<HumanInput[]> {
  return handleResponse<HumanInput[]>(await fetch(`${ENGINE_API}/human-inputs`));
}

export async function approveRun(runId: string): Promise<void> {
  await ensureOk(await fetch(`${ENGINE_API}/human-inputs/${runId}/approve`, { method: 'POST' }));
}

export async function rejectRun(runId: string, reason?: string): Promise<void> {
  await ensureOk(await fetch(`${ENGINE_API}/human-inputs/${runId}/reject`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason }),
  }));
}

export async function fetchEvents(): Promise<RunEvent[]> {
  return handleResponse<RunEvent[]>(await fetch(`${ENGINE_API}/events`));
}

export async function fetchAgents(): Promise<AgentStatus[]> {
  return handleResponse<AgentStatus[]>(await fetch(`${ENGINE_API}/agents`));
}

export async function fetchAgentTelemetry(agentId: string): Promise<AgentTelemetry> {
  return handleResponse<AgentTelemetry>(await fetch(`${ENGINE_API}/agents/${encodeURIComponent(agentId)}/telemetry`));
}

export async function fetchTelemetrySummary(): Promise<TelemetrySummary> {
  return handleResponse<TelemetrySummary>(await fetch(`${ENGINE_API}/telemetry/summary`));
}

export async function fetchInbox(): Promise<InboxMessage[]> {
  return handleResponse<InboxMessage[]>(await fetch(`${ENGINE_API}/inbox`));
}
