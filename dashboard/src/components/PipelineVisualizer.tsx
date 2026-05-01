import { ReactFlow, type Node, type Edge } from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { type WorkflowState, type WorkflowStage } from '../types';

interface PipelineVisualizerProps {
  selectedIssue?: WorkflowState;
}

const mainFlow: WorkflowStage[] = [
  'Received',
  'Classified',
  'TeamAssigned',
  'AgentAssigned',
  'Resolving',
  'Resolved',
  'CodeChangeGenerated',
];

const terminalStages: WorkflowStage[] = [
  'ClassifiedOutOfScope',
  'Failed',
  'ManualReviewRequired',
];

function getNodeColor(
  stage: WorkflowStage,
  selectedStage?: WorkflowStage
): { background: string; boxShadow?: string; animation?: string } {
  if (!selectedStage) return { background: '#3f3f46' }; // inactive gray

  // If the final success stage is reached, all main flow nodes are green
  if (selectedStage === 'CodeChangeGenerated' && mainFlow.includes(stage)) {
    return { background: '#10b981' };
  }

  // Terminal/error stages highlighted red when they are the current stage
  if (terminalStages.includes(stage) && stage === selectedStage) {
    return { background: '#ef4444', boxShadow: '0 0 12px 4px rgba(239, 68, 68, 0.5)' };
  }

  // Current/active stage — pulsing effect
  if (stage === selectedStage) {
    return {
      background: '#3b82f6',
      boxShadow: '0 0 12px 4px rgba(59, 130, 246, 0.5)',
      animation: 'pulse 2s infinite',
    };
  }

  // Completed stages: stages before the current one in the main flow
  const currentIndex = mainFlow.indexOf(selectedStage);
  const stageIndex = mainFlow.indexOf(stage);

  if (currentIndex > 0 && stageIndex >= 0 && stageIndex < currentIndex) {
    return { background: '#10b981' }; // green
  }

  // Terminal stages that are not the selected stage remain gray
  // All other stages are inactive
  return { background: '#3f3f46' }; // inactive gray
}

function buildNodes(selectedStage?: WorkflowStage): Node[] {
  const nodeWidth = 180;
  const nodeHeight = 50;
  const xGap = 220;
  const yMain = 100;
  const yBranch = 250;

  const mainNodes: Node[] = mainFlow.map((stage, index) => {
    const colors = getNodeColor(stage, selectedStage);
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
        padding: '10px',
        width: nodeWidth,
        height: nodeHeight,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: '12px',
        fontWeight: 600,
      },
    };
  });

  // Branch nodes: terminal stages positioned below their branch points
  const branchConfig: { stage: WorkflowStage; parentStage: WorkflowStage }[] = [
    { stage: 'ClassifiedOutOfScope', parentStage: 'Classified' },
    { stage: 'Failed', parentStage: 'Resolving' },
    { stage: 'ManualReviewRequired', parentStage: 'Resolved' },
  ];

  const branchNodes: Node[] = branchConfig.map(({ stage, parentStage }) => {
    const parentIndex = mainFlow.indexOf(parentStage);
    const colors = getNodeColor(stage, selectedStage);
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
        padding: '10px',
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

  return [...mainNodes, ...branchNodes];
}

function buildEdges(selectedStage?: WorkflowStage): Edge[] {
  const currentIndex = selectedStage ? mainFlow.indexOf(selectedStage) : -1;

  // Main flow edges
  const mainEdges: Edge[] = mainFlow.slice(0, -1).map((stage, index) => {
    // Animate edges between completed stages and the active stage
    const isAnimated = currentIndex > 0 && index < currentIndex;
    return {
      id: `${stage}-${mainFlow[index + 1]}`,
      source: stage,
      target: mainFlow[index + 1],
      style: { stroke: isAnimated ? '#10b981' : '#71717a' },
      animated: isAnimated,
    };
  });

  // Branch edges from decision points to terminal nodes
  const branchEdges: Edge[] = [
    {
      id: 'Classified-ClassifiedOutOfScope',
      source: 'Classified',
      target: 'ClassifiedOutOfScope',
      style: { stroke: '#ef4444', strokeDasharray: '5,5' },
      animated: selectedStage === 'ClassifiedOutOfScope',
    },
    {
      id: 'Resolving-Failed',
      source: 'Resolving',
      target: 'Failed',
      style: { stroke: '#ef4444', strokeDasharray: '5,5' },
      animated: selectedStage === 'Failed',
    },
    {
      id: 'Resolved-ManualReviewRequired',
      source: 'Resolved',
      target: 'ManualReviewRequired',
      style: { stroke: '#ef4444', strokeDasharray: '5,5' },
      animated: selectedStage === 'ManualReviewRequired',
    },
  ];

  return [...mainEdges, ...branchEdges];
}

export function PipelineVisualizer({ selectedIssue }: PipelineVisualizerProps) {
  const nodes = buildNodes(selectedIssue?.stage);
  const edges = buildEdges(selectedIssue?.stage);

  return (
    <div style={{ width: '100%', height: '400px' }}>
      <style>{`
        @keyframes pulse {
          0%, 100% { opacity: 1; }
          50% { opacity: 0.7; }
        }
      `}</style>
      <ReactFlow
        nodes={nodes}
        edges={edges}
        fitView
        panOnDrag={false}
        zoomOnScroll={false}
        zoomOnPinch={false}
        zoomOnDoubleClick={false}
        elementsSelectable={false}
        preventScrolling={false}
        nodesDraggable={false}
        nodesConnectable={false}
        proOptions={{ hideAttribution: true }}
      />
    </div>
  );
}
