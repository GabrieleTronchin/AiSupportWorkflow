import { ReactFlow, type Node, type Edge } from '@xyflow/react';
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
  'CodeChangeGenerated',
];

export const terminalStages: WorkflowStage[] = [
  'ClassifiedOutOfScope',
  'Failed',
  'ManualReviewRequired',
];

/**
 * Determines the color/style for a pipeline node given the stage and the list of active issues.
 *
 * Color mapping:
 * - When no issues are active: all nodes neutral gray (idle state)
 * - Completed stages (before the active stage in the main flow): green
 * - Active stage: blue with pulsing glow (box-shadow animation)
 * - Pending stages (after the active stage): neutral gray
 * - Terminal stages highlighted red when they are the current stage of an active issue
 */
export function getNodeColor(
  stage: WorkflowStage,
  activeIssues: WorkflowState[]
): { background: string; boxShadow?: string; animation?: string } {
  const neutralGray = { background: '#3f3f46' };

  // When no issues are active, all nodes are neutral gray (idle state)
  if (activeIssues.length === 0) return neutralGray;

  // Collect all active stages from the issues
  const activeStages = activeIssues.map((issue) => issue.stage);

  // Check if this stage is an active stage for any issue
  const isActiveStage = activeStages.includes(stage);

  // Terminal/error stages highlighted red when they are the current stage of an active issue
  if (terminalStages.includes(stage) && isActiveStage) {
    return { background: '#ef4444', boxShadow: '0 0 12px 4px rgba(239, 68, 68, 0.5)' };
  }

  // Active stage — pulsing blue effect
  if (isActiveStage) {
    return {
      background: '#3b82f6',
      boxShadow: '0 0 12px 4px rgba(59, 130, 246, 0.5)',
      animation: 'pulse 2s infinite',
    };
  }

  // For main flow stages, check if this stage is completed relative to any active issue
  const stageIndex = mainFlow.indexOf(stage);
  if (stageIndex >= 0) {
    // A stage is "completed" if any active issue is at a later stage in the main flow
    const isCompleted = activeIssues.some((issue) => {
      const issueIndex = mainFlow.indexOf(issue.stage);
      // If the issue's stage is in the main flow and comes after this stage
      if (issueIndex > stageIndex) return true;
      // If the final success stage is reached, all main flow nodes before it are green
      if (issue.stage === 'CodeChangeGenerated' && mainFlow.includes(stage)) return true;
      return false;
    });

    if (isCompleted) {
      return { background: '#10b981' }; // green
    }
  }

  return neutralGray;
}

/**
 * Returns the labels (issueId + subject) for issues active at a given stage.
 */
function getStageLabels(stage: WorkflowStage, activeIssues: WorkflowState[]): string[] {
  return activeIssues
    .filter((issue) => issue.stage === stage)
    .map((issue) => {
      const subject = issue.detail || '';
      return subject ? `${issue.issueId}: ${subject}` : issue.issueId;
    });
}

function buildNodes(activeIssues: WorkflowState[]): Node[] {
  const nodeWidth = 180;
  const nodeHeight = 50;
  const xGap = 220;
  const yMain = 100;
  const yBranch = 250;

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

  // Add label nodes for active issues
  const labelNodes: Node[] = [];
  const allStages: WorkflowStage[] = [...mainFlow, ...terminalStages];

  for (const stage of allStages) {
    const labels = getStageLabels(stage, activeIssues);
    if (labels.length === 0) continue;

    // Determine position based on whether it's a main flow or branch stage
    const mainIndex = mainFlow.indexOf(stage);
    const branchEntry = branchConfig.find((b) => b.stage === stage);

    let x: number;
    let y: number;

    if (mainIndex >= 0) {
      x = mainIndex * xGap;
      y = yMain + nodeHeight + 10;
    } else if (branchEntry) {
      const parentIndex = mainFlow.indexOf(branchEntry.parentStage);
      x = parentIndex * xGap;
      y = yBranch + nodeHeight + 10;
    } else {
      continue;
    }

    labels.forEach((label, labelIndex) => {
      labelNodes.push({
        id: `label-${stage}-${labelIndex}`,
        position: { x, y: y + labelIndex * 20 },
        data: { label },
        style: {
          background: 'transparent',
          border: 'none',
          color: '#a1a1aa',
          fontSize: '10px',
          width: nodeWidth,
          padding: '2px 4px',
          pointerEvents: 'none' as const,
        },
        selectable: false,
        draggable: false,
        connectable: false,
      });
    });
  }

  return [...mainNodes, ...branchNodes, ...labelNodes];
}

function buildEdges(activeIssues: WorkflowState[]): Edge[] {
  const activeStages = activeIssues.map((issue) => issue.stage);

  // Determine the furthest active stage index in the main flow for edge animation
  const maxActiveIndex = activeIssues.reduce((max, issue) => {
    const idx = mainFlow.indexOf(issue.stage);
    return idx > max ? idx : max;
  }, -1);

  // Main flow edges
  const mainEdges: Edge[] = mainFlow.slice(0, -1).map((stage, index) => {
    // Animate edges between completed stages and the active stage
    const isAnimated = maxActiveIndex > 0 && index < maxActiveIndex;
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
