import type { AgentStatus, ApiError, InboxMessage, IncomingEmail, StateTransitionEvent, WorkflowState } from '../types';

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
