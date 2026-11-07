/** Lifecycle of a run (engine RunStatus). */
export type RunStatus = 'Queued' | 'Running' | 'AwaitingInput' | 'Completed' | 'Failed' | 'Cancelled';

/**
 * Every RunStatus in lifecycle order. The Record keyed by RunStatus makes the compiler
 * fail if a status is added to the union but not listed here.
 */
const STATUS_ORDER: Record<RunStatus, number> = {
  Queued: 0,
  Running: 1,
  AwaitingInput: 2,
  Completed: 3,
  Failed: 4,
  Cancelled: 5,
};

export const RUN_STATUSES: readonly RunStatus[] = (Object.keys(STATUS_ORDER) as RunStatus[])
  .sort((a, b) => STATUS_ORDER[a] - STATUS_ORDER[b]);

export const TERMINAL_STATUSES: readonly RunStatus[] = ['Completed', 'Failed', 'Cancelled'];

export function isTerminal(status: RunStatus): boolean {
  return TERMINAL_STATUSES.includes(status);
}

export type RunEventKind =
  | 'Queued'
  | 'StepStarted'
  | 'StepCompleted'
  | 'AwaitingInput'
  | 'InputReceived'
  | 'Completed'
  | 'Failed'
  | 'Cancelled';

export interface RunSummary {
  id: string;
  moduleId: string;
  workflowId: string;
  workflowVersion: number;
  title: string | null;
  status: RunStatus;
  currentStepId: string | null;
  detail: string | null;
  assignedAgentId: string | null;
  createdAt: string; // ISO 8601
  lastUpdated: string; // ISO 8601
}

export interface RunDetail extends RunSummary {
  input: Record<string, unknown> | null;
  stepOutputs: Record<string, unknown>;
  finalOutput: unknown;
}

/** A transition received from the gRPC stream. */
export interface RunUpdate {
  runId: string;
  moduleId: string;
  workflowId: string;
  stepId: string | null;
  status: RunStatus;
  eventKind: RunEventKind;
  timestamp: string;
  detail: string | null;
  title: string | null;
}

export interface RunEvent {
  id: string;
  runId: string;
  moduleId: string;
  workflowId: string;
  kind: RunEventKind;
  previousStepId: string | null;
  stepId: string | null;
  status: RunStatus;
  timestamp: string; // ISO 8601
  detail: string | null;
}

export type ModuleStatus = 'Loading' | 'Available' | 'Invalid' | 'Unavailable';

export interface DefinitionError {
  moduleId: string;
  file: string | null;
  stepId: string | null;
  line: number | null;
  message: string;
  severity: 'Error' | 'Warning';
}

export interface ModuleInfo {
  id: string;
  name: string;
  description: string | null;
  status: ModuleStatus;
  version: string | null;
  workflows: string[];
  errors: DefinitionError[];
  lastLoadedAt: string | null;
  lastError: string | null;
}

export type StepKind = 'llm' | 'rule' | 'map' | 'human-input' | 'tool' | 'http' | 'terminal';

export interface GraphNode {
  id: string;
  name: string;
  kind: StepKind | string;
  outcome: 'success' | 'failure' | null;
}

export interface GraphEdge {
  from: string;
  to: string;
  label: string | null;
}

export interface WorkflowGraph {
  start: string;
  nodes: GraphNode[];
  edges: GraphEdge[];
}

/** Subset of JSON Schema used to build input forms. */
export interface JsonSchema {
  type?: string | string[];
  title?: string;
  description?: string;
  properties?: Record<string, JsonSchema>;
  required?: string[];
  enum?: (string | number)[];
  minLength?: number;
  pattern?: string;
  default?: unknown;
  'x-multiline'?: boolean;
}

export interface SampleInfo {
  id: string;
  name: string;
  group: string | null;
  description: string | null;
  workflowId: string;
  input: Record<string, unknown> | null;
  expectInvalid: boolean;
}

export interface WorkflowInfo {
  id: string;
  moduleId: string;
  name: string;
  description: string | null;
  version: number;
  available: boolean;
  inputSchema: JsonSchema | null;
  graph: WorkflowGraph;
  samples: SampleInfo[];
}

export interface IntakeAccepted {
  messageId: string;
  runId: string;
}

export interface AgentStatus {
  agentId: string;
  moduleId: string;
  name: string;
  group: string | null;
  role: string | null;
  status: 'Idle' | 'Working';
  currentRunId: string | null;
  currentRunTitle: string | null;
  currentStepId: string | null;
}

export type InboxStatus = 'queued' | 'processed' | 'failed';

export interface InboxMessage {
  id: string;
  moduleId: string;
  workflowId: string;
  runId: string | null;
  receivedAt: string;
  processedAt: string | null;
  error: string | null;
  status: InboxStatus;
  payload: string | null;
}

export interface InboxStats {
  queued: number;
  processed: number;
  failed: number;
}

export interface ApiError {
  statusCode: number;
  message: string;
}

export interface LlmCallDetail {
  modelName: string;
  promptTokens: number;
  completionTokens: number;
  latencyMs: number;
  success: boolean;
  timestamp: string;
  stepId?: string | null;
  runId?: string | null;
}

export interface AgentTelemetry {
  agentId: string;
  totalPromptTokens: number;
  totalCompletionTokens: number;
  totalCalls: number;
  averageLatencyMs: number;
  lastCall: LlmCallDetail | null;
}

export interface TelemetrySummary {
  totalTokens: number;
  totalCalls: number;
  averageLatencyMs: number;
  errorRate: number;
}

export interface ReviewField {
  label: string;
  value: string | null;
}

/** A run waiting for an approve/reject decision. */
export interface HumanInput {
  runId: string;
  moduleId: string;
  workflowId: string;
  stepId: string;
  runTitle: string | null;
  review: { title: string; fields: ReviewField[] };
  requestedAt: string;
}
