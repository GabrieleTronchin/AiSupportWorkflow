import { useState, useEffect } from 'react';
import { fetchConfig, type RuntimeConfig } from '../api/client';

const DEFAULT_CONFIG: RuntimeConfig = {
  sequentialProcessing: false,
  demoMode: false,
  llmProvider: null,
  llmModel: null,
};

const asNonEmptyString = (value: unknown): string | null =>
  typeof value === 'string' && value.trim() !== '' ? value : null;

export function useConfig() {
  const [config, setConfig] = useState<RuntimeConfig>(DEFAULT_CONFIG);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function loadConfig() {
      try {
        const data = await fetchConfig();
        if (!cancelled) {
          setConfig({
            sequentialProcessing: data.sequentialProcessing === true,
            demoMode: data.demoMode === true,
            llmProvider: asNonEmptyString(data.llmProvider),
            llmModel: asNonEmptyString(data.llmModel),
          });
        }
      } catch {
        if (!cancelled) {
          setConfig(DEFAULT_CONFIG);
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

  return { ...config, isLoading };
}
