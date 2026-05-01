import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { EventLog, capEvents } from '../components/EventLog';
import type { StateTransitionEvent } from '../types';

describe('EventLog', () => {
  const mockEvents: StateTransitionEvent[] = [
    {
      id: 'evt-1',
      issueId: 'abcdef1234567890',
      previousStage: null,
      newStage: 'Received',
      detail: 'Email received from user',
      timestamp: new Date(Date.now() - 3 * 60 * 1000).toISOString(),
    },
    {
      id: 'evt-2',
      issueId: '1234567890abcdef',
      previousStage: 'Resolving',
      newStage: 'Failed',
      detail: null,
      timestamp: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
    },
    {
      id: 'evt-3',
      issueId: 'deadbeef12345678',
      previousStage: 'Resolved',
      newStage: 'CodeChangeGenerated',
      detail: 'PR created successfully',
      timestamp: new Date(Date.now() - 30 * 1000).toISOString(),
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

    it('shows new stage badges', () => {
      render(<EventLog events={mockEvents} />);

      expect(screen.getByText('Received')).toBeInTheDocument();
      expect(screen.getByText('Failed')).toBeInTheDocument();
      expect(screen.getByText('CodeChangeGenerated')).toBeInTheDocument();
    });

    it('shows previous stage when present', () => {
      render(<EventLog events={mockEvents} />);

      expect(screen.getByText('Resolving')).toBeInTheDocument();
      expect(screen.getByText('Resolved')).toBeInTheDocument();
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
    it('displays at most 200 events when more are provided', () => {
      const manyEvents: StateTransitionEvent[] = Array.from({ length: 250 }, (_, i) => ({
        id: `evt-${i}`,
        issueId: `issue${String(i).padStart(12, '0')}`,
        previousStage: null,
        newStage: 'Received' as const,
        detail: `Event ${i}`,
        timestamp: new Date(Date.now() - i * 1000).toISOString(),
      }));

      render(<EventLog events={manyEvents} />);

      const items = screen.getAllByRole('listitem');
      expect(items).toHaveLength(200);
    });

    it('displays all events when fewer than 200', () => {
      render(<EventLog events={mockEvents} />);

      const items = screen.getAllByRole('listitem');
      expect(items).toHaveLength(3);
    });
  });

  describe('capEvents utility', () => {
    it('returns first 200 items from array longer than 200', () => {
      const events: StateTransitionEvent[] = Array.from({ length: 220 }, (_, i) => ({
        id: `evt-${i}`,
        issueId: `issue${String(i).padStart(12, '0')}`,
        previousStage: null,
        newStage: 'Received' as const,
        detail: `Event ${i}`,
        timestamp: new Date().toISOString(),
      }));

      const result = capEvents(events);
      expect(result).toHaveLength(200);
      expect(result[0].issueId).toBe('issue000000000000');
      expect(result[199].issueId).toBe('issue000000000199');
    });

    it('returns all items when array is shorter than 200', () => {
      const events: StateTransitionEvent[] = Array.from({ length: 50 }, (_, i) => ({
        id: `evt-${i}`,
        issueId: `issue${String(i).padStart(12, '0')}`,
        previousStage: null,
        newStage: 'Received' as const,
        detail: `Event ${i}`,
        timestamp: new Date().toISOString(),
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
