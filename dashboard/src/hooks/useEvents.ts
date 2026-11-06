import { useState, useEffect, useCallback } from 'react';
import type { ApiError, RunEvent } from '../types';
import { fetchEvents } from '../api/client';
import { useGrpcStream } from './useGrpcStream';

export function useEvents() {
  const [events, setEvents] = useState<RunEvent[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);
  const { lastUpdate } = useGrpcStream();

  const loadEvents = useCallback(async () => {
    try {
      const data = await fetchEvents();
      setEvents(data);
      setError(null);
    } catch (err) {
      setError(err as ApiError);
    } finally {
      setIsLoading(false);
    }
  }, []);

  // Fetch initial events on mount
  useEffect(() => {
    loadEvents();
  }, [loadEvents]);

  // Re-fetch events when the stream reports a new transition
  useEffect(() => {
    if (!lastUpdate) return;
    loadEvents();
  }, [lastUpdate, loadEvents]);

  return { events, isLoading, error };
}
