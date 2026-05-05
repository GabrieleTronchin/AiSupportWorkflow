import { useState, useEffect } from 'react';
import { fetchConfig } from '../api/client';

export function useConfig() {
  const [sequentialProcessing, setSequentialProcessing] = useState(false);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function loadConfig() {
      try {
        const data = await fetchConfig();
        if (!cancelled) {
          setSequentialProcessing(data.sequentialProcessing);
        }
      } catch {
        if (!cancelled) {
          setSequentialProcessing(false);
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    loadConfig();

    return () => {
      cancelled = true;
    };
  }, []);

  return { sequentialProcessing, isLoading };
}
