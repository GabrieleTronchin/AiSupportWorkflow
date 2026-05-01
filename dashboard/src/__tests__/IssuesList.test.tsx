import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { IssuesList, formatRelativeTime } from '../components/IssuesList';
import type { WorkflowState } from '../types';

describe('IssuesList', () => {
  const mockOnSelectIssue = vi.fn();

  const mockIssues: WorkflowState[] = [
    {
      issueId: 'abcdef1234567890',
      stage: 'Received',
      detail: 'Email received from user',
      lastUpdated: new Date(Date.now() - 3 * 60 * 1000).toISOString(), // 3 min ago
    },
    {
      issueId: '1234567890abcdef',
      stage: 'Failed',
      detail: null,
      lastUpdated: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(), // 2 hours ago
    },
    {
      issueId: 'deadbeef12345678',
      stage: 'CodeChangeGenerated',
      detail: 'PR created successfully',
      lastUpdated: new Date(Date.now() - 30 * 1000).toISOString(), // 30 sec ago
    },
  ];

  beforeEach(() => {
    mockOnSelectIssue.mockClear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('Rendering', () => {
    it('renders table headers', () => {
      render(<IssuesList issues={mockIssues} onSelectIssue={mockOnSelectIssue} />);

      expect(screen.getByText('Issue ID')).toBeInTheDocument();
      expect(screen.getByText('Stage')).toBeInTheDocument();
      expect(screen.getByText('Detail')).toBeInTheDocument();
      expect(screen.getByText('Last Updated')).toBeInTheDocument();
    });

    it('renders issue rows with truncated ID (first 8 chars)', () => {
      render(<IssuesList issues={mockIssues} onSelectIssue={mockOnSelectIssue} />);

      expect(screen.getByText('abcdef12')).toBeInTheDocument();
      expect(screen.getByText('12345678')).toBeInTheDocument();
      expect(screen.getByText('deadbeef')).toBeInTheDocument();
    });

    it('shows detail text when present', () => {
      render(<IssuesList issues={mockIssues} onSelectIssue={mockOnSelectIssue} />);

      expect(screen.getByText('Email received from user')).toBeInTheDocument();
      expect(screen.getByText('PR created successfully')).toBeInTheDocument();
    });

    it('shows dash when detail is null', () => {
      render(<IssuesList issues={mockIssues} onSelectIssue={mockOnSelectIssue} />);

      expect(screen.getByText('—')).toBeInTheDocument();
    });

    it('shows relative time for lastUpdated', () => {
      render(<IssuesList issues={mockIssues} onSelectIssue={mockOnSelectIssue} />);

      expect(screen.getByText('3 min ago')).toBeInTheDocument();
      expect(screen.getByText('2 hours ago')).toBeInTheDocument();
      expect(screen.getByText('30 sec ago')).toBeInTheDocument();
    });
  });

  describe('Stage coloring', () => {
    it('terminal stages (Failed, ClassifiedOutOfScope) have red badge classes', () => {
      const terminalIssues: WorkflowState[] = [
        { issueId: 'failed00000000', stage: 'Failed', detail: null, lastUpdated: new Date().toISOString() },
        { issueId: 'outofscope0000', stage: 'ClassifiedOutOfScope', detail: null, lastUpdated: new Date().toISOString() },
      ];

      render(<IssuesList issues={terminalIssues} onSelectIssue={mockOnSelectIssue} />);

      const failedBadge = screen.getByText('Failed');
      const outOfScopeBadge = screen.getByText('ClassifiedOutOfScope');

      expect(failedBadge).toHaveClass('text-red-400');
      expect(failedBadge).toHaveClass('bg-red-400/10');
      expect(outOfScopeBadge).toHaveClass('text-red-400');
      expect(outOfScopeBadge).toHaveClass('bg-red-400/10');
    });

    it('completed stages (CodeChangeGenerated, Resolved) have green badge classes', () => {
      const completedIssues: WorkflowState[] = [
        { issueId: 'codegen00000000', stage: 'CodeChangeGenerated', detail: null, lastUpdated: new Date().toISOString() },
        { issueId: 'resolved0000000', stage: 'Resolved', detail: null, lastUpdated: new Date().toISOString() },
      ];

      render(<IssuesList issues={completedIssues} onSelectIssue={mockOnSelectIssue} />);

      const codeGenBadge = screen.getByText('CodeChangeGenerated');
      const resolvedBadge = screen.getByText('Resolved');

      expect(codeGenBadge).toHaveClass('text-emerald-400');
      expect(codeGenBadge).toHaveClass('bg-emerald-400/10');
      expect(resolvedBadge).toHaveClass('text-emerald-400');
      expect(resolvedBadge).toHaveClass('bg-emerald-400/10');
    });

    it('in-progress stages (Received, Classified, etc.) have blue badge classes', () => {
      const inProgressIssues: WorkflowState[] = [
        { issueId: 'received0000000', stage: 'Received', detail: null, lastUpdated: new Date().toISOString() },
        { issueId: 'classified00000', stage: 'Classified', detail: null, lastUpdated: new Date().toISOString() },
        { issueId: 'teamassign00000', stage: 'TeamAssigned', detail: null, lastUpdated: new Date().toISOString() },
      ];

      render(<IssuesList issues={inProgressIssues} onSelectIssue={mockOnSelectIssue} />);

      const receivedBadge = screen.getByText('Received');
      const classifiedBadge = screen.getByText('Classified');
      const teamAssignedBadge = screen.getByText('TeamAssigned');

      expect(receivedBadge).toHaveClass('text-blue-400');
      expect(receivedBadge).toHaveClass('bg-blue-400/10');
      expect(classifiedBadge).toHaveClass('text-blue-400');
      expect(classifiedBadge).toHaveClass('bg-blue-400/10');
      expect(teamAssignedBadge).toHaveClass('text-blue-400');
      expect(teamAssignedBadge).toHaveClass('bg-blue-400/10');
    });
  });

  describe('Row selection', () => {
    it('calls onSelectIssue with the correct issue when row is clicked', () => {
      render(<IssuesList issues={mockIssues} onSelectIssue={mockOnSelectIssue} />);

      const firstRow = screen.getByText('abcdef12').closest('tr')!;
      fireEvent.click(firstRow);

      expect(mockOnSelectIssue).toHaveBeenCalledTimes(1);
      expect(mockOnSelectIssue).toHaveBeenCalledWith(mockIssues[0]);
    });

    it('calls onSelectIssue with the second issue when second row is clicked', () => {
      render(<IssuesList issues={mockIssues} onSelectIssue={mockOnSelectIssue} />);

      const secondRow = screen.getByText('12345678').closest('tr')!;
      fireEvent.click(secondRow);

      expect(mockOnSelectIssue).toHaveBeenCalledTimes(1);
      expect(mockOnSelectIssue).toHaveBeenCalledWith(mockIssues[1]);
    });
  });

  describe('formatRelativeTime', () => {
    it('returns seconds for recent timestamps', () => {
      const timestamp = new Date(Date.now() - 45 * 1000).toISOString();
      expect(formatRelativeTime(timestamp)).toBe('45 sec ago');
    });

    it('returns minutes for timestamps within an hour', () => {
      const timestamp = new Date(Date.now() - 15 * 60 * 1000).toISOString();
      expect(formatRelativeTime(timestamp)).toBe('15 min ago');
    });

    it('returns hours for timestamps within a day', () => {
      const timestamp = new Date(Date.now() - 5 * 60 * 60 * 1000).toISOString();
      expect(formatRelativeTime(timestamp)).toBe('5 hours ago');
    });

    it('returns days for older timestamps', () => {
      const timestamp = new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString();
      expect(formatRelativeTime(timestamp)).toBe('3 days ago');
    });

    it('returns "just now" for future timestamps', () => {
      const timestamp = new Date(Date.now() + 60 * 1000).toISOString();
      expect(formatRelativeTime(timestamp)).toBe('just now');
    });
  });
});
