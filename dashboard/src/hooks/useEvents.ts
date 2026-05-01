import { useState, useEffect } from 'react';
import type { StateTransitionEvent, ApiError } from '../types';

export function useEvents() {
  const [events, setEvents] = useState<StateTransitionEvent[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function loadEvents() {
      try {
        setIsLoading(true);
        const response = await fetch('/api/support/events');
        if (!response.ok) {
          throw { statusCode: response.status, message: `Request failed with status ${response.status}` };
        }
        const data = (await response.json()) as StateTransitionEvent[];
        if (!cancelled) {
          setEvents(data);
          setError(null);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err as ApiError);
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    loadEvents();

    return () => {
      cancelled = true;
    };
  }, []);

  return { events, isLoading, error };
}
