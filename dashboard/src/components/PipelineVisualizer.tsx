import { useMemo } from 'react';
import { ReactFlow, Controls, MarkerType, Position, type Node, type Edge } from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import type { RunSummary, WorkflowGraph } from '../types';
import { computeNodeStates, formatEdgeLabel, layoutGraph, NODE_HEIGHT, NODE_WIDTH, type NodeState } from '../utils/graphLayout';

interface PipelineVisualizerProps {
  graph: WorkflowGraph;
  /** Runs shown on the graph (usually the non-terminal runs of the workflow). */
  activeRuns: Pick<RunSummary, 'id' | 'currentStepId' | 'status'>[];
}

/** Colors of a node for its state; terminal nodes keep a border colored by outcome. */
export function getNodeStyle(state: NodeState, outcome: 'success' | 'failure' | null): React.CSSProperties {
  const base: React.CSSProperties = {
    color: '#fff',
    borderRadius: '8px',
    padding: '6px 8px',
    width: NODE_WIDTH,
    height: NODE_HEIGHT,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    textAlign: 'center',
    fontSize: '12px',
    fontWeight: 600,
    lineHeight: 1.2,
    border: `1px solid ${outcome === 'success' ? '#10b981' : outcome === 'failure' ? '#ef4444' : '#52525b'}`,
  };

  switch (state) {
    case 'active':
      return { ...base, background: '#3b82f6', boxShadow: '0 0 12px 4px rgba(59, 130, 246, 0.5)', animation: 'pulse 2s infinite' };
    case 'awaiting':
      return { ...base, background: '#f59e0b', boxShadow: '0 0 12px 4px rgba(245, 158, 11, 0.5)', animation: 'pulse 2s infinite' };
    case 'failed':
      return { ...base, background: '#ef4444', boxShadow: '0 0 12px 4px rgba(239, 68, 68, 0.5)' };
    case 'completed':
      return { ...base, background: '#10b981' };
    default:
      return { ...base, background: '#3f3f46' };
  }
}

export function buildFlow(graph: WorkflowGraph, activeRuns: PipelineVisualizerProps['activeRuns']): { nodes: Node[]; edges: Edge[] } {
  const positions = new Map(layoutGraph(graph).map((p) => [p.id, p]));
  const states = computeNodeStates(graph, activeRuns);
  const outgoing = new Map<string, number>();
  for (const edge of graph.edges) {
    outgoing.set(edge.from, (outgoing.get(edge.from) ?? 0) + 1);
  }

  const nodes: Node[] = graph.nodes.map((node) => {
    const position = positions.get(node.id) ?? { x: 0, y: 0 };
    return {
      id: node.id,
      position: { x: position.x, y: position.y },
      data: { label: node.name },
      sourcePosition: Position.Right,
      targetPosition: Position.Left,
      style: getNodeStyle(states.get(node.id) ?? 'idle', node.outcome),
      ariaLabel: `${node.name} (${node.kind})`,
    };
  });

  const edges: Edge[] = graph.edges.map((edge, index) => {
    const from = states.get(edge.from) ?? 'idle';
    const to = states.get(edge.to) ?? 'idle';
    const traversed = from === 'completed' && to !== 'idle';
    const target = graph.nodes.find((n) => n.id === edge.to);
    const isFailurePath = target?.outcome === 'failure';
    return {
      id: `${edge.from}-${edge.to}-${index}`,
      source: edge.from,
      target: edge.to,
      label: formatEdgeLabel(edge.label, (outgoing.get(edge.from) ?? 0) <= 1),
      labelStyle: { fill: '#d4d4d8', fontSize: 10 },
      labelBgStyle: { fill: '#18181b' },
      markerEnd: { type: MarkerType.ArrowClosed, color: '#71717a' },
      animated: traversed,
      style: {
        stroke: traversed ? '#10b981' : isFailurePath ? '#ef4444' : '#71717a',
        strokeDasharray: isFailurePath ? '5,5' : undefined,
      },
    };
  });

  return { nodes, edges };
}

/** Graph of the selected workflow, laid out automatically, with the progress of the active runs. */
export function PipelineVisualizer({ graph, activeRuns }: PipelineVisualizerProps) {
  const { nodes, edges } = useMemo(() => buildFlow(graph, activeRuns), [graph, activeRuns]);

  return (
    <div style={{ width: '100%', height: '100%', minHeight: '280px' }} data-testid="pipeline-graph">
      <style>{`
        @keyframes pulse {
          0%, 100% { opacity: 1; }
          50% { opacity: 0.7; }
        }
        .react-flow__controls {
          background: #27272a;
          border: 1px solid #3f3f46;
          border-radius: 8px;
        }
        .react-flow__controls button {
          background: #27272a;
          border-color: #3f3f46;
          color: #a1a1aa;
        }
        .react-flow__controls button:hover {
          background: #3f3f46;
        }
      `}</style>
      <ReactFlow
        key={graph.nodes.map((n) => n.id).join(',')}
        nodes={nodes}
        edges={edges}
        fitView
        fitViewOptions={{ padding: 0.15 }}
        panOnDrag
        zoomOnScroll
        zoomOnPinch
        zoomOnDoubleClick={false}
        elementsSelectable={false}
        preventScrolling={false}
        nodesDraggable={false}
        nodesConnectable={false}
        minZoom={0.2}
        maxZoom={2}
        proOptions={{ hideAttribution: true }}
      >
        <Controls showInteractive={false} />
      </ReactFlow>
    </div>
  );
}
