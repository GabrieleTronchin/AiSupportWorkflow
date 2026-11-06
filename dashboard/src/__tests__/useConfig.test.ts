import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useConfig } from '../hooks/useConfig';

vi.mock('../api/client', () => ({
  fetchConfig: vi.fn(),
}));

import { fetchConfig } from '../api/client';

const mockFetchConfig = vi.mocked(fetchConfig);

describe('useConfig', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('exposes demo mode, provider and model from the backend', async () => {
    mockFetchConfig.mockResolvedValue({
      sequentialProcessing: true,
      demoMode: true,
      llmProvider: 'Demo',
      llmModel: 'demo-simulated',
    });

    const { result } = renderHook(() => useConfig());

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.demoMode).toBe(true);
    expect(result.current.sequentialProcessing).toBe(true);
    expect(result.current.llmProvider).toBe('Demo');
    expect(result.current.llmModel).toBe('demo-simulated');
  });

  it('exposes the real model when not in demo mode', async () => {
    mockFetchConfig.mockResolvedValue({
      sequentialProcessing: false,
      demoMode: false,
      llmProvider: 'OpenAI',
      llmModel: 'gpt-4o-mini',
    });

    const { result } = renderHook(() => useConfig());

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.demoMode).toBe(false);
    expect(result.current.llmProvider).toBe('OpenAI');
    expect(result.current.llmModel).toBe('gpt-4o-mini');
  });

  it('defaults new fields when an older backend omits them', async () => {
    mockFetchConfig.mockResolvedValue({ sequentialProcessing: false } as never);

    const { result } = renderHook(() => useConfig());

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.demoMode).toBe(false);
    expect(result.current.llmProvider).toBeNull();
    expect(result.current.llmModel).toBeNull();
  });

  it('falls back to defaults when the request fails', async () => {
    mockFetchConfig.mockRejectedValue({ statusCode: 502, message: 'Bad gateway' });

    const { result } = renderHook(() => useConfig());

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.demoMode).toBe(false);
    expect(result.current.sequentialProcessing).toBe(false);
    expect(result.current.llmModel).toBeNull();
  });
});
