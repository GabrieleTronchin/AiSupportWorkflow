import { useCallback, useEffect, useMemo, useState } from 'react';
import type { WorkflowInfo } from '../types';
import { useRunSubmit } from '../hooks/useRunSubmit';
import { buildFields, emptyValues, toInput, validateValues, valuesFromInput, type FormValues } from '../utils/schemaForm';

interface RunSubmitFormProps {
  workflow: WorkflowInfo;
}

const inputClasses = 'w-full rounded-md bg-zinc-800 border border-zinc-700 text-zinc-100 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500';

/**
 * Submission form generated from the workflow input schema, with the workflow's samples as templates.
 * Invalid input is reported before sending (and by the engine, which validates the same schema).
 */
export function RunSubmitForm({ workflow }: RunSubmitFormProps) {
  const fields = useMemo(() => buildFields(workflow.inputSchema), [workflow.inputSchema]);
  const [values, setValues] = useState<FormValues>(() => emptyValues(fields, workflow.inputSchema));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [sampleId, setSampleId] = useState('');
  const [showSuccess, setShowSuccess] = useState(false);
  const { submit, isSubmitting, isSuccess, error, reset } = useRunSubmit();

  // A different workflow means a different form.
  useEffect(() => {
    setValues(emptyValues(fields, workflow.inputSchema));
    setErrors({});
    setSampleId('');
  }, [fields, workflow.inputSchema]);

  useEffect(() => {
    if (isSuccess) {
      setValues(emptyValues(fields, workflow.inputSchema));
      setErrors({});
      setSampleId('');
      setShowSuccess(true);
      const timer = setTimeout(() => {
        setShowSuccess(false);
        reset();
      }, 3000);
      return () => clearTimeout(timer);
    }
  }, [isSuccess, reset, fields, workflow.inputSchema]);

  const handleSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      const validationErrors = validateValues(fields, values);
      if (Object.keys(validationErrors).length > 0) {
        setErrors(validationErrors);
        return;
      }
      setErrors({});
      await submit(workflow.id, toInput(fields, values));
    },
    [fields, values, submit, workflow.id],
  );

  const handleSampleChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      const id = e.target.value;
      setSampleId(id);
      const sample = workflow.samples.find((s) => s.id === id);
      if (!sample) return;
      const next = valuesFromInput(fields, sample.input);
      setValues(next);
      // Show validation errors immediately for invalid samples
      setErrors(validateValues(fields, next));
    },
    [fields, workflow.samples],
  );

  const setValue = (name: string, value: string | boolean) => setValues((prev) => ({ ...prev, [name]: value }));
  const groups = [...new Set(workflow.samples.map((s) => s.group ?? 'Samples'))];

  return (
    <form onSubmit={handleSubmit} className="space-y-4 max-w-2xl" aria-label={`Submit to ${workflow.name}`}>
      <h2 className="text-sm font-semibold text-zinc-300 uppercase tracking-wide">New input · {workflow.name}</h2>

      {showSuccess && (
        <div className="text-emerald-400 text-sm font-medium" role="status">
          Submitted successfully!
        </div>
      )}

      {error && (
        <div className="text-red-400 text-sm font-medium" role="alert">
          {error.message}
        </div>
      )}

      {!workflow.available && (
        <div className="text-amber-300 text-sm" role="alert">
          The module of this workflow is unavailable: new inputs are rejected.
        </div>
      )}

      {workflow.samples.length > 0 && (
        <div>
          <label htmlFor="sample" className="block text-sm font-medium text-zinc-300 mb-1">
            Template
          </label>
          <select id="sample" onChange={handleSampleChange} value={sampleId} className={inputClasses}>
            <option value="">Select a template...</option>
            {groups.map((group) => (
              <optgroup key={group} label={group}>
                {workflow.samples.filter((s) => (s.group ?? 'Samples') === group).map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </optgroup>
            ))}
          </select>
        </div>
      )}

      {fields.map((field) => {
        const id = `field-${field.name}`;
        const value = values[field.name];
        return (
          <div key={field.name}>
            {field.widget === 'checkbox' ? (
              <label htmlFor={id} className="flex items-center gap-2 text-sm font-medium text-zinc-300">
                <input id={id} type="checkbox" checked={value === true} onChange={(e) => setValue(field.name, e.target.checked)} />
                {field.label}
              </label>
            ) : (
              <>
                <label htmlFor={id} className="block text-sm font-medium text-zinc-300 mb-1">
                  {field.label}
                  {field.required && <span className="text-zinc-500"> *</span>}
                </label>
                {field.widget === 'textarea' ? (
                  <textarea
                    id={id}
                    value={String(value ?? '')}
                    onChange={(e) => setValue(field.name, e.target.value)}
                    rows={6}
                    className={`${inputClasses} resize-y`}
                    aria-invalid={errors[field.name] ? true : undefined}
                  />
                ) : field.widget === 'select' ? (
                  <select id={id} value={String(value ?? '')} onChange={(e) => setValue(field.name, e.target.value)} className={inputClasses}>
                    <option value="">—</option>
                    {field.options.map((option) => (
                      <option key={option} value={option}>
                        {option}
                      </option>
                    ))}
                  </select>
                ) : (
                  <input
                    id={id}
                    type={field.widget === 'number' ? 'number' : 'text'}
                    value={String(value ?? '')}
                    onChange={(e) => setValue(field.name, e.target.value)}
                    className={inputClasses}
                    aria-invalid={errors[field.name] ? true : undefined}
                  />
                )}
              </>
            )}
            {field.description && <p className="mt-1 text-xs text-zinc-500">{field.description}</p>}
            {errors[field.name] && <p className="mt-1 text-sm text-red-400">{errors[field.name]}</p>}
          </div>
        );
      })}

      <button
        type="submit"
        disabled={isSubmitting || !workflow.available}
        className="px-4 py-2 rounded-md bg-blue-600 text-white font-medium hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        {isSubmitting ? 'Submitting...' : 'Submit'}
      </button>
    </form>
  );
}
