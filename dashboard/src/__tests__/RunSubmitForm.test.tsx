import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { RunSubmitForm } from '../components/RunSubmitForm';
import type { WorkflowInfo } from '../types';
import { workflow } from './fixtures';

const mockSubmit = vi.fn();
const mockReset = vi.fn();

let mockHookReturn = {
  submit: mockSubmit,
  isSubmitting: false,
  isSuccess: false,
  error: null as { statusCode: number; message: string } | null,
  accepted: null,
  reset: mockReset,
};

vi.mock('../hooks/useRunSubmit', () => ({
  useRunSubmit: () => mockHookReturn,
}));

describe('RunSubmitForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSubmit.mockResolvedValue(undefined);
    mockHookReturn = { submit: mockSubmit, isSubmitting: false, isSuccess: false, error: null, accepted: null, reset: mockReset };
  });

  describe('Fields from the input schema', () => {
    it('renders one field per schema property with its title', () => {
      render(<RunSubmitForm workflow={workflow} />);

      expect(screen.getByLabelText('Sender')).toBeInstanceOf(HTMLInputElement);
      expect(screen.getByLabelText(/^Subject/)).toBeInstanceOf(HTMLInputElement);
      // x-multiline renders a textarea
      expect(screen.getByLabelText(/^Body/)).toBeInstanceOf(HTMLTextAreaElement);
    });

    it('renders selects, checkboxes and numbers for enum, boolean and number properties', () => {
      const custom: WorkflowInfo = {
        ...workflow,
        samples: [],
        inputSchema: {
          type: 'object',
          required: ['size'],
          properties: {
            color: { type: 'string', enum: ['red', 'green'] },
            urgent: { type: 'boolean', title: 'Urgent' },
            size: { type: 'integer', title: 'Size' },
          },
        },
      };

      render(<RunSubmitForm workflow={custom} />);

      expect(screen.getByLabelText('Color')).toBeInstanceOf(HTMLSelectElement);
      expect(screen.getByLabelText('Urgent')).toHaveAttribute('type', 'checkbox');
      expect(screen.getByLabelText(/^Size/)).toHaveAttribute('type', 'number');
      expect(screen.queryByLabelText('Template')).not.toBeInTheDocument();
    });
  });

  describe('Validation', () => {
    it('shows an error when a required field is empty on submit', () => {
      render(<RunSubmitForm workflow={workflow} />);

      fireEvent.change(screen.getByLabelText(/^Body/), { target: { value: 'Some body text' } });
      fireEvent.click(screen.getByRole('button', { name: 'Submit' }));

      expect(screen.getByText('Subject is required')).toBeInTheDocument();
      expect(mockSubmit).not.toHaveBeenCalled();
    });

    it('rejects whitespace-only values', () => {
      render(<RunSubmitForm workflow={workflow} />);

      fireEvent.change(screen.getByLabelText(/^Subject/), { target: { value: '   ' } });
      fireEvent.change(screen.getByLabelText(/^Body/), { target: { value: 'Body' } });
      fireEvent.click(screen.getByRole('button', { name: 'Submit' }));

      expect(screen.getByText('Subject is required')).toBeInTheDocument();
    });

    it('does not show errors when the required fields are filled', () => {
      render(<RunSubmitForm workflow={workflow} />);

      fireEvent.change(screen.getByLabelText(/^Subject/), { target: { value: 'Test subject' } });
      fireEvent.change(screen.getByLabelText(/^Body/), { target: { value: 'Test body' } });
      fireEvent.click(screen.getByRole('button', { name: 'Submit' }));

      expect(screen.queryByText('Subject is required')).not.toBeInTheDocument();
      expect(screen.queryByText('Body is required')).not.toBeInTheDocument();
    });

    it('shows errors immediately when an invalid template is selected', () => {
      render(<RunSubmitForm workflow={workflow} />);

      fireEvent.change(screen.getByLabelText('Template'), { target: { value: 'edge-empty-input' } });

      expect(screen.getByText('Subject is required')).toBeInTheDocument();
      expect(screen.getByText('Body is required')).toBeInTheDocument();
    });
  });

  describe('Templates', () => {
    it('groups the workflow samples', () => {
      render(<RunSubmitForm workflow={workflow} />);

      const groups = Array.from(screen.getByLabelText('Template').querySelectorAll('optgroup')).map((g) => g.label);
      expect(groups).toEqual(['Application A', 'Application B', 'Edge Cases']);
    });

    it('fills every field from the selected sample', () => {
      render(<RunSubmitForm workflow={workflow} />);

      fireEvent.change(screen.getByLabelText('Template'), { target: { value: 'scenario-b1' } });

      expect((screen.getByLabelText('Sender') as HTMLInputElement).value).toBe('security.team@company.com');
      expect((screen.getByLabelText(/^Subject/) as HTMLInputElement).value).toBe('SQL Injection in UserController - Application B');
      expect((screen.getByLabelText(/^Body/) as HTMLTextAreaElement).value).toBe('Critical vulnerability.');
    });
  });

  describe('Submission', () => {
    it('submits the input to the workflow', async () => {
      render(<RunSubmitForm workflow={workflow} />);

      fireEvent.change(screen.getByLabelText('Sender'), { target: { value: 'user@example.com' } });
      fireEvent.change(screen.getByLabelText(/^Subject/), { target: { value: 'Bug report' } });
      fireEvent.change(screen.getByLabelText(/^Body/), { target: { value: 'App crashes on login' } });
      fireEvent.click(screen.getByRole('button', { name: 'Submit' }));

      await waitFor(() => {
        expect(mockSubmit).toHaveBeenCalledWith('support-email', {
          sender: 'user@example.com',
          subject: 'Bug report',
          body: 'App crashes on login',
        });
      });
    });

    it('omits empty optional fields', async () => {
      render(<RunSubmitForm workflow={workflow} />);

      fireEvent.change(screen.getByLabelText(/^Subject/), { target: { value: 'S' } });
      fireEvent.change(screen.getByLabelText(/^Body/), { target: { value: 'B' } });
      fireEvent.click(screen.getByRole('button', { name: 'Submit' }));

      await waitFor(() => expect(mockSubmit).toHaveBeenCalledWith('support-email', { subject: 'S', body: 'B' }));
    });

    it('disables the button and shows "Submitting..." while submitting', () => {
      mockHookReturn = { ...mockHookReturn, isSubmitting: true };

      render(<RunSubmitForm workflow={workflow} />);

      expect(screen.getByRole('button', { name: 'Submitting...' })).toBeDisabled();
    });

    it('disables submission when the module is unavailable', () => {
      render(<RunSubmitForm workflow={{ ...workflow, available: false }} />);

      expect(screen.getByRole('button', { name: 'Submit' })).toBeDisabled();
      expect(screen.getByRole('alert')).toHaveTextContent('unavailable');
    });
  });

  describe('Success and errors', () => {
    it('shows a success notification and resets the fields', () => {
      mockHookReturn = { ...mockHookReturn, isSuccess: true };

      render(<RunSubmitForm workflow={workflow} />);

      expect(screen.getByText('Submitted successfully!')).toBeInTheDocument();
      expect((screen.getByLabelText(/^Subject/) as HTMLInputElement).value).toBe('');
      expect((screen.getByLabelText(/^Body/) as HTMLTextAreaElement).value).toBe('');
    });

    it('shows the API error message', () => {
      mockHookReturn = { ...mockHookReturn, error: { statusCode: 400, message: 'Subject and Body are required.' } };

      render(<RunSubmitForm workflow={workflow} />);

      expect(screen.getByText('Subject and Body are required.')).toBeInTheDocument();
    });

    it('keeps the field values when the submission fails', () => {
      render(<RunSubmitForm workflow={workflow} />);
      fireEvent.change(screen.getByLabelText(/^Subject/), { target: { value: 'My subject' } });
      fireEvent.change(screen.getByLabelText(/^Body/), { target: { value: 'My body text' } });

      mockHookReturn = { ...mockHookReturn, error: { statusCode: 503, message: 'Module unavailable' } };
      fireEvent.click(screen.getByRole('button', { name: 'Submit' }));

      expect((screen.getByLabelText(/^Subject/) as HTMLInputElement).value).toBe('My subject');
      expect((screen.getByLabelText(/^Body/) as HTMLTextAreaElement).value).toBe('My body text');
    });
  });
});
