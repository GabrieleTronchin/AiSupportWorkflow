import { useState, useCallback } from 'react';
import type { IncomingEmail, ApiError } from '../types';
import { submitEmail } from '../api/client';

export function useEmailSubmit() {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSuccess, setIsSuccess] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const submit = useCallback(async (email: IncomingEmail) => {
    setIsSubmitting(true);
    setIsSuccess(false);
    setError(null);

    try {
      await submitEmail(email);
      setIsSuccess(true);
    } catch (err) {
      setError(err as ApiError);
    } finally {
      setIsSubmitting(false);
    }
  }, []);

  const reset = useCallback(() => {
    setIsSubmitting(false);
    setIsSuccess(false);
    setError(null);
  }, []);

  return { submit, isSubmitting, isSuccess, error, reset };
}
