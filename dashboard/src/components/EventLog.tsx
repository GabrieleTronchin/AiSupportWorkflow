import { useEffect, useRef, useState } from 'react';
import type { RunEvent, RunEventKind } from '../types';
import { formatRelativeTime } from '../utils/runs';
import { formatStatus, getStatusBadgeClasses } from '../utils/badges';
import { useWorkflowCatalog } from '../hooks/WorkflowCatalogContext';

const EVENT_CAP = 200;

/**
 * Caps the events array to the specified limit.
 * Events are assumed to be newest-first from the parent.
 */
export function capEvents(events: RunEvent[], limit = EVENT_CAP): RunEvent[] {
  return events.slice(0, limit);
}

/** Groups events by run, preserving the order of first appearance. */
export function groupEventsByRun(events: RunEvent[]): Map<string, RunEvent[]> {
  const groups = new Map<string, RunEvent[]>();
  for (const event of events) {
    const existing = groups.get(event.runId);
    if (existing) {
      existing.push(event);
    } else {
      groups.set(event.runId, [event]);
    }
  }
  return groups;
}

const KIND_LABELS: Record<RunEventKind, string> = {
  Queued: 'Queued',
  StepStarted: 'Started',
  StepCompleted: 'Completed step',
  AwaitingInput: 'Awaiting input',
  InputReceived: 'Input received',
  Completed: 'Run completed',
  Failed: 'Run failed',
  Cancelled: 'Run cancelled',
};

export function describeEventKind(kind: RunEventKind): string {
  return KIND_LABELS[kind] ?? kind;
}

interface EventLogProps {
  events: RunEvent[];
}

export function EventLog({ events }: EventLogProps) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const [userHasScrolled, setUserHasScrolled] = useState(false);
  const [collapsedGroups, setCollapsedGroups] = useState<Set<string>>(new Set());
  const { stepName, workflowName } = useWorkflowCatalog();

  const cappedEvents = capEvents(events);
  const grouped = groupEventsByRun(cappedEvents);

  const toggleGroup = (runId: string) => {
    setCollapsedGroups((prev) => {
      const next = new Set(prev);
      if (next.has(runId)) {
        next.delete(runId);
      } else {
        next.add(runId);
      }
      return next;
    });
  };

  const handleScroll = () => {
    const container = scrollRef.current;
    if (!container) return;
    setUserHasScrolled(container.scrollTop > 0);
  };

  // Auto-scroll to top on new events unless the user has scrolled down
  useEffect(() => {
    const container = scrollRef.current;
    if (!container) return;
    if (!userHasScrolled) {
      container.scrollTop = 0;
    }
  }, [events, userHasScrolled]);

  return (
    <div className="bg-zinc-900 rounded-lg border border-zinc-700 flex flex-col min-h-0 flex-1">
      <div ref={scrollRef} onScroll={handleScroll} className="overflow-y-auto flex-1">
        {cappedEvents.length === 0 ? (
          <p className="text-zinc-400 text-sm py-6 text-center">No events yet</p>
        ) : (
          <div className="divide-y divide-zinc-800">
            {[...grouped.entries()].map(([runId, runEvents]) => {
              const isCollapsed = collapsedGroups.has(runId);
              const latest = runEvents[0];

              return (
                <div key={runId}>
                  <button
                    onClick={() => toggleGroup(runId)}
                    aria-expanded={!isCollapsed}
                    className="w-full flex items-center gap-3 py-2.5 px-3 text-sm hover:bg-zinc-800/50 transition-colors text-left"
                  >
                    <span className="text-zinc-500 text-xs shrink-0" aria-hidden="true">
                      {isCollapsed ? '▶' : '▼'}
                    </span>
                    <span className="font-mono text-zinc-100 shrink-0">{runId.slice(0, 8)}</span>
                    <span className="text-zinc-400 text-xs shrink-0">{workflowName(latest.workflowId)}</span>
                    <span className={`inline-block px-1.5 py-0.5 rounded text-xs font-medium ${getStatusBadgeClasses(latest.status)}`}>
                      {formatStatus(latest.status)}
                    </span>
                    <span className="text-zinc-300 text-xs truncate">{stepName(latest.workflowId, latest.stepId)}</span>
                    <span className="text-zinc-500 text-xs">
                      {runEvents.length} event{runEvents.length > 1 ? 's' : ''}
                    </span>
                    <span className="text-zinc-500 text-xs ml-auto shrink-0">{formatRelativeTime(latest.timestamp)}</span>
                  </button>

                  {!isCollapsed && (
                    <ul className="border-t border-zinc-800/50">
                      {runEvents.map((event, index) => (
                        <li key={`${event.id}-${index}`} className="py-1.5 px-3 pl-10 flex items-center gap-3 text-sm">
                          <span className="text-zinc-500 shrink-0 w-28 text-xs">{describeEventKind(event.kind)}</span>
                          <span className="text-zinc-200 shrink-0 text-xs">{stepName(event.workflowId, event.stepId)}</span>
                          <span className={`inline-block px-1.5 py-0.5 rounded text-xs font-medium shrink-0 ${getStatusBadgeClasses(event.status)}`}>
                            {formatStatus(event.status)}
                          </span>
                          <span className="text-zinc-400 truncate flex-1">{event.detail ?? '—'}</span>
                          <span className="text-zinc-500 text-xs shrink-0">{formatRelativeTime(event.timestamp)}</span>
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
