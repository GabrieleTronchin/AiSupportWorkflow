import { useState, useCallback } from 'react';
import type { ApiError, IntakeAccepted } from '../types';
import { submitRun } from '../api/client';

/** Submits an input to a workflow and tracks the request state. */
export function useRunSubmit() {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSuccess, setIsSuccess] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [accepted, setAccepted] = useState<IntakeAccepted | null>(null);

  const submit = useCallback(async (workflowId: string, input: Record<string, unknown>) => {
    setIsSubmitting(true);
    setIsSuccess(false);
    setError(null);
    try {
      setAccepted(await submitRun(workflowId, input));
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
    setAccepted(null);
  }, []);

  return { submit, isSubmitting, isSuccess, error, accepted, reset };
}
