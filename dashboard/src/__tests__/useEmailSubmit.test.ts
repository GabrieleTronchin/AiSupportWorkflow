import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import type { IncomingEmail, ApiError } from '../types';
import { useEmailSubmit } from '../hooks/useEmailSubmit';

vi.mock('../api/client', () => ({
  submitEmail: vi.fn(),
}));

import { submitEmail } from '../api/client';

const mockSubmitEmail = vi.mocked(submitEmail);

const testEmail: IncomingEmail = {
  sender: 'user@example.com',
  subject: 'Bug in Application A',
  body: 'The login page crashes when submitting the form.',
};

describe('useEmailSubmit', () => {
  beforeEach(() => {
    mockSubmitEmail.mockResolvedValue({ success: true });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('initial state', () => {
    it('isSubmitting is false', () => {
      const { result } = renderHook(() => useEmailSubmit());
      expect(result.current.isSubmitting).toBe(false);
    });

    it('isSuccess is false', () => {
      const { result } = renderHook(() => useEmailSubmit());
      expect(result.current.isSuccess).toBe(false);
    });

    it('error is null', () => {
      const { result } = renderHook(() => useEmailSubmit());
      expect(result.current.error).toBeNull();
    });
  });

  describe('submission flow', () => {
    it('sets isSubmitting to true during submission', async () => {
      let resolveSubmit: (value: unknown) => void;
      mockSubmitEmail.mockImplementation(
        () => new Promise((resolve) => { resolveSubmit = resolve; })
      );

      const { result } = renderHook(() => useEmailSubmit());

      act(() => {
        result.current.submit(testEmail);
      });

      expect(result.current.isSubmitting).toBe(true);

      await act(async () => {
        resolveSubmit!({ success: true });
      });
    });

    it('calls submitEmail with the provided email', async () => {
      const { result } = renderHook(() => useEmailSubmit());

      await act(async () => {
        await result.current.submit(testEmail);
      });

      expect(mockSubmitEmail).toHaveBeenCalledWith(testEmail);
    });

    it('sets isSubmitting to false after completion', async () => {
      const { result } = renderHook(() => useEmailSubmit());

      await act(async () => {
        await result.current.submit(testEmail);
      });

      expect(result.current.isSubmitting).toBe(false);
    });
  });

  describe('success state', () => {
    it('sets isSuccess to true on successful submission', async () => {
      const { result } = renderHook(() => useEmailSubmit());

      await act(async () => {
        await result.current.submit(testEmail);
      });

      expect(result.current.isSuccess).toBe(true);
    });

    it('error remains null on success', async () => {
      const { result } = renderHook(() => useEmailSubmit());

      await act(async () => {
        await result.current.submit(testEmail);
      });

      expect(result.current.error).toBeNull();
    });
  });

  describe('error state', () => {
    const apiError: ApiError = { statusCode: 500, message: 'Internal server error' };

    beforeEach(() => {
      mockSubmitEmail.mockRejectedValue(apiError);
    });

    it('sets error when submitEmail throws', async () => {
      const { result } = renderHook(() => useEmailSubmit());

      await act(async () => {
        await result.current.submit(testEmail);
      });

      expect(result.current.error).toEqual(apiError);
    });

    it('isSuccess remains false on error', async () => {
      const { result } = renderHook(() => useEmailSubmit());

      await act(async () => {
        await result.current.submit(testEmail);
      });

      expect(result.current.isSuccess).toBe(false);
    });

    it('isSubmitting is false after error', async () => {
      const { result } = renderHook(() => useEmailSubmit());

      await act(async () => {
        await result.current.submit(testEmail);
      });

      expect(result.current.isSubmitting).toBe(false);
    });
  });

  describe('reset', () => {
    it('clears isSuccess back to false', async () => {
      const { result } = renderHook(() => useEmailSubmit());

      await act(async () => {
        await result.current.submit(testEmail);
      });

      expect(result.current.isSuccess).toBe(true);

      act(() => {
        result.current.reset();
      });

      expect(result.current.isSuccess).toBe(false);
    });

    it('clears error back to null', async () => {
      const apiError: ApiError = { statusCode: 400, message: 'Bad request' };
      mockSubmitEmail.mockRejectedValue(apiError);

      const { result } = renderHook(() => useEmailSubmit());

      await act(async () => {
        await result.current.submit(testEmail);
      });

      expect(result.current.error).toEqual(apiError);

      act(() => {
        result.current.reset();
      });

      expect(result.current.error).toBeNull();
    });
  });
});
