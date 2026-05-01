import { useEffect, useRef, useState } from 'react';
import type { WorkflowState, WorkflowStage } from '../types';
import { formatRelativeTime } from './IssuesList';

const EVENT_CAP = 100;

/**
 * Caps the events array to the specified limit.
 * Events are assumed to be newest-first from the parent.
 * Exported for property-based testing.
 */
export function capEvents(events: WorkflowState[], limit = EVENT_CAP): WorkflowState[] {
  return events.slice(0, limit);
}

const terminalStages: WorkflowStage[] = ['Failed', 'ClassifiedOutOfScope', 'ManualReviewRequired'];
const completedStages: WorkflowStage[] = ['CodeChangeGenerated', 'Resolved'];

function getStageBadgeClasses(stage: WorkflowStage): string {
  if (terminalStages.includes(stage)) {
    return 'text-red-400 bg-red-400/10';
  }
  if (completedStages.includes(stage)) {
    return 'text-emerald-400 bg-emerald-400/10';
  }
  return 'text-blue-400 bg-blue-400/10';
}

interface EventLogProps {
  events: WorkflowState[];
}

export function EventLog({ events }: EventLogProps) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const [userHasScrolled, setUserHasScrolled] = useState(false);

  const cappedEvents = capEvents(events);

  // Track whether the user has scrolled down
  const handleScroll = () => {
    const container = scrollRef.current;
    if (!container) return;
    setUserHasScrolled(container.scrollTop > 0);
  };

  // Auto-scroll to top on new events unless user has scrolled down
  useEffect(() => {
    const container = scrollRef.current;
    if (!container) return;

    if (!userHasScrolled) {
      container.scrollTop = 0;
    }
  }, [events, userHasScrolled]);

  return (
    <div className="bg-zinc-900 rounded-lg border border-zinc-700 flex flex-col max-h-[600px]">
      <div
        ref={scrollRef}
        onScroll={handleScroll}
        className="overflow-y-auto flex-1"
      >
        {cappedEvents.length === 0 ? (
          <p className="text-zinc-400 text-sm py-6 text-center">No events yet</p>
        ) : (
          <ul>
            {cappedEvents.map((event, index) => (
              <li
                key={`${event.issueId}-${event.lastUpdated}-${index}`}
                className="border-b border-zinc-800 py-2 px-3 flex items-center gap-3 text-sm"
              >
                <span className="font-mono text-zinc-100 shrink-0">
                  {event.issueId.slice(0, 8)}
                </span>
                <span
                  className={`inline-block px-2 py-0.5 rounded text-xs font-medium shrink-0 ${getStageBadgeClasses(event.stage)}`}
                >
                  {event.stage}
                </span>
                <span className="text-zinc-400 truncate flex-1">
                  {event.detail ?? '—'}
                </span>
                <span className="text-zinc-500 text-xs shrink-0">
                  {formatRelativeTime(event.lastUpdated)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
