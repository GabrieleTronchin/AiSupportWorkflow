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
  status: string;
  lastAction: string | null;
}

export interface IncomingEmail {
  sender: string;
  subject: string;
  body: string;
}

export interface ApiError {
  statusCode: number;
  message: string;
}
