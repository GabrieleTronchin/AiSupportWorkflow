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
): string {
  if (!selectedStage) return '#3f3f46'; // inactive gray

  // Terminal/error stages highlighted red when they are the current stage
  if (terminalStages.includes(stage) && stage === selectedStage) {
    return '#ef4444'; // red
  }

  // Current/active stage
  if (stage === selectedStage) {
    return '#3b82f6'; // blue
  }

  // Completed stages: stages before the current one in the main flow
  const currentIndex = mainFlow.indexOf(selectedStage);
  const stageIndex = mainFlow.indexOf(stage);

  if (currentIndex > 0 && stageIndex >= 0 && stageIndex < currentIndex) {
    return '#10b981'; // green
  }

  // Terminal stages that are not the selected stage remain gray
  // All other stages are inactive
  return '#3f3f46'; // inactive gray
}

function buildNodes(selectedStage?: WorkflowStage): Node[] {
  const nodeWidth = 180;
  const nodeHeight = 50;
  const xGap = 220;
  const yMain = 100;
  const yBranch = 250;

  const mainNodes: Node[] = mainFlow.map((stage, index) => ({
    id: stage,
    position: { x: index * xGap, y: yMain },
    data: { label: stage },
    style: {
      background: getNodeColor(stage, selectedStage),
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
  }));

  // Branch nodes: terminal stages positioned below their branch points
  const branchConfig: { stage: WorkflowStage; parentStage: WorkflowStage }[] = [
    { stage: 'ClassifiedOutOfScope', parentStage: 'Classified' },
    { stage: 'Failed', parentStage: 'Resolving' },
    { stage: 'ManualReviewRequired', parentStage: 'Resolved' },
  ];

  const branchNodes: Node[] = branchConfig.map(({ stage, parentStage }) => {
    const parentIndex = mainFlow.indexOf(parentStage);
    return {
      id: stage,
      position: { x: parentIndex * xGap, y: yBranch },
      data: { label: stage },
      style: {
        background: getNodeColor(stage, selectedStage),
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

function buildEdges(): Edge[] {
  // Main flow edges
  const mainEdges: Edge[] = mainFlow.slice(0, -1).map((stage, index) => ({
    id: `${stage}-${mainFlow[index + 1]}`,
    source: stage,
    target: mainFlow[index + 1],
    style: { stroke: '#71717a' },
    animated: false,
  }));

  // Branch edges from decision points to terminal nodes
  const branchEdges: Edge[] = [
    {
      id: 'Classified-ClassifiedOutOfScope',
      source: 'Classified',
      target: 'ClassifiedOutOfScope',
      style: { stroke: '#ef4444', strokeDasharray: '5,5' },
      animated: false,
    },
    {
      id: 'Resolving-Failed',
      source: 'Resolving',
      target: 'Failed',
      style: { stroke: '#ef4444', strokeDasharray: '5,5' },
      animated: false,
    },
    {
      id: 'Resolved-ManualReviewRequired',
      source: 'Resolved',
      target: 'ManualReviewRequired',
      style: { stroke: '#ef4444', strokeDasharray: '5,5' },
      animated: false,
    },
  ];

  return [...mainEdges, ...branchEdges];
}

export function PipelineVisualizer({ selectedIssue }: PipelineVisualizerProps) {
  const nodes = buildNodes(selectedIssue?.stage);
  const edges = buildEdges();

  return (
    <div style={{ width: '100%', height: '400px' }}>
      <ReactFlow
        nodes={nodes}
        edges={edges}
        fitView
        nodesDraggable={false}
        nodesConnectable={false}
        proOptions={{ hideAttribution: true }}
      />
    </div>
  );
}
