import { useState } from 'react';
import { useInbox } from '../hooks/useInbox';
import { getInboxStatusBadgeClasses } from '../utils/badges';
import type { InboxMessage, InboxStatus } from '../types';

interface ParsedPayload {
  sender?: string;
  subject?: string;
  body?: string;
}

function parsePayload(payload: string | null): ParsedPayload | null {
  if (!payload) return null;
  try {
    const raw = JSON.parse(payload) as Record<string, unknown>;
    // Handle both PascalCase (C# default JsonSerializer) and camelCase
    return {
      sender: (raw.sender ?? raw.Sender) as string | undefined,
      subject: (raw.subject ?? raw.Subject) as string | undefined,
      body: (raw.body ?? raw.Body) as string | undefined,
    };
  } catch {
    return null;
  }
}

export function InboxPage() {
  const { messages, stats, isLoading, error, filter, setFilter } = useInbox();
  const [selectedMessage, setSelectedMessage] = useState<InboxMessage | null>(null);

  const parsed = selectedMessage ? parsePayload(selectedMessage.payload) : null;

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
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Table */}
          <div className={`${selectedMessage ? 'lg:col-span-2' : 'lg:col-span-3'} bg-zinc-900 rounded-lg border border-zinc-700 overflow-hidden`}>
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
                  <tr
                    key={msg.id}
                    onClick={() => setSelectedMessage(msg)}
                    className={`border-b border-zinc-800 cursor-pointer transition-colors hover:bg-zinc-800 ${
                      selectedMessage?.id === msg.id ? 'bg-zinc-800/80 ring-1 ring-inset ring-blue-500/40' : ''
                    }`}
                  >
                    <td className="py-2 px-3 font-mono text-zinc-100">
                      {msg.id.slice(0, 8)}
                    </td>
                    <td className="py-2 px-3">
                      <span className={`inline-block px-2 py-0.5 rounded text-xs font-medium ${getInboxStatusBadgeClasses(msg.status)}`}>
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

          {/* Detail panel */}
          {selectedMessage && (
            <div className="lg:col-span-1 bg-zinc-900 rounded-lg border border-zinc-700 p-4">
              <div className="flex items-center justify-between mb-4">
                <h2 className="text-sm font-semibold text-zinc-100">Message Detail</h2>
                <button
                  onClick={() => setSelectedMessage(null)}
                  className="text-zinc-400 hover:text-zinc-100 text-lg leading-none"
                  aria-label="Close detail panel"
                >
                  ×
                </button>
              </div>

              <div className="space-y-3 text-sm">
                <div>
                  <p className="text-zinc-500 text-xs uppercase tracking-wide">ID</p>
                  <p className="text-zinc-100 font-mono break-all">{selectedMessage.id}</p>
                </div>
                <div>
                  <p className="text-zinc-500 text-xs uppercase tracking-wide">Status</p>
                  <span className={`inline-block px-2 py-0.5 rounded text-xs font-medium ${getInboxStatusBadgeClasses(selectedMessage.status)}`}>
                    {selectedMessage.status}
                  </span>
                </div>
                <div>
                  <p className="text-zinc-500 text-xs uppercase tracking-wide">Received</p>
                  <p className="text-zinc-300">{new Date(selectedMessage.receivedAt).toLocaleString()}</p>
                </div>
                {selectedMessage.processedAt && (
                  <div>
                    <p className="text-zinc-500 text-xs uppercase tracking-wide">Processed</p>
                    <p className="text-zinc-300">{new Date(selectedMessage.processedAt).toLocaleString()}</p>
                  </div>
                )}
                {selectedMessage.error && (
                  <div>
                    <p className="text-zinc-500 text-xs uppercase tracking-wide">Error</p>
                    <p className="text-red-400">{selectedMessage.error}</p>
                  </div>
                )}

                {parsed && (
                  <>
                    <hr className="border-zinc-700" />
                    <h3 className="text-xs font-semibold text-zinc-400 uppercase tracking-wide">Email Content</h3>
                    {parsed.sender && (
                      <div>
                        <p className="text-zinc-500 text-xs uppercase tracking-wide">Sender</p>
                        <p className="text-zinc-100">{parsed.sender}</p>
                      </div>
                    )}
                    {parsed.subject && (
                      <div>
                        <p className="text-zinc-500 text-xs uppercase tracking-wide">Subject</p>
                        <p className="text-zinc-100">{parsed.subject}</p>
                      </div>
                    )}
                    {parsed.body && (
                      <div>
                        <p className="text-zinc-500 text-xs uppercase tracking-wide">Body</p>
                        <p className="text-zinc-300 whitespace-pre-wrap">{parsed.body}</p>
                      </div>
                    )}
                  </>
                )}

                {!parsed && selectedMessage.payload && (
                  <>
                    <hr className="border-zinc-700" />
                    <div>
                      <p className="text-zinc-500 text-xs uppercase tracking-wide">Raw Payload</p>
                      <pre className="text-zinc-300 text-xs whitespace-pre-wrap bg-zinc-800 rounded p-2 mt-1 overflow-auto max-h-48">
                        {selectedMessage.payload}
                      </pre>
                    </div>
                  </>
                )}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
