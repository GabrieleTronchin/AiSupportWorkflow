import { useIssues } from '../hooks/useIssues';
import { EventLog } from '../components/EventLog';

export function EventLogPage() {
  const { issues } = useIssues();

  // Sort issues newest first by lastUpdated
  const sortedEvents = [...issues].sort(
    (a, b) => new Date(b.lastUpdated).getTime() - new Date(a.lastUpdated).getTime()
  );

  return (
    <div>
      <h1 className="text-2xl font-bold text-zinc-100 mb-6">Event Log</h1>
      <EventLog events={sortedEvents} />
    </div>
  );
}
