import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AgentMonitor, renderAgentActivity } from '../components/AgentMonitor';
import { getAgentStatusBadgeClasses } from '../utils/badges';
import type { AgentStatus } from '../types';
import { agent } from './fixtures';

describe('AgentMonitor', () => {
  const mockAgents: AgentStatus[] = [
    agent(),
    agent({
      agentId: 'TeamB_FrontendDeveloper',
      name: 'Frontend developer',
      group: 'TeamB',
      role: 'FrontendDeveloper',
      status: 'Working',
      currentRunId: 'run-123',
      currentRunTitle: 'NullRef in OrderController',
      currentStepId: 'resolve',
    }),
    agent({ agentId: 'TeamA_QAEngineer', name: 'QA engineer', role: 'QAEngineer' }),
  ];

  describe('Rendering', () => {
    it('shows skeleton loading cards when isLoading is true', () => {
      const { container } = render(<AgentMonitor agents={[]} isLoading={true} />);

      expect(container.querySelectorAll('.animate-pulse').length).toBe(3);
    });

    it('renders agent cards with agent ids, group and role', () => {
      render(<AgentMonitor agents={mockAgents} isLoading={false} />);

      expect(screen.getByText('TeamA_BackendDeveloper')).toBeInTheDocument();
      expect(screen.getByText('TeamB_FrontendDeveloper')).toBeInTheDocument();
      expect(screen.getByText('TeamA_QAEngineer')).toBeInTheDocument();
      expect(screen.getAllByText(/TeamB/).length).toBeGreaterThan(0);
    });

    it('shows the current run of a Working agent with the step display name', () => {
      render(<AgentMonitor agents={mockAgents} isLoading={false} stepName={(_agent, stepId) => (stepId === 'resolve' ? 'Root cause analysis' : stepId)} />);

      expect(screen.getByText(/Run: run-123/)).toBeInTheDocument();
      expect(screen.getByText(/Title: NullRef in OrderController/)).toBeInTheDocument();
      expect(screen.getByText(/Step: Root cause analysis/)).toBeInTheDocument();
    });

    it('shows "No recent activity" for Idle agents', () => {
      render(<AgentMonitor agents={mockAgents} isLoading={false} />);

      expect(screen.getAllByText('No recent activity').length).toBe(2);
    });
  });

  describe('Status badge colors', () => {
    it('"Idle" status has green badge classes', () => {
      render(<AgentMonitor agents={[agent()]} isLoading={false} />);

      const badge = screen.getByText('Idle');
      expect(badge).toHaveClass('text-emerald-400');
      expect(badge).toHaveClass('bg-emerald-400/10');
    });

    it('"Working" status has amber badge classes', () => {
      render(<AgentMonitor agents={[agent({ status: 'Working' })]} isLoading={false} />);

      const badge = screen.getByText('Working');
      expect(badge).toHaveClass('text-amber-400');
      expect(badge).toHaveClass('bg-amber-400/10');
    });

    it.each([
      ['Idle', 'text-emerald-400 bg-emerald-400/10'],
      ['Working', 'text-amber-400 bg-amber-400/10'],
      ['SomethingElse', 'text-zinc-400 bg-zinc-400/10'],
    ])('getAgentStatusBadgeClasses(%s)', (status, classes) => {
      expect(getAgentStatusBadgeClasses(status)).toBe(classes);
    });
  });

  describe('renderAgentActivity', () => {
    it('returns run, title and step for a Working agent', () => {
      const working = agent({ status: 'Working', currentRunId: 'abc-123', currentRunTitle: 'Bug report', currentStepId: 'resolve' });

      expect(renderAgentActivity(working)).toBe('Run: abc-123 | Title: Bug report | Step: resolve');
    });

    it('returns "No recent activity" for an Idle agent', () => {
      expect(renderAgentActivity(agent())).toBe('No recent activity');
    });

    it('returns "No recent activity" for a Working agent without run', () => {
      expect(renderAgentActivity(agent({ status: 'Working' }))).toBe('No recent activity');
    });

    it('handles a Working agent with a run but no title or step', () => {
      expect(renderAgentActivity(agent({ status: 'Working', currentRunId: 'xyz-789' }))).toBe('Run: xyz-789');
    });
  });
});
