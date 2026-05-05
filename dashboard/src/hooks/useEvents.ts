import { useState, useEffect, useCallback } from 'react';
import type { StateTransitionEvent, ApiError } from '../types';
import { fetchEvents } from '../api/client';
import { useGrpcStream } from './useGrpcStream';

export function useEvents() {
  const [events, setEvents] = useState<StateTransitionEvent[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);
  const { latestStates } = useGrpcStream();

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

  // Re-fetch events when gRPC stream reports a new state transition
  useEffect(() => {
    if (latestStates.length === 0) return;
    loadEvents();
  }, [latestStates, loadEvents]);

  return { events, isLoading, error };
}
