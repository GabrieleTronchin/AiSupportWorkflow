import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { EventLog, capEvents } from '../components/EventLog';
import type { WorkflowState } from '../types';

describe('EventLog', () => {
  const mockEvents: WorkflowState[] = [
    {
      issueId: 'abcdef1234567890',
      stage: 'Received',
      detail: 'Email received from user',
      lastUpdated: new Date(Date.now() - 3 * 60 * 1000).toISOString(),
    },
    {
      issueId: '1234567890abcdef',
      stage: 'Failed',
      detail: null,
      lastUpdated: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
    },
    {
      issueId: 'deadbeef12345678',
      stage: 'CodeChangeGenerated',
      detail: 'PR created successfully',
      lastUpdated: new Date(Date.now() - 30 * 1000).toISOString(),
    },
  ];

  describe('Rendering', () => {
    it('shows "No events yet" when events array is empty', () => {
      render(<EventLog events={[]} />);
      expect(screen.getByText('No events yet')).toBeInTheDocument();
    });

    it('renders events with truncated issue ID (first 8 chars)', () => {
      render(<EventLog events={mockEvents} />);

      expect(screen.getByText('abcdef12')).toBeInTheDocument();
      expect(screen.getByText('12345678')).toBeInTheDocument();
      expect(screen.getByText('deadbeef')).toBeInTheDocument();
    });

    it('shows stage badges', () => {
      render(<EventLog events={mockEvents} />);

      expect(screen.getByText('Received')).toBeInTheDocument();
      expect(screen.getByText('Failed')).toBeInTheDocument();
      expect(screen.getByText('CodeChangeGenerated')).toBeInTheDocument();
    });

    it('shows detail text when present', () => {
      render(<EventLog events={mockEvents} />);

      expect(screen.getByText('Email received from user')).toBeInTheDocument();
      expect(screen.getByText('PR created successfully')).toBeInTheDocument();
    });

    it('shows dash when detail is null', () => {
      render(<EventLog events={mockEvents} />);
      expect(screen.getByText('—')).toBeInTheDocument();
    });

    it('shows relative timestamp', () => {
      render(<EventLog events={mockEvents} />);

      expect(screen.getByText('3 min ago')).toBeInTheDocument();
      expect(screen.getByText('2 hours ago')).toBeInTheDocument();
      expect(screen.getByText('30 sec ago')).toBeInTheDocument();
    });
  });

  describe('Event cap', () => {
    it('displays at most 100 events when more are provided', () => {
      const manyEvents: WorkflowState[] = Array.from({ length: 150 }, (_, i) => ({
        issueId: `issue${String(i).padStart(12, '0')}`,
        stage: 'Received' as const,
        detail: `Event ${i}`,
        lastUpdated: new Date(Date.now() - i * 1000).toISOString(),
      }));

      render(<EventLog events={manyEvents} />);

      const items = screen.getAllByRole('listitem');
      expect(items).toHaveLength(100);
    });

    it('displays all events when fewer than 100', () => {
      render(<EventLog events={mockEvents} />);

      const items = screen.getAllByRole('listitem');
      expect(items).toHaveLength(3);
    });
  });

  describe('capEvents utility', () => {
    it('returns first 100 items from array longer than 100', () => {
      const events: WorkflowState[] = Array.from({ length: 120 }, (_, i) => ({
        issueId: `issue${String(i).padStart(12, '0')}`,
        stage: 'Received' as const,
        detail: `Event ${i}`,
        lastUpdated: new Date().toISOString(),
      }));

      const result = capEvents(events);
      expect(result).toHaveLength(100);
      expect(result[0].issueId).toBe('issue000000000000');
      expect(result[99].issueId).toBe('issue000000000099');
    });

    it('returns all items when array is shorter than 100', () => {
      const events: WorkflowState[] = Array.from({ length: 50 }, (_, i) => ({
        issueId: `issue${String(i).padStart(12, '0')}`,
        stage: 'Received' as const,
        detail: `Event ${i}`,
        lastUpdated: new Date().toISOString(),
      }));

      const result = capEvents(events);
      expect(result).toHaveLength(50);
    });

    it('returns empty array for empty input', () => {
      const result = capEvents([]);
      expect(result).toHaveLength(0);
      expect(result).toEqual([]);
    });
  });
});
