import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AgentMonitor, getStatusBadgeClasses } from '../components/AgentMonitor';
import type { AgentStatus } from '../types';

describe('AgentMonitor', () => {
  const mockAgents: AgentStatus[] = [
    { agentId: 'TeamA_BackendDeveloper', status: 'Idle', lastAction: 'Resolved issue #42' },
    { agentId: 'TeamB_FrontendDeveloper', status: 'Working', lastAction: 'Analyzing root cause' },
    { agentId: 'TeamA_QAEngineer', status: 'Idle', lastAction: null },
  ];

  describe('Rendering', () => {
    it('shows loading text when isLoading is true', () => {
      render(<AgentMonitor agents={[]} isLoading={true} />);

      expect(screen.getByText('Loading agents...')).toBeInTheDocument();
    });

    it('renders agent cards with agent IDs', () => {
      render(<AgentMonitor agents={mockAgents} isLoading={false} />);

      expect(screen.getByText('TeamA_BackendDeveloper')).toBeInTheDocument();
      expect(screen.getByText('TeamB_FrontendDeveloper')).toBeInTheDocument();
      expect(screen.getByText('TeamA_QAEngineer')).toBeInTheDocument();
    });

    it('shows last action text', () => {
      render(<AgentMonitor agents={mockAgents} isLoading={false} />);

      expect(screen.getByText('Resolved issue #42')).toBeInTheDocument();
      expect(screen.getByText('Analyzing root cause')).toBeInTheDocument();
    });

    it('shows "No recent activity" when lastAction is null', () => {
      render(<AgentMonitor agents={mockAgents} isLoading={false} />);

      expect(screen.getByText('No recent activity')).toBeInTheDocument();
    });
  });

  describe('Status badge colors', () => {
    it('"Idle" status has green badge classes', () => {
      const agents: AgentStatus[] = [
        { agentId: 'agent-idle', status: 'Idle', lastAction: null },
      ];

      render(<AgentMonitor agents={agents} isLoading={false} />);

      const badge = screen.getByText('Idle');
      expect(badge).toHaveClass('text-emerald-400');
      expect(badge).toHaveClass('bg-emerald-400/10');
    });

    it('"Working" status has yellow badge classes', () => {
      const agents: AgentStatus[] = [
        { agentId: 'agent-working', status: 'Working', lastAction: null },
      ];

      render(<AgentMonitor agents={agents} isLoading={false} />);

      const badge = screen.getByText('Working');
      expect(badge).toHaveClass('text-amber-400');
      expect(badge).toHaveClass('bg-amber-400/10');
    });

    it('unknown status has gray badge classes', () => {
      const agents: AgentStatus[] = [
        { agentId: 'agent-unknown', status: 'Offline', lastAction: null },
      ];

      render(<AgentMonitor agents={agents} isLoading={false} />);

      const badge = screen.getByText('Offline');
      expect(badge).toHaveClass('text-zinc-400');
      expect(badge).toHaveClass('bg-zinc-400/10');
    });
  });

  describe('getStatusBadgeClasses', () => {
    it('returns emerald classes for Idle', () => {
      expect(getStatusBadgeClasses('Idle')).toBe('text-emerald-400 bg-emerald-400/10');
    });

    it('returns amber classes for Working', () => {
      expect(getStatusBadgeClasses('Working')).toBe('text-amber-400 bg-amber-400/10');
    });

    it('returns zinc classes for unknown status', () => {
      expect(getStatusBadgeClasses('SomethingElse')).toBe('text-zinc-400 bg-zinc-400/10');
    });
  });
});
