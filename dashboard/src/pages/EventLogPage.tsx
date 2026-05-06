import { useEvents } from '../hooks/useEvents';
import { EventLog } from '../components/EventLog';

export function EventLogPage() {
  const { events, isLoading, error } = useEvents();

  return (
    <div className="flex flex-col h-full">
      <h1 className="text-2xl font-bold text-zinc-100 mb-6">Event Log</h1>
      {isLoading && <p className="text-zinc-400 text-sm">Loading events...</p>}
      {error && (
        <p className="text-red-400 text-sm mb-4">
          Failed to load events: {error.message}
        </p>
      )}
      {!isLoading && <EventLog events={events} />}
    </div>
  );
}
