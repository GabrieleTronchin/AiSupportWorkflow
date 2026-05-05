import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AgentMonitor, renderAgentActivity } from '../components/AgentMonitor';
import { getAgentStatusBadgeClasses } from '../utils/badges';
import type { AgentStatus } from '../types';

describe('AgentMonitor', () => {
  const mockAgents: AgentStatus[] = [
    { agentId: 'TeamA_BackendDeveloper', team: 'TeamA', role: 'BackendDeveloper', status: 'Idle', lastAction: 'Resolved issue #42', currentIssueId: null, currentSubject: null, currentStage: null },
    { agentId: 'TeamB_FrontendDeveloper', team: 'TeamB', role: 'FrontendDeveloper', status: 'Working', lastAction: 'Analyzing root cause', currentIssueId: 'issue-123', currentSubject: 'NullRef in OrderController', currentStage: 'Resolving' },
    { agentId: 'TeamA_QAEngineer', team: 'TeamA', role: 'QAEngineer', status: 'Idle', lastAction: null, currentIssueId: null, currentSubject: null, currentStage: null },
  ];

  describe('Rendering', () => {
    it('shows skeleton loading cards when isLoading is true', () => {
      const { container } = render(<AgentMonitor agents={[]} isLoading={true} />);

      const skeletonCards = container.querySelectorAll('.animate-pulse');
      expect(skeletonCards.length).toBe(3);
    });

    it('renders agent cards with agent IDs', () => {
      render(<AgentMonitor agents={mockAgents} isLoading={false} />);

      expect(screen.getByText('TeamA_BackendDeveloper')).toBeInTheDocument();
      expect(screen.getByText('TeamB_FrontendDeveloper')).toBeInTheDocument();
      expect(screen.getByText('TeamA_QAEngineer')).toBeInTheDocument();
    });

    it('shows current email info for Working agent with currentIssueId', () => {
      render(<AgentMonitor agents={mockAgents} isLoading={false} />);

      expect(screen.getByText(/Issue: issue-123/)).toBeInTheDocument();
      expect(screen.getByText(/Subject: NullRef in OrderController/)).toBeInTheDocument();
      expect(screen.getByText(/Stage: Resolving/)).toBeInTheDocument();
    });

    it('shows "No recent activity" for Idle agents', () => {
      render(<AgentMonitor agents={mockAgents} isLoading={false} />);

      const noActivityElements = screen.getAllByText('No recent activity');
      expect(noActivityElements.length).toBe(2); // Two idle agents
    });
  });

  describe('Status badge colors', () => {
    it('"Idle" status has green badge classes', () => {
      const agents: AgentStatus[] = [
        { agentId: 'agent-idle', team: 'TeamA', role: 'BackendDeveloper', status: 'Idle', lastAction: null, currentIssueId: null, currentSubject: null, currentStage: null },
      ];

      render(<AgentMonitor agents={agents} isLoading={false} />);

      const badge = screen.getByText('Idle');
      expect(badge).toHaveClass('text-emerald-400');
      expect(badge).toHaveClass('bg-emerald-400/10');
    });

    it('"Working" status has yellow badge classes', () => {
      const agents: AgentStatus[] = [
        { agentId: 'agent-working', team: 'TeamA', role: 'BackendDeveloper', status: 'Working', lastAction: null, currentIssueId: null, currentSubject: null, currentStage: null },
      ];

      render(<AgentMonitor agents={agents} isLoading={false} />);

      const badge = screen.getByText('Working');
      expect(badge).toHaveClass('text-amber-400');
      expect(badge).toHaveClass('bg-amber-400/10');
    });

    it('getAgentStatusBadgeClasses returns gray for unknown status string', () => {
      expect(getAgentStatusBadgeClasses('Unknown')).toBe('text-zinc-400 bg-zinc-400/10');
    });
  });

  describe('getAgentStatusBadgeClasses', () => {
    it('returns emerald classes for Idle', () => {
      expect(getAgentStatusBadgeClasses('Idle')).toBe('text-emerald-400 bg-emerald-400/10');
    });

    it('returns amber classes for Working', () => {
      expect(getAgentStatusBadgeClasses('Working')).toBe('text-amber-400 bg-amber-400/10');
    });

    it('returns zinc classes for unknown status', () => {
      expect(getAgentStatusBadgeClasses('SomethingElse')).toBe('text-zinc-400 bg-zinc-400/10');
    });
  });

  describe('renderAgentActivity', () => {
    it('returns full email info for Working agent with all fields', () => {
      const agent: AgentStatus = {
        agentId: 'agent-1', team: 'TeamA', role: 'BackendDeveloper',
        status: 'Working', lastAction: null,
        currentIssueId: 'abc-123', currentSubject: 'Bug report', currentStage: 'Resolving',
      };
      expect(renderAgentActivity(agent)).toBe('Issue: abc-123 | Subject: Bug report | Stage: Resolving');
    });

    it('returns "No recent activity" for Idle agent', () => {
      const agent: AgentStatus = {
        agentId: 'agent-1', team: 'TeamA', role: 'BackendDeveloper',
        status: 'Idle', lastAction: null,
        currentIssueId: null, currentSubject: null, currentStage: null,
      };
      expect(renderAgentActivity(agent)).toBe('No recent activity');
    });

    it('returns "No recent activity" for Working agent without currentIssueId', () => {
      const agent: AgentStatus = {
        agentId: 'agent-1', team: 'TeamA', role: 'BackendDeveloper',
        status: 'Working', lastAction: null,
        currentIssueId: null, currentSubject: null, currentStage: null,
      };
      expect(renderAgentActivity(agent)).toBe('No recent activity');
    });

    it('handles Working agent with issueId but no subject or stage', () => {
      const agent: AgentStatus = {
        agentId: 'agent-1', team: 'TeamA', role: 'BackendDeveloper',
        status: 'Working', lastAction: null,
        currentIssueId: 'xyz-789', currentSubject: null, currentStage: null,
      };
      expect(renderAgentActivity(agent)).toBe('Issue: xyz-789');
    });
  });
});
