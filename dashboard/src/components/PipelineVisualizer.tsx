import { ReactFlow, Controls, type Node, type Edge } from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { type WorkflowState, type WorkflowStage } from '../types';

interface PipelineVisualizerProps {
  activeIssues: WorkflowState[];
}

export const mainFlow: WorkflowStage[] = [
  'Received',
  'Classified',
  'TeamAssigned',
  'AgentAssigned',
  'Resolving',
  'Resolved',
  'AwaitingApproval',
  'CodeChangeGenerated',
];

export const terminalStages: WorkflowStage[] = [
  'ClassifiedOutOfScope',
  'Failed',
  'ManualReviewRequired',
];

/**
 * Determines the color/style for a pipeline node given the stage and the list of active issues.
 */
export function getNodeColor(
  stage: WorkflowStage,
  activeIssues: WorkflowState[]
): { background: string; boxShadow?: string; animation?: string } {
  const neutralGray = { background: '#3f3f46' };

  if (activeIssues.length === 0) return neutralGray;

  const activeStages = activeIssues.map((issue) => issue.stage);
  const isActiveStage = activeStages.includes(stage);

  if (terminalStages.includes(stage) && isActiveStage) {
    return { background: '#ef4444', boxShadow: '0 0 12px 4px rgba(239, 68, 68, 0.5)' };
  }

  if (stage === 'AwaitingApproval' && isActiveStage) {
    return {
      background: '#f59e0b',
      boxShadow: '0 0 12px 4px rgba(245, 158, 11, 0.5)',
      animation: 'pulse 2s infinite',
    };
  }

  if (isActiveStage) {
    return {
      background: '#3b82f6',
      boxShadow: '0 0 12px 4px rgba(59, 130, 246, 0.5)',
      animation: 'pulse 2s infinite',
    };
  }

  const stageIndex = mainFlow.indexOf(stage);
  if (stageIndex >= 0) {
    const isCompleted = activeIssues.some((issue) => {
      const issueIndex = mainFlow.indexOf(issue.stage);
      if (issueIndex > stageIndex) return true;
      if (issue.stage === 'CodeChangeGenerated' && mainFlow.includes(stage)) return true;
      return false;
    });

    if (isCompleted) {
      return { background: '#10b981' };
    }
  }

  return neutralGray;
}

function buildNodes(activeIssues: WorkflowState[]): Node[] {
  const nodeWidth = 160;
  const nodeHeight = 44;
  const xGap = 200;
  const yMain = 80;
  const yBranch = 200;

  const mainNodes: Node[] = mainFlow.map((stage, index) => {
    const colors = getNodeColor(stage, activeIssues);
    return {
      id: stage,
      position: { x: index * xGap, y: yMain },
      data: { label: stage },
      style: {
        background: colors.background,
        boxShadow: colors.boxShadow,
        animation: colors.animation,
        color: '#fff',
        border: '1px solid #52525b',
        borderRadius: '8px',
        padding: '8px',
        width: nodeWidth,
        height: nodeHeight,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: '11px',
        fontWeight: 600,
      },
    };
  });

  const branchConfig: { stage: WorkflowStage; parentStage: WorkflowStage }[] = [
    { stage: 'ClassifiedOutOfScope', parentStage: 'Classified' },
    { stage: 'Failed', parentStage: 'Resolving' },
    { stage: 'ManualReviewRequired', parentStage: 'Resolved' },
  ];

  const branchNodes: Node[] = branchConfig.map(({ stage, parentStage }) => {
    const parentIndex = mainFlow.indexOf(parentStage);
    const colors = getNodeColor(stage, activeIssues);
    return {
      id: stage,
      position: { x: parentIndex * xGap, y: yBranch },
      data: { label: stage },
      style: {
        background: colors.background,
        boxShadow: colors.boxShadow,
        animation: colors.animation,
        color: '#fff',
        border: '1px solid #52525b',
        borderRadius: '8px',
        padding: '8px',
        width: nodeWidth,
        height: nodeHeight,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: '10px',
        fontWeight: 600,
      },
    };
  });

  return [...mainNodes, ...branchNodes];
}

function buildEdges(activeIssues: WorkflowState[]): Edge[] {
  const activeStages = activeIssues.map((issue) => issue.stage);

  const maxActiveIndex = activeIssues.reduce((max, issue) => {
    const idx = mainFlow.indexOf(issue.stage);
    return idx > max ? idx : max;
  }, -1);

  const mainEdges: Edge[] = mainFlow.slice(0, -1).map((stage, index) => {
    const isAnimated = maxActiveIndex > 0 && index < maxActiveIndex;
    return {
      id: `${stage}-${mainFlow[index + 1]}`,
      source: stage,
      target: mainFlow[index + 1],
      style: { stroke: isAnimated ? '#10b981' : '#71717a' },
      animated: isAnimated,
    };
  });

  const branchEdges: Edge[] = [
    {
      id: 'Classified-ClassifiedOutOfScope',
      source: 'Classified',
      target: 'ClassifiedOutOfScope',
      style: { stroke: '#ef4444', strokeDasharray: '5,5' },
      animated: activeStages.includes('ClassifiedOutOfScope'),
    },
    {
      id: 'Resolving-Failed',
      source: 'Resolving',
      target: 'Failed',
      style: { stroke: '#ef4444', strokeDasharray: '5,5' },
      animated: activeStages.includes('Failed'),
    },
    {
      id: 'Resolved-ManualReviewRequired',
      source: 'Resolved',
      target: 'ManualReviewRequired',
      style: { stroke: '#ef4444', strokeDasharray: '5,5' },
      animated: activeStages.includes('ManualReviewRequired'),
    },
  ];

  return [...mainEdges, ...branchEdges];
}

export function PipelineVisualizer({ activeIssues }: PipelineVisualizerProps) {
  const nodes = buildNodes(activeIssues);
  const edges = buildEdges(activeIssues);

  return (
    <div style={{ width: '100%', height: '100%', minHeight: '280px' }}>
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
        key={activeIssues.map((i) => `${i.issueId}-${i.stage}`).join(',')}
        nodes={nodes}
        edges={edges}
        fitView
        fitViewOptions={{ padding: 0.2 }}
        panOnDrag
        zoomOnScroll
        zoomOnPinch
        zoomOnDoubleClick={false}
        elementsSelectable={false}
        preventScrolling={false}
        nodesDraggable={false}
        nodesConnectable={false}
        minZoom={0.3}
        maxZoom={2}
        proOptions={{ hideAttribution: true }}
      >
        <Controls showInteractive={false} />
      </ReactFlow>
    </div>
  );
}
