import type { WorkflowState } from '../types';

export function createSSEConnection(
  url: string,
  onMessage: (states: WorkflowState[]) => void,
  onError?: (event: Event) => void,
): { close: () => void } {
  const eventSource = new EventSource(url);

  eventSource.onmessage = (event: MessageEvent) => {
    try {
      const data = JSON.parse(event.data) as WorkflowState[];
      onMessage(data);
    } catch {
      // Ignore malformed messages
    }
  };

  if (onError) {
    eventSource.onerror = onError;
  }

  return {
    close: () => eventSource.close(),
  };
}
