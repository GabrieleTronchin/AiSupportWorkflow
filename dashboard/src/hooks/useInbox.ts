import { useState, useEffect, useCallback } from 'react';
import type { InboxMessage, InboxStats, InboxStatus, ApiError } from '../types';
import { fetchInbox } from '../api/client';
import { useGrpcStream } from './useGrpcStream';

export function useInbox() {
  const [messages, setMessages] = useState<InboxMessage[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);
  const [filter, setFilter] = useState<InboxStatus | 'all'>('all');
  const { lastUpdate } = useGrpcStream();

  const loadMessages = useCallback(async () => {
    try {
      const data = await fetchInbox();
      setMessages(data);
      setError(null);
    } catch (err) {
      setError(err as ApiError);
    } finally {
      setIsLoading(false);
    }
  }, []);

  // Fetch initial inbox on mount
  useEffect(() => {
    loadMessages();
  }, [loadMessages]);

  // Re-fetch when the stream reports a transition (a queued message may have been processed)
  useEffect(() => {
    if (!lastUpdate) return;
    loadMessages();
  }, [lastUpdate, loadMessages]);

  const stats: InboxStats = {
    queued: messages.filter((m) => m.status === 'queued').length,
    processed: messages.filter((m) => m.status === 'processed').length,
    failed: messages.filter((m) => m.status === 'failed').length,
  };

  const filteredMessages = filter === 'all'
    ? messages
    : messages.filter((m) => m.status === filter);

  return { messages: filteredMessages, stats, isLoading, error, filter, setFilter };
}
