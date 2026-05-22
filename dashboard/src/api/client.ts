import type { AgentStatus, AgentTelemetry, ApiError, InboxMessage, IncomingEmail, PendingApproval, StateTransitionEvent, TelemetrySummary, WorkflowState } from '../types';

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let message = `Request failed with status ${response.status}`;
    try {
      const body = await response.json();
      if (body && typeof body.message === 'string') {
        message = body.message;
      }
    } catch {
      // Use default message if body parsing fails
    }
    const error: ApiError = { statusCode: response.status, message };
    throw error;
  }
  return response.json() as Promise<T>;
}

export async function submitEmail(email: IncomingEmail): Promise<unknown> {
  const response = await fetch('/api/support/emails', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(email),
  });
  return handleResponse<unknown>(response);
}

export async function fetchIssues(): Promise<WorkflowState[]> {
  const response = await fetch('/api/support/issues');
  return handleResponse<WorkflowState[]>(response);
}

export async function fetchIssue(id: string): Promise<WorkflowState> {
  const response = await fetch(`/api/support/issues/${id}`);
  return handleResponse<WorkflowState>(response);
}

export async function fetchAgents(): Promise<AgentStatus[]> {
  const response = await fetch('/api/support/agents');
  return handleResponse<AgentStatus[]>(response);
}

export async function fetchEvents(): Promise<StateTransitionEvent[]> {
  const response = await fetch('/api/support/events');
  return handleResponse<StateTransitionEvent[]>(response);
}

export async function fetchInbox(): Promise<InboxMessage[]> {
  const response = await fetch('/api/support/inbox');
  return handleResponse<InboxMessage[]>(response);
}

export async function fetchConfig(): Promise<{ sequentialProcessing: boolean }> {
  const response = await fetch('/api/support/config');
  return handleResponse<{ sequentialProcessing: boolean }>(response);
}

export async function fetchPendingApprovals(): Promise<PendingApproval[]> {
  const response = await fetch('/api/support/approvals/pending');
  return handleResponse<PendingApproval[]>(response);
}

export async function approveWorkflow(issueId: string): Promise<void> {
  await fetch(`/api/support/approvals/${issueId}/approve`, { method: 'POST' });
}

export async function rejectWorkflow(issueId: string, reason?: string): Promise<void> {
  await fetch(`/api/support/approvals/${issueId}/reject`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason }),
  });
}

export async function abortWorkflow(issueId: string): Promise<void> {
  const response = await fetch(`/api/support/issues/${issueId}/abort`, { method: 'POST' });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.error ?? `Failed to abort workflow ${issueId}`);
  }
}

export async function fetchAgentTelemetry(agentId: string): Promise<AgentTelemetry> {
  const response = await fetch(`/api/support/agents/${agentId}/telemetry`);
  return handleResponse<AgentTelemetry>(response);
}

export async function fetchTelemetrySummary(): Promise<TelemetrySummary> {
  const response = await fetch('/api/support/telemetry/summary');
  return handleResponse<TelemetrySummary>(response);
}
