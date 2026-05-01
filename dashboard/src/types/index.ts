export type WorkflowStage =
  | 'Received'
  | 'Classified'
  | 'ClassifiedOutOfScope'
  | 'TeamAssigned'
  | 'AgentAssigned'
  | 'Resolving'
  | 'Resolved'
  | 'CodeChangeGenerated'
  | 'Failed'
  | 'ManualReviewRequired';

export interface WorkflowState {
  issueId: string;
  stage: WorkflowStage;
  lastUpdated: string; // ISO 8601
  detail: string | null;
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
