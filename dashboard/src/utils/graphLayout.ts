import dagre from '@dagrejs/dagre';
import type { RunStatus, RunSummary, WorkflowGraph } from '../types';

export const NODE_WIDTH = 170;
export const NODE_HEIGHT = 46;

export interface PositionedNode {
  id: string;
  x: number;
  y: number;
}

/** Left-to-right automatic layout of a workflow graph (top-left node positions). */
export function layoutGraph(graph: WorkflowGraph): PositionedNode[] {
  const g = new dagre.graphlib.Graph();
  g.setGraph({ rankdir: 'LR', nodesep: 30, ranksep: 70, marginx: 10, marginy: 10 });
  g.setDefaultEdgeLabel(() => ({}));

  for (const node of graph.nodes) {
    g.setNode(node.id, { width: NODE_WIDTH, height: NODE_HEIGHT });
  }
  for (const edge of graph.edges) {
    if (g.hasNode(edge.from) && g.hasNode(edge.to) && edge.from !== edge.to) {
      g.setEdge(edge.from, edge.to);
    }
  }

  dagre.layout(g);

  return graph.nodes.map((node) => {
    const position = g.node(node.id);
    return { id: node.id, x: position.x - NODE_WIDTH / 2, y: position.y - NODE_HEIGHT / 2 };
  });
}

/** Short, readable edge label from a transition condition. */
export function formatEdgeLabel(label: string | null, isOnlyEdge: boolean): string | undefined {
  if (!label) {
    return isOnlyEdge ? undefined : 'otherwise';
  }

  const comparison = label.match(/==\s*["']?([^"']+)["']?\s*$/);
  if (comparison) {
    return comparison[1].trim();
  }

  const last = label.split('.').pop() ?? label;
  return last.length > 24 ? `${last.slice(0, 21)}…` : last;
}

/** Steps from which <paramref name="target"/> is reachable (its ancestors). */
export function ancestorsOf(graph: WorkflowGraph, target: string): Set<string> {
  const parents = new Map<string, string[]>();
  for (const edge of graph.edges) {
    parents.set(edge.to, [...(parents.get(edge.to) ?? []), edge.from]);
  }

  const visited = new Set<string>();
  const stack = [...(parents.get(target) ?? [])];
  while (stack.length > 0) {
    const id = stack.pop()!;
    if (id === target || visited.has(id)) continue;
    visited.add(id);
    stack.push(...(parents.get(id) ?? []));
  }
  return visited;
}

export type NodeState = 'idle' | 'completed' | 'active' | 'awaiting' | 'failed';

/**
 * State of each step for the active runs: the current step is active (awaiting when the run waits
 * for input, failed when the run failed there) and the steps leading to it are completed.
 */
export function computeNodeStates(graph: WorkflowGraph, runs: Pick<RunSummary, 'currentStepId' | 'status'>[]): Map<string, NodeState> {
  const states = new Map<string, NodeState>(graph.nodes.map((n) => [n.id, 'idle']));
  const rank: Record<NodeState, number> = { idle: 0, completed: 1, active: 2, awaiting: 3, failed: 4 };
  const promote = (id: string, state: NodeState) => {
    if (states.has(id) && rank[state] > rank[states.get(id)!]) {
      states.set(id, state);
    }
  };

  for (const run of runs) {
    if (!run.currentStepId || !states.has(run.currentStepId)) continue;
    for (const ancestor of ancestorsOf(graph, run.currentStepId)) {
      promote(ancestor, 'completed');
    }
    promote(run.currentStepId, currentState(run.status));
  }

  return states;
}

function currentState(status: RunStatus): NodeState {
  switch (status) {
    case 'AwaitingInput':
      return 'awaiting';
    case 'Failed':
    case 'Cancelled':
      return 'failed';
    case 'Completed':
      return 'completed';
    default:
      return 'active';
  }
}
