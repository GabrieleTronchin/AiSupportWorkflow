import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { submitEmail, fetchIssues, fetchIssue, fetchAgents } from '../api/client';
import type { IncomingEmail, WorkflowState, AgentStatus } from '../types';

const mockFetch = vi.fn();

beforeEach(() => {
  vi.stubGlobal('fetch', mockFetch);
});

afterEach(() => {
  vi.restoreAllMocks();
});

function okResponse(data: unknown): Response {
  return {
    ok: true,
    status: 200,
    json: () => Promise.resolve(data),
  } as unknown as Response;
}

function errorResponse(status: number, body?: unknown): Response {
  return {
    ok: false,
    status,
    json: body !== undefined ? () => Promise.resolve(body) : () => Promise.reject(new Error('no body')),
  } as unknown as Response;
}

describe('submitEmail', () => {
  const email: IncomingEmail = {
    sender: 'user@example.com',
    subject: 'Bug in Application A',
    body: 'There is a backend bug in the order service.',
  };

  it('sends POST request with correct URL, method, headers, and body', async () => {
    mockFetch.mockResolvedValue(okResponse({ id: '123' }));

    await submitEmail(email);

    expect(mockFetch).toHaveBeenCalledWith('/api/support/emails', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(email),
    });
  });

  it('returns parsed JSON on success', async () => {
    const responseData = { id: 'abc-123' };
    mockFetch.mockResolvedValue(okResponse(responseData));

    const result = await submitEmail(email);

    expect(result).toEqual(responseData);
  });

  it('throws ApiError on non-ok response with message in body', async () => {
    mockFetch.mockResolvedValue(errorResponse(400, { message: 'Invalid email format' }));

    await expect(submitEmail(email)).rejects.toEqual({
      statusCode: 400,
      message: 'Invalid email format',
    });
  });

  it('throws ApiError with fallback message when body has no message field', async () => {
    mockFetch.mockResolvedValue(errorResponse(500, { error: 'something went wrong' }));

    await expect(submitEmail(email)).rejects.toEqual({
      statusCode: 500,
      message: 'Request failed with status 500',
    });
  });

  it('throws ApiError with fallback message when body fails to parse as JSON', async () => {
    mockFetch.mockResolvedValue(errorResponse(502));

    await expect(submitEmail(email)).rejects.toEqual({
      statusCode: 502,
      message: 'Request failed with status 502',
    });
  });
});

describe('fetchIssues', () => {
  const mockIssues: WorkflowState[] = [
    { issueId: 'issue-1', stage: 'Received', lastUpdated: '2024-01-01T00:00:00Z', detail: null },
    { issueId: 'issue-2', stage: 'Classified', lastUpdated: '2024-01-02T00:00:00Z', detail: 'Code related' },
  ];

  it('sends GET request to correct URL', async () => {
    mockFetch.mockResolvedValue(okResponse(mockIssues));

    await fetchIssues();

    expect(mockFetch).toHaveBeenCalledWith('/api/support/issues');
  });

  it('returns parsed WorkflowState array on success', async () => {
    mockFetch.mockResolvedValue(okResponse(mockIssues));

    const result = await fetchIssues();

    expect(result).toEqual(mockIssues);
  });

  it('throws ApiError on non-ok response with message in body', async () => {
    mockFetch.mockResolvedValue(errorResponse(503, { message: 'Service unavailable' }));

    await expect(fetchIssues()).rejects.toEqual({
      statusCode: 503,
      message: 'Service unavailable',
    });
  });

  it('throws ApiError with fallback message when body has no message field', async () => {
    mockFetch.mockResolvedValue(errorResponse(404, { detail: 'not found' }));

    await expect(fetchIssues()).rejects.toEqual({
      statusCode: 404,
      message: 'Request failed with status 404',
    });
  });

  it('throws ApiError with fallback message when body fails to parse', async () => {
    mockFetch.mockResolvedValue(errorResponse(500));

    await expect(fetchIssues()).rejects.toEqual({
      statusCode: 500,
      message: 'Request failed with status 500',
    });
  });
});

describe('fetchIssue', () => {
  const mockIssue: WorkflowState = {
    issueId: 'issue-42',
    stage: 'Resolving',
    lastUpdated: '2024-03-15T10:30:00Z',
    detail: 'Agent working on resolution',
  };

  it('sends GET request to correct URL with issue ID', async () => {
    mockFetch.mockResolvedValue(okResponse(mockIssue));

    await fetchIssue('issue-42');

    expect(mockFetch).toHaveBeenCalledWith('/api/support/issues/issue-42');
  });

  it('returns parsed WorkflowState on success', async () => {
    mockFetch.mockResolvedValue(okResponse(mockIssue));

    const result = await fetchIssue('issue-42');

    expect(result).toEqual(mockIssue);
  });

  it('throws ApiError on non-ok response with message in body', async () => {
    mockFetch.mockResolvedValue(errorResponse(404, { message: 'Issue not found' }));

    await expect(fetchIssue('nonexistent')).rejects.toEqual({
      statusCode: 404,
      message: 'Issue not found',
    });
  });

  it('throws ApiError with fallback message when body has no message field', async () => {
    mockFetch.mockResolvedValue(errorResponse(403, {}));

    await expect(fetchIssue('forbidden-id')).rejects.toEqual({
      statusCode: 403,
      message: 'Request failed with status 403',
    });
  });

  it('throws ApiError with fallback message when body fails to parse', async () => {
    mockFetch.mockResolvedValue(errorResponse(500));

    await expect(fetchIssue('error-id')).rejects.toEqual({
      statusCode: 500,
      message: 'Request failed with status 500',
    });
  });
});

describe('fetchAgents', () => {
  const mockAgents: AgentStatus[] = [
    { agentId: 'TeamA_BackendDeveloper', team: 'TeamA', role: 'BackendDeveloper', status: 'Idle', lastAction: null },
    { agentId: 'TeamB_FrontendDeveloper', team: 'TeamB', role: 'FrontendDeveloper', status: 'Working', lastAction: 'Analyzing root cause' },
  ];

  it('sends GET request to correct URL', async () => {
    mockFetch.mockResolvedValue(okResponse(mockAgents));

    await fetchAgents();

    expect(mockFetch).toHaveBeenCalledWith('/api/support/agents');
  });

  it('returns parsed AgentStatus array on success', async () => {
    mockFetch.mockResolvedValue(okResponse(mockAgents));

    const result = await fetchAgents();

    expect(result).toEqual(mockAgents);
  });

  it('throws ApiError on non-ok response with message in body', async () => {
    mockFetch.mockResolvedValue(errorResponse(500, { message: 'Internal server error' }));

    await expect(fetchAgents()).rejects.toEqual({
      statusCode: 500,
      message: 'Internal server error',
    });
  });

  it('throws ApiError with fallback message when body has no message field', async () => {
    mockFetch.mockResolvedValue(errorResponse(429, { retryAfter: 60 }));

    await expect(fetchAgents()).rejects.toEqual({
      statusCode: 429,
      message: 'Request failed with status 429',
    });
  });

  it('throws ApiError with fallback message when body fails to parse', async () => {
    mockFetch.mockResolvedValue(errorResponse(502));

    await expect(fetchAgents()).rejects.toEqual({
      statusCode: 502,
      message: 'Request failed with status 502',
    });
  });
});
