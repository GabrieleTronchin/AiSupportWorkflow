export type WorkflowStage =
  | 'Received'
  | 'Classified'
  | 'ClassifiedOutOfScope'
  | 'TeamAssigned'
  | 'AgentAssigned'
  | 'Resolving'
  | 'Resolved'
  | 'AwaitingApproval'
  | 'CodeChangeGenerated'
  | 'Failed'
  | 'ManualReviewRequired';

/**
 * Every WorkflowStage in pipeline order. The Record keyed by WorkflowStage makes
 * the compiler fail if a stage is added to the union but not listed here.
 */
const STAGE_ORDER: Record<WorkflowStage, number> = {
  Received: 0,
  Classified: 1,
  ClassifiedOutOfScope: 2,
  TeamAssigned: 3,
  AgentAssigned: 4,
  Resolving: 5,
  Resolved: 6,
  AwaitingApproval: 7,
  CodeChangeGenerated: 8,
  Failed: 9,
  ManualReviewRequired: 10,
};

export const WORKFLOW_STAGES: readonly WorkflowStage[] = (Object.keys(STAGE_ORDER) as WorkflowStage[])
  .sort((a, b) => STAGE_ORDER[a] - STAGE_ORDER[b]);

export interface WorkflowState {
  issueId: string;
  stage: WorkflowStage;
  lastUpdated: string; // ISO 8601
  detail: string | null;
  subject?: string | null;
}

export interface AgentStatus {
  agentId: string;
  team: string;
  role: string;
  status: 'Idle' | 'Working';
  lastAction: string | null;
  currentIssueId: string | null;
  currentSubject: string | null;
  currentStage: WorkflowStage | null;
}

export interface IncomingEmail {
  sender: string;
  subject: string;
  body: string;
}

export interface StateTransitionEvent {
  id: string;
  issueId: string;
  previousStage: WorkflowStage | null;
  newStage: WorkflowStage;
  timestamp: string; // ISO 8601
  detail: string | null;
}

export type InboxStatus = 'queued' | 'processed' | 'failed';

export interface InboxMessage {
  id: string;
  messageType: string;
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

export interface AgentTelemetry {
  agentId: string;
  totalPromptTokens: number;
  totalCompletionTokens: number;
  totalCalls: number;
  averageLatencyMs: number;
  lastCall: LlmCallDetail | null;
}

export interface LlmCallDetail {
  modelName: string;
  promptTokens: number;
  completionTokens: number;
  latencyMs: number;
  success: boolean;
  timestamp: string;
}

export interface TelemetrySummary {
  totalTokens: number;
  totalCalls: number;
  averageLatencyMs: number;
  errorRate: number;
}

export interface PendingApproval {
  issueId: string;
  report: {
    issueId: string;
    rootCauseDescription: string;
    affectedComponent: string;
    severityAssessment: string;
    proposedFixSummary: string;
    requiresEscalation: boolean;
    escalationReason: string | null;
  };
}
