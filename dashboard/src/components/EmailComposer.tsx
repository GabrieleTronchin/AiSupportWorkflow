import { useState, useCallback, useEffect } from 'react';
import { useEmailSubmit } from '../hooks/useEmailSubmit';
import { EMAIL_TEMPLATES } from './emailTemplates';

/**
 * Validates email form fields.
 * Exported for independent testing (property-based tests).
 */
export function validateEmail(subject: string, body: string): { subject?: string; body?: string } {
  const errors: { subject?: string; body?: string } = {};
  if (!subject.trim()) errors.subject = 'Subject is required';
  if (!body.trim()) errors.body = 'Body is required';
  return errors;
}

export function EmailComposer() {
  const [sender, setSender] = useState('');
  const [subject, setSubject] = useState('');
  const [body, setBody] = useState('');
  const [errors, setErrors] = useState<{ subject?: string; body?: string }>({});
  const [showSuccess, setShowSuccess] = useState(false);

  const { submit, isSubmitting, isSuccess, error, reset } = useEmailSubmit();

  useEffect(() => {
    if (isSuccess) {
      setSender('');
      setSubject('');
      setBody('');
      setErrors({});
      setShowSuccess(true);
      const timer = setTimeout(() => {
        setShowSuccess(false);
        reset();
      }, 3000);
      return () => clearTimeout(timer);
    }
  }, [isSuccess, reset]);

  const handleSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();

      const validationErrors = validateEmail(subject, body);
      if (Object.keys(validationErrors).length > 0) {
        setErrors(validationErrors);
        return;
      }

      setErrors({});
      await submit({ sender, subject, body });
    },
    [sender, subject, body, submit]
  );

  const handleTemplateChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      const templateId = e.target.value;
      if (!templateId) return;
      const template = EMAIL_TEMPLATES.find((t) => t.id === templateId);
      if (template) {
        setSender(template.sender);
        setSubject(template.subject);
        setBody(template.body);
      }
    },
    []
  );

  const categories = [...new Set(EMAIL_TEMPLATES.map((t) => t.category))];

  return (
    <form onSubmit={handleSubmit} className="space-y-4 max-w-2xl">
      {showSuccess && (
        <div className="text-emerald-400 text-sm font-medium" role="status">
          Email submitted successfully!
        </div>
      )}

      {error && (
        <div className="text-red-400 text-sm font-medium" role="alert">
          {error.message}
        </div>
      )}

      <div>
        <label htmlFor="template" className="block text-sm font-medium text-zinc-300 mb-1">
          Template
        </label>
        <select
          id="template"
          onChange={handleTemplateChange}
          defaultValue=""
          className="w-full rounded-md bg-zinc-800 border border-zinc-700 text-zinc-100 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          <option value="">Select a template...</option>
          {categories.map((category) => (
            <optgroup key={category} label={category}>
              {EMAIL_TEMPLATES.filter((t) => t.category === category).map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
                </option>
              ))}
            </optgroup>
          ))}
        </select>
      </div>

      <div>
        <label htmlFor="sender" className="block text-sm font-medium text-zinc-300 mb-1">
          Sender
        </label>
        <input
          id="sender"
          type="text"
          value={sender}
          onChange={(e) => setSender(e.target.value)}
          className="w-full rounded-md bg-zinc-800 border border-zinc-700 text-zinc-100 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
          placeholder="sender@example.com"
        />
      </div>

      <div>
        <label htmlFor="subject" className="block text-sm font-medium text-zinc-300 mb-1">
          Subject
        </label>
        <input
          id="subject"
          type="text"
          value={subject}
          onChange={(e) => setSubject(e.target.value)}
          className="w-full rounded-md bg-zinc-800 border border-zinc-700 text-zinc-100 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
          placeholder="Issue subject"
        />
        {errors.subject && (
          <p className="mt-1 text-sm text-red-400">{errors.subject}</p>
        )}
      </div>

      <div>
        <label htmlFor="body" className="block text-sm font-medium text-zinc-300 mb-1">
          Body
        </label>
        <textarea
          id="body"
          value={body}
          onChange={(e) => setBody(e.target.value)}
          rows={6}
          className="w-full rounded-md bg-zinc-800 border border-zinc-700 text-zinc-100 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500 resize-y"
          placeholder="Describe the issue..."
        />
        {errors.body && (
          <p className="mt-1 text-sm text-red-400">{errors.body}</p>
        )}
      </div>

      <button
        type="submit"
        disabled={isSubmitting}
        className="px-4 py-2 rounded-md bg-blue-600 text-white font-medium hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        {isSubmitting ? 'Submitting...' : 'Submit Email'}
      </button>
    </form>
  );
}
