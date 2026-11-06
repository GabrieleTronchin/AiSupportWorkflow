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

  it('exposes demoMode and sequentialProcessing from the backend', async () => {
    mockFetchConfig.mockResolvedValue({ sequentialProcessing: true, demoMode: true });

    const { result } = renderHook(() => useConfig());

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.demoMode).toBe(true);
    expect(result.current.sequentialProcessing).toBe(true);
  });

  it('defaults demoMode to false when an older backend omits it', async () => {
    mockFetchConfig.mockResolvedValue({ sequentialProcessing: false } as never);

    const { result } = renderHook(() => useConfig());

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.demoMode).toBe(false);
  });

  it('falls back to defaults when the request fails', async () => {
    mockFetchConfig.mockRejectedValue({ statusCode: 502, message: 'Bad gateway' });

    const { result } = renderHook(() => useConfig());

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.demoMode).toBe(false);
    expect(result.current.sequentialProcessing).toBe(false);
  });
});
