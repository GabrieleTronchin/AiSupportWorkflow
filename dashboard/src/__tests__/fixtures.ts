import type { AgentStatus, RunEvent, RunSummary, RunUpdate, WorkflowGraph, WorkflowInfo } from '../types';

/** A workflow shaped like the support pipeline (linear path with branches to terminals). */
export const graph: WorkflowGraph = {
  start: 'classify',
  nodes: [
    { id: 'classify', name: 'Classification', kind: 'llm', outcome: null },
    { id: 'route_team', name: 'Team routing', kind: 'rule', outcome: null },
    { id: 'assign_agent', name: 'Agent assignment', kind: 'map', outcome: null },
    { id: 'resolve', name: 'Root cause analysis', kind: 'llm', outcome: null },
    { id: 'approval', name: 'Human approval', kind: 'human-input', outcome: null },
    { id: 'generate_code', name: 'Code generation', kind: 'llm', outcome: null },
    { id: 'code_change_generated', name: 'Code change generated', kind: 'terminal', outcome: 'success' },
    { id: 'classified_out_of_scope', name: 'Out of scope', kind: 'terminal', outcome: 'success' },
    { id: 'manual_review_required', name: 'Manual review required', kind: 'terminal', outcome: 'failure' },
  ],
  edges: [
    { from: 'classify', to: 'route_team', label: 'steps.classify.isCodeRelated' },
    { from: 'classify', to: 'classified_out_of_scope', label: null },
    { from: 'route_team', to: 'assign_agent', label: null },
    { from: 'assign_agent', to: 'resolve', label: null },
    { from: 'resolve', to: 'approval', label: null },
    { from: 'approval', to: 'generate_code', label: 'steps.approval.decision == "approved"' },
    { from: 'approval', to: 'manual_review_required', label: null },
    { from: 'generate_code', to: 'code_change_generated', label: null },
  ],
};

/** The main path of the fixture graph, in order. */
export const mainPath = ['classify', 'route_team', 'assign_agent', 'resolve', 'approval', 'generate_code', 'code_change_generated'];

export const workflow: WorkflowInfo = {
  id: 'support-email',
  moduleId: 'support',
  name: 'Support email',
  description: 'Support pipeline',
  version: 1,
  available: true,
  inputSchema: {
    type: 'object',
    required: ['subject', 'body'],
    properties: {
      sender: { type: 'string', title: 'Sender' },
      subject: { type: 'string', title: 'Subject', minLength: 1, pattern: '\\S' },
      body: { type: 'string', title: 'Body', minLength: 1, pattern: '\\S', 'x-multiline': true },
    },
  },
  graph,
  samples: [
    {
      id: 'scenario-a1',
      name: 'A1: NullReferenceException',
      group: 'Application A',
      description: null,
      workflowId: 'support-email',
      input: { sender: 'dev.team@company.com', subject: 'NullReferenceException in OrderController - Application A', body: 'The API crashes.' },
      expectInvalid: false,
    },
    {
      id: 'scenario-b1',
      name: 'B1: SQL Injection',
      group: 'Application B',
      description: null,
      workflowId: 'support-email',
      input: { sender: 'security.team@company.com', subject: 'SQL Injection in UserController - Application B', body: 'Critical vulnerability.' },
      expectInvalid: false,
    },
    {
      id: 'edge-empty-input',
      name: 'Empty Input (tests validation)',
      group: 'Edge Cases',
      description: null,
      workflowId: 'support-email',
      input: { sender: 'tester@company.com', subject: '   ', body: '   ' },
      expectInvalid: true,
    },
  ],
};

export function run(overrides: Partial<RunSummary> = {}): RunSummary {
  return {
    id: 'run-00000000-0000',
    moduleId: 'support',
    workflowId: 'support-email',
    workflowVersion: 1,
    title: 'A support email',
    status: 'Running',
    currentStepId: 'classify',
    detail: null,
    assignedAgentId: null,
    createdAt: '2024-01-01T00:00:00Z',
    lastUpdated: '2024-01-01T00:00:00Z',
    ...overrides,
  };
}

export function update(overrides: Partial<RunUpdate> = {}): RunUpdate {
  return {
    runId: 'run-00000000-0000',
    moduleId: 'support',
    workflowId: 'support-email',
    stepId: 'classify',
    status: 'Running',
    eventKind: 'StepStarted',
    timestamp: '2024-01-01T00:00:01Z',
    detail: null,
    title: null,
    ...overrides,
  };
}

export function event(overrides: Partial<RunEvent> = {}): RunEvent {
  return {
    id: 'event-1',
    runId: 'run-00000000-0000',
    moduleId: 'support',
    workflowId: 'support-email',
    kind: 'StepCompleted',
    previousStepId: null,
    stepId: 'classify',
    status: 'Running',
    timestamp: new Date().toISOString(),
    detail: null,
    ...overrides,
  };
}

export function agent(overrides: Partial<AgentStatus> = {}): AgentStatus {
  return {
    agentId: 'TeamA_BackendDeveloper',
    moduleId: 'support',
    name: 'Backend developer',
    group: 'TeamA',
    role: 'BackendDeveloper',
    status: 'Idle',
    currentRunId: null,
    currentRunTitle: null,
    currentStepId: null,
    ...overrides,
  };
}
