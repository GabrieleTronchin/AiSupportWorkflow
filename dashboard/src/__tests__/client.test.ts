import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  abortRun,
  approveRun,
  extractErrorMessage,
  fetchAgents,
  fetchAgentTelemetry,
  fetchConfig,
  fetchEvents,
  fetchHumanInputs,
  fetchInbox,
  fetchModules,
  fetchRun,
  fetchRuns,
  fetchTelemetrySummary,
  fetchWorkflows,
  rejectRun,
  submitRun,
} from '../api/client';
import { agent, run, workflow } from './fixtures';

const mockFetch = vi.fn();

beforeEach(() => {
  vi.clearAllMocks();
  vi.stubGlobal('fetch', mockFetch);
});

afterEach(() => {
  vi.restoreAllMocks();
});

function okResponse(data: unknown, status = 200): Response {
  return { ok: true, status, json: () => Promise.resolve(data) } as unknown as Response;
}

function errorResponse(status: number, body?: unknown): Response {
  return {
    ok: false,
    status,
    json: body !== undefined ? () => Promise.resolve(body) : () => Promise.reject(new Error('no body')),
  } as unknown as Response;
}

describe('submitRun', () => {
  const input = { sender: 'user@example.com', subject: 'Bug in Application A', body: 'Backend bug.' };

  it('posts the input to the workflow intake', async () => {
    mockFetch.mockResolvedValue(okResponse({ messageId: 'm1', runId: 'r1' }, 202));

    const result = await submitRun('support-email', input);

    expect(mockFetch).toHaveBeenCalledWith('/api/engine/workflows/support-email/runs', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(input),
    });
    expect(result).toEqual({ messageId: 'm1', runId: 'r1' });
  });

  it('encodes the workflow id', async () => {
    mockFetch.mockResolvedValue(okResponse({ messageId: 'm1', runId: 'r1' }, 202));

    await submitRun('a b', {});

    expect(mockFetch.mock.calls[0][0]).toBe('/api/engine/workflows/a%20b/runs');
  });

  it('throws the problem detail on 400', async () => {
    mockFetch.mockResolvedValue(errorResponse(400, { title: 'Invalid input', detail: 'Subject and Body are required.' }));

    await expect(submitRun('support-email', input)).rejects.toEqual({ statusCode: 400, message: 'Subject and Body are required.' });
  });

  it('throws a fallback message when the body is not JSON', async () => {
    mockFetch.mockResolvedValue(errorResponse(502));

    await expect(submitRun('support-email', input)).rejects.toEqual({ statusCode: 502, message: 'Request failed with status 502' });
  });
});

describe('extractErrorMessage', () => {
  it.each([
    [{ detail: 'Problem detail' }, 'Problem detail'],
    [{ message: 'Message' }, 'Message'],
    [{ error: 'Legacy error' }, 'Legacy error'],
    [{ title: 'Only title' }, 'Only title'],
    [{ detail: '  ', message: 'Fallback to message' }, 'Fallback to message'],
    [{ retryAfter: 60 }, 'Request failed with status 418'],
    [null, 'Request failed with status 418'],
    ['text', 'Request failed with status 418'],
  ])('extracts %j', (body, expected) => {
    expect(extractErrorMessage(body, 418)).toBe(expected);
  });
});

describe('read endpoints', () => {
  it.each([
    ['fetchConfig', () => fetchConfig(), '/api/engine/config'],
    ['fetchModules', () => fetchModules(), '/api/engine/modules'],
    ['fetchWorkflows', () => fetchWorkflows(), '/api/engine/workflows'],
    ['fetchWorkflows(moduleId)', () => fetchWorkflows('support'), '/api/engine/workflows?moduleId=support'],
    ['fetchRuns', () => fetchRuns(), '/api/engine/runs'],
    ['fetchRuns(filters)', () => fetchRuns({ workflowId: 'support-email', status: 'Failed' }), '/api/engine/runs?workflowId=support-email&status=Failed'],
    ['fetchRun', () => fetchRun('r1'), '/api/engine/runs/r1'],
    ['fetchHumanInputs', () => fetchHumanInputs(), '/api/engine/human-inputs'],
    ['fetchEvents', () => fetchEvents(), '/api/engine/events'],
    ['fetchAgents', () => fetchAgents(), '/api/engine/agents'],
    ['fetchAgentTelemetry', () => fetchAgentTelemetry('TeamA_BackendDeveloper'), '/api/engine/agents/TeamA_BackendDeveloper/telemetry'],
    ['fetchTelemetrySummary', () => fetchTelemetrySummary(), '/api/engine/telemetry/summary'],
    ['fetchInbox', () => fetchInbox(), '/api/engine/inbox'],
  ])('%s calls %s', async (_name, call, url) => {
    mockFetch.mockResolvedValue(okResponse([]));

    await call();

    expect(mockFetch).toHaveBeenCalledWith(url);
  });

  it('returns the parsed body', async () => {
    mockFetch.mockResolvedValueOnce(okResponse([run()])).mockResolvedValueOnce(okResponse([agent()])).mockResolvedValueOnce(okResponse([workflow]));

    expect(await fetchRuns()).toEqual([run()]);
    expect(await fetchAgents()).toEqual([agent()]);
    expect(await fetchWorkflows()).toEqual([workflow]);
  });

  it.each([
    [503, { detail: 'Module unavailable' }, 'Module unavailable'],
    [500, { message: 'Internal server error' }, 'Internal server error'],
    [429, { retryAfter: 60 }, 'Request failed with status 429'],
    [502, undefined, 'Request failed with status 502'],
  ])('throws ApiError for %i', async (status, body, message) => {
    mockFetch.mockResolvedValue(errorResponse(status, body));

    await expect(fetchRuns()).rejects.toEqual({ statusCode: status, message });
  });
});

describe('run actions', () => {
  it('approve, reject and abort post to the engine', async () => {
    mockFetch.mockResolvedValue(okResponse(null, 204));

    await approveRun('r1');
    await rejectRun('r1', 'Too risky');
    await abortRun('r1');

    expect(mockFetch).toHaveBeenNthCalledWith(1, '/api/engine/human-inputs/r1/approve', { method: 'POST' });
    expect(mockFetch).toHaveBeenNthCalledWith(2, '/api/engine/human-inputs/r1/reject', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ reason: 'Too risky' }),
    });
    expect(mockFetch).toHaveBeenNthCalledWith(3, '/api/engine/runs/r1/abort', { method: 'POST' });
  });

  it.each([
    ['approve', () => approveRun('r1')],
    ['reject', () => rejectRun('r1')],
    ['abort', () => abortRun('r1')],
  ])('%s throws on 409 with the problem detail', async (_name, call) => {
    mockFetch.mockResolvedValue(errorResponse(409, { detail: 'Run r1 has already completed (Completed).' }));

    await expect(call()).rejects.toEqual({ statusCode: 409, message: 'Run r1 has already completed (Completed).' });
  });
});
