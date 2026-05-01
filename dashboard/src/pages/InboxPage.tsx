import { useInbox } from '../hooks/useInbox';
import type { InboxStatus } from '../types';

function getStatusBadgeClasses(status: InboxStatus): string {
  switch (status) {
    case 'queued':
      return 'text-amber-400 bg-amber-400/10';
    case 'processed':
      return 'text-emerald-400 bg-emerald-400/10';
    case 'failed':
      return 'text-red-400 bg-red-400/10';
  }
}

export function InboxPage() {
  const { messages, stats, isLoading, error, filter, setFilter } = useInbox();

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-zinc-100">Inbox</h1>
        <select
          value={filter}
          onChange={(e) => setFilter(e.target.value as InboxStatus | 'all')}
          className="rounded-md bg-zinc-800 border border-zinc-700 text-zinc-100 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          <option value="all">All</option>
          <option value="queued">Queued</option>
          <option value="processed">Processed</option>
          <option value="failed">Failed</option>
        </select>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
        <div className="bg-zinc-800 rounded-lg p-4 border border-zinc-700">
          <p className="text-zinc-400 text-sm">Queued</p>
          <p className="text-2xl font-bold text-amber-400">{stats.queued}</p>
        </div>
        <div className="bg-zinc-800 rounded-lg p-4 border border-zinc-700">
          <p className="text-zinc-400 text-sm">Processed</p>
          <p className="text-2xl font-bold text-emerald-400">{stats.processed}</p>
        </div>
        <div className="bg-zinc-800 rounded-lg p-4 border border-zinc-700">
          <p className="text-zinc-400 text-sm">Failed</p>
          <p className="text-2xl font-bold text-red-400">{stats.failed}</p>
        </div>
      </div>

      {isLoading && <p className="text-zinc-400 text-sm">Loading inbox...</p>}
      {error && (
        <p className="text-red-400 text-sm mb-4">
          Failed to load inbox: {error.message}
        </p>
      )}

      {!isLoading && (
        <div className="bg-zinc-900 rounded-lg border border-zinc-700 overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-zinc-400 border-b border-zinc-700">
                <th className="text-left py-2 px-3 font-medium">ID</th>
                <th className="text-left py-2 px-3 font-medium">Status</th>
                <th className="text-left py-2 px-3 font-medium">Received At</th>
                <th className="text-left py-2 px-3 font-medium">Processed At</th>
                <th className="text-left py-2 px-3 font-medium">Error</th>
              </tr>
            </thead>
            <tbody>
              {messages.map((msg) => (
                <tr key={msg.id} className="border-b border-zinc-800">
                  <td className="py-2 px-3 font-mono text-zinc-100">
                    {msg.id.slice(0, 8)}
                  </td>
                  <td className="py-2 px-3">
                    <span className={`inline-block px-2 py-0.5 rounded text-xs font-medium ${getStatusBadgeClasses(msg.status)}`}>
                      {msg.status}
                    </span>
                  </td>
                  <td className="py-2 px-3 text-zinc-400">
                    {new Date(msg.receivedAt).toLocaleString()}
                  </td>
                  <td className="py-2 px-3 text-zinc-400">
                    {msg.processedAt ? new Date(msg.processedAt).toLocaleString() : '—'}
                  </td>
                  <td className="py-2 px-3 text-red-400 truncate max-w-xs">
                    {msg.error ?? '—'}
                  </td>
                </tr>
              ))}
              {messages.length === 0 && (
                <tr>
                  <td colSpan={5} className="py-6 text-center text-zinc-400">
                    No messages
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
