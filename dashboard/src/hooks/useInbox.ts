import { useState, useEffect, useCallback } from 'react';
import type { InboxMessage, InboxStats, InboxStatus, ApiError } from '../types';

export function useInbox(pollInterval = 5000) {
  const [messages, setMessages] = useState<InboxMessage[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);
  const [filter, setFilter] = useState<InboxStatus | 'all'>('all');

  const loadMessages = useCallback(async () => {
    try {
      const response = await fetch('/api/support/inbox');
      if (!response.ok) {
        throw { statusCode: response.status, message: `Request failed with status ${response.status}` };
      }
      const data = (await response.json()) as InboxMessage[];
      setMessages(data);
      setError(null);
    } catch (err) {
      setError(err as ApiError);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadMessages();

    const intervalId = setInterval(loadMessages, pollInterval);

    return () => {
      clearInterval(intervalId);
    };
  }, [loadMessages, pollInterval]);

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
