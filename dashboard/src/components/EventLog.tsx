import { useEffect, useRef, useState } from 'react';
import type { StateTransitionEvent } from '../types';
import { formatRelativeTime } from './IssuesList';
import { getStageBadgeClasses } from '../utils/badges';

const EVENT_CAP = 200;

/**
 * Caps the events array to the specified limit.
 * Events are assumed to be newest-first from the parent.
 * Exported for property-based testing.
 */
export function capEvents(events: StateTransitionEvent[], limit = EVENT_CAP): StateTransitionEvent[] {
  return events.slice(0, limit);
}

/**
 * Groups events by issueId, preserving the order of first appearance.
 * Exported for testing.
 */
export function groupEventsByIssue(events: StateTransitionEvent[]): Map<string, StateTransitionEvent[]> {
  const groups = new Map<string, StateTransitionEvent[]>();
  for (const event of events) {
    const existing = groups.get(event.issueId);
    if (existing) {
      existing.push(event);
    } else {
      groups.set(event.issueId, [event]);
    }
  }
  return groups;
}

interface EventLogProps {
  events: StateTransitionEvent[];
}

export function EventLog({ events }: EventLogProps) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const [userHasScrolled, setUserHasScrolled] = useState(false);
  const [collapsedGroups, setCollapsedGroups] = useState<Set<string>>(new Set());

  const cappedEvents = capEvents(events);
  const grouped = groupEventsByIssue(cappedEvents);

  const toggleGroup = (issueId: string) => {
    setCollapsedGroups((prev) => {
      const next = new Set(prev);
      if (next.has(issueId)) {
        next.delete(issueId);
      } else {
        next.add(issueId);
      }
      return next;
    });
  };

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
    <div className="bg-zinc-900 rounded-lg border border-zinc-700 flex flex-col min-h-0 flex-1">
      <div
        ref={scrollRef}
        onScroll={handleScroll}
        className="overflow-y-auto flex-1"
      >
        {cappedEvents.length === 0 ? (
          <p className="text-zinc-400 text-sm py-6 text-center">No events yet</p>
        ) : (
          <div className="divide-y divide-zinc-800">
            {[...grouped.entries()].map(([issueId, issueEvents]) => {
              const isCollapsed = collapsedGroups.has(issueId);
              const latestEvent = issueEvents[0];
              const latestStage = latestEvent.newStage;

              return (
                <div key={issueId}>
                  {/* Group header */}
                  <button
                    onClick={() => toggleGroup(issueId)}
                    className="w-full flex items-center gap-3 py-2.5 px-3 text-sm hover:bg-zinc-800/50 transition-colors text-left"
                  >
                    <span className="text-zinc-500 text-xs shrink-0">
                      {isCollapsed ? '▶' : '▼'}
                    </span>
                    <span className="font-mono text-zinc-100 shrink-0">
                      {issueId.slice(0, 8)}
                    </span>
                    <span className={`inline-block px-1.5 py-0.5 rounded text-xs font-medium ${getStageBadgeClasses(latestStage)}`}>
                      {latestStage}
                    </span>
                    <span className="text-zinc-500 text-xs">
                      {issueEvents.length} event{issueEvents.length > 1 ? 's' : ''}
                    </span>
                    <span className="text-zinc-500 text-xs ml-auto shrink-0">
                      {formatRelativeTime(latestEvent.timestamp)}
                    </span>
                  </button>

                  {/* Group events */}
                  {!isCollapsed && (
                    <ul className="border-t border-zinc-800/50">
                      {issueEvents.map((event, index) => (
                        <li
                          key={`${event.id}-${index}`}
                          className="py-1.5 px-3 pl-10 flex items-center gap-3 text-sm"
                        >
                          <span className="text-zinc-500 shrink-0">
                            {event.previousStage ? (
                              <>
                                <span className={`inline-block px-1.5 py-0.5 rounded text-xs font-medium ${getStageBadgeClasses(event.previousStage)}`}>
                                  {event.previousStage}
                                </span>
                                <span className="mx-1">→</span>
                              </>
                            ) : null}
                            <span className={`inline-block px-1.5 py-0.5 rounded text-xs font-medium ${getStageBadgeClasses(event.newStage)}`}>
                              {event.newStage}
                            </span>
                          </span>
                          <span className="text-zinc-400 truncate flex-1">
                            {event.detail ?? '—'}
                          </span>
                          <span className="text-zinc-500 text-xs shrink-0">
                            {formatRelativeTime(event.timestamp)}
                          </span>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
