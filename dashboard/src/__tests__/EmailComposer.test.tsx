import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { EmailComposer } from '../components/EmailComposer';

const mockSubmit = vi.fn();
const mockReset = vi.fn();

let mockHookReturn = {
  submit: mockSubmit,
  isSubmitting: false,
  isSuccess: false,
  error: null as { statusCode: number; message: string } | null,
  reset: mockReset,
};

vi.mock('../hooks/useEmailSubmit', () => ({
  useEmailSubmit: () => mockHookReturn,
}));

describe('EmailComposer', () => {
  beforeEach(() => {
    mockSubmit.mockResolvedValue(undefined);
    mockReset.mockImplementation(() => {});
    mockHookReturn = {
      submit: mockSubmit,
      isSubmitting: false,
      isSuccess: false,
      error: null,
      reset: mockReset,
    };
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('Validation', () => {
    it('shows error when subject is empty on submit', async () => {
      render(<EmailComposer />);

      const bodyInput = screen.getByLabelText('Body');
      fireEvent.change(bodyInput, { target: { value: 'Some body text' } });

      const submitButton = screen.getByRole('button', { name: /submit email/i });
      fireEvent.click(submitButton);

      expect(screen.getByText('Subject is required')).toBeInTheDocument();
    });

    it('shows error when body is empty on submit', async () => {
      render(<EmailComposer />);

      const subjectInput = screen.getByLabelText('Subject');
      fireEvent.change(subjectInput, { target: { value: 'Some subject' } });

      const submitButton = screen.getByRole('button', { name: /submit email/i });
      fireEvent.click(submitButton);

      expect(screen.getByText('Body is required')).toBeInTheDocument();
    });

    it('does not show errors when both fields are filled', async () => {
      render(<EmailComposer />);

      const subjectInput = screen.getByLabelText('Subject');
      const bodyInput = screen.getByLabelText('Body');
      fireEvent.change(subjectInput, { target: { value: 'Test subject' } });
      fireEvent.change(bodyInput, { target: { value: 'Test body' } });

      const submitButton = screen.getByRole('button', { name: /submit email/i });
      fireEvent.click(submitButton);

      expect(screen.queryByText('Subject is required')).not.toBeInTheDocument();
      expect(screen.queryByText('Body is required')).not.toBeInTheDocument();
    });

    it('does not submit when validation fails', async () => {
      render(<EmailComposer />);

      const submitButton = screen.getByRole('button', { name: /submit email/i });
      fireEvent.click(submitButton);

      expect(mockSubmit).not.toHaveBeenCalled();
    });
  });

  describe('Submission', () => {
    it('calls submit with correct email data', async () => {
      render(<EmailComposer />);

      const senderInput = screen.getByLabelText('Sender');
      const subjectInput = screen.getByLabelText('Subject');
      const bodyInput = screen.getByLabelText('Body');

      fireEvent.change(senderInput, { target: { value: 'user@example.com' } });
      fireEvent.change(subjectInput, { target: { value: 'Bug report' } });
      fireEvent.change(bodyInput, { target: { value: 'App crashes on login' } });

      const submitButton = screen.getByRole('button', { name: /submit email/i });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(mockSubmit).toHaveBeenCalledWith({
          sender: 'user@example.com',
          subject: 'Bug report',
          body: 'App crashes on login',
        });
      });
    });

    it('disables submit button while submitting', () => {
      mockHookReturn = {
        ...mockHookReturn,
        isSubmitting: true,
      };

      render(<EmailComposer />);

      const submitButton = screen.getByRole('button', { name: /submitting/i });
      expect(submitButton).toBeDisabled();
    });

    it('shows "Submitting..." text while submitting', () => {
      mockHookReturn = {
        ...mockHookReturn,
        isSubmitting: true,
      };

      render(<EmailComposer />);

      expect(screen.getByRole('button', { name: /submitting\.\.\./i })).toBeInTheDocument();
    });
  });

  describe('Success state', () => {
    it('shows success notification after successful submission', () => {
      mockHookReturn = {
        ...mockHookReturn,
        isSuccess: true,
      };

      render(<EmailComposer />);

      expect(screen.getByText('Email submitted successfully!')).toBeInTheDocument();
    });

    it('resets form fields after success', () => {
      mockHookReturn = {
        ...mockHookReturn,
        isSuccess: true,
      };

      render(<EmailComposer />);

      const senderInput = screen.getByLabelText('Sender') as HTMLInputElement;
      const subjectInput = screen.getByLabelText('Subject') as HTMLInputElement;
      const bodyInput = screen.getByLabelText('Body') as HTMLTextAreaElement;

      expect(senderInput.value).toBe('');
      expect(subjectInput.value).toBe('');
      expect(bodyInput.value).toBe('');
    });
  });

  describe('Error state', () => {
    it('shows error message from API', () => {
      mockHookReturn = {
        ...mockHookReturn,
        error: { statusCode: 500, message: 'Internal server error' },
      };

      render(<EmailComposer />);

      expect(screen.getByText('Internal server error')).toBeInTheDocument();
    });

    it('does not reset form fields on error', async () => {
      render(<EmailComposer />);

      const senderInput = screen.getByLabelText('Sender') as HTMLInputElement;
      const subjectInput = screen.getByLabelText('Subject') as HTMLInputElement;
      const bodyInput = screen.getByLabelText('Body') as HTMLTextAreaElement;

      fireEvent.change(senderInput, { target: { value: 'user@test.com' } });
      fireEvent.change(subjectInput, { target: { value: 'My subject' } });
      fireEvent.change(bodyInput, { target: { value: 'My body text' } });

      // Simulate error state by re-rendering with error
      mockHookReturn = {
        ...mockHookReturn,
        error: { statusCode: 400, message: 'Bad request' },
      };

      // Submit the form to trigger the hook
      const submitButton = screen.getByRole('button', { name: /submit email/i });
      fireEvent.click(submitButton);

      // Fields should retain their values
      expect(senderInput.value).toBe('user@test.com');
      expect(subjectInput.value).toBe('My subject');
      expect(bodyInput.value).toBe('My body text');
    });
  });
});
