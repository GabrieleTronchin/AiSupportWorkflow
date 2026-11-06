import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { EventLog, capEvents, describeEventKind, groupEventsByRun } from '../components/EventLog';
import type { RunEvent } from '../types';
import { event } from './fixtures';

describe('EventLog', () => {
  // Fixed clock so relative-time assertions don't drift on slow test runs.
  const NOW = new Date('2026-01-01T12:00:00.000Z').getTime();

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(NOW);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  const mockEvents: RunEvent[] = [
    event({ id: 'evt-1', runId: 'abcdef1234567890', kind: 'Queued', stepId: null, status: 'Queued', detail: 'Email received from user', timestamp: new Date(NOW - 3 * 60 * 1000).toISOString() }),
    event({ id: 'evt-2', runId: '1234567890abcdef', kind: 'Failed', stepId: 'route_team', status: 'Failed', detail: null, timestamp: new Date(NOW - 2 * 60 * 60 * 1000).toISOString() }),
    event({ id: 'evt-3', runId: 'deadbeef12345678', kind: 'Completed', stepId: 'code_change_generated', status: 'Completed', detail: 'PR created successfully', timestamp: new Date(NOW - 30 * 1000).toISOString() }),
  ];

  describe('Rendering', () => {
    it('shows "No events yet" when events array is empty', () => {
      render(<EventLog events={[]} />);
      expect(screen.getByText('No events yet')).toBeInTheDocument();
    });

    it('renders groups with the truncated run id (first 8 chars)', () => {
      render(<EventLog events={mockEvents} />);

      expect(screen.getByText('abcdef12')).toBeInTheDocument();
      expect(screen.getByText('12345678')).toBeInTheDocument();
      expect(screen.getByText('deadbeef')).toBeInTheDocument();
    });

    it('shows status badges and event kinds', () => {
      render(<EventLog events={mockEvents} />);

      expect(screen.getAllByText('Failed').length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByText('Completed').length).toBeGreaterThanOrEqual(1);
      expect(screen.getByText('Run failed')).toBeInTheDocument();
      expect(screen.getByText('Run completed')).toBeInTheDocument();
    });

    it('shows the step of each event', () => {
      render(<EventLog events={mockEvents} />);

      expect(screen.getAllByText('route_team').length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByText('code_change_generated').length).toBeGreaterThanOrEqual(1);
    });

    it('shows detail text, or a dash when missing', () => {
      render(<EventLog events={mockEvents} />);

      expect(screen.getByText('Email received from user')).toBeInTheDocument();
      expect(screen.getByText('PR created successfully')).toBeInTheDocument();
      expect(screen.getAllByText('—').length).toBeGreaterThanOrEqual(1);
    });

    it('shows relative timestamps', () => {
      render(<EventLog events={mockEvents} />);

      expect(screen.getAllByText('3 min ago').length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByText('2 hours ago').length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByText('30 sec ago').length).toBeGreaterThanOrEqual(1);
    });
  });

  describe('Group toggle', () => {
    it('exposes the expanded state of each run group via aria-expanded', () => {
      render(<EventLog events={mockEvents} />);

      const header = screen.getByRole('button', { name: /abcdef12/ });
      expect(header).toHaveAttribute('aria-expanded', 'true');

      fireEvent.click(header);
      expect(header).toHaveAttribute('aria-expanded', 'false');
      expect(screen.queryByText('Email received from user')).not.toBeInTheDocument();

      fireEvent.click(header);
      expect(header).toHaveAttribute('aria-expanded', 'true');
      expect(screen.getByText('Email received from user')).toBeInTheDocument();
    });
  });

  describe('Event cap', () => {
    const many = (count: number): RunEvent[] => Array.from({ length: count }, (_, i) => event({
      id: `evt-${i}`,
      runId: `run${String(i).padStart(12, '0')}`,
      detail: `Event ${i}`,
      timestamp: new Date(NOW - i * 1000).toISOString(),
    }));

    it('displays at most 200 events when more are provided', () => {
      render(<EventLog events={many(250)} />);

      expect(screen.getAllByRole('listitem')).toHaveLength(200);
    });

    it('displays all events when fewer than 200', () => {
      render(<EventLog events={mockEvents} />);

      expect(screen.getAllByRole('listitem')).toHaveLength(3);
    });

    it('capEvents returns the first 200 items', () => {
      const result = capEvents(many(220));

      expect(result).toHaveLength(200);
      expect(result[0].runId).toBe('run000000000000');
      expect(result[199].runId).toBe('run000000000199');
    });

    it('capEvents returns all items for short or empty arrays', () => {
      expect(capEvents(many(50))).toHaveLength(50);
      expect(capEvents([])).toEqual([]);
    });
  });

  describe('Grouping', () => {
    it('groups events by run preserving the order of first appearance', () => {
      const events = [
        event({ id: '1', runId: 'b' }),
        event({ id: '2', runId: 'a' }),
        event({ id: '3', runId: 'b' }),
      ];

      const groups = groupEventsByRun(events);

      expect([...groups.keys()]).toEqual(['b', 'a']);
      expect(groups.get('b')!.map((e) => e.id)).toEqual(['1', '3']);
    });

    it('describes every event kind', () => {
      expect(describeEventKind('AwaitingInput')).toBe('Awaiting input');
      expect(describeEventKind('InputReceived')).toBe('Input received');
      expect(describeEventKind('StepStarted')).toBe('Started');
    });
  });
});
