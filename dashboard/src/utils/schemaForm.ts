import type { JsonSchema } from '../types';

export type FieldWidget = 'text' | 'textarea' | 'number' | 'checkbox' | 'select';

/** A form field derived from a property of the workflow input schema. */
export interface FormField {
  name: string;
  label: string;
  description: string | null;
  widget: FieldWidget;
  required: boolean;
  options: string[];
  /** The value must contain a non-whitespace character (required, minLength or pattern \S). */
  nonBlank: boolean;
}

export type FormValues = Record<string, string | boolean>;

function primaryType(schema: JsonSchema): string | undefined {
  return Array.isArray(schema.type) ? schema.type.find((t) => t !== 'null') : schema.type;
}

function humanize(name: string): string {
  const spaced = name.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/[_-]+/g, ' ');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

/** Builds the form fields of an input schema (object properties, in declaration order). */
export function buildFields(schema: JsonSchema | null | undefined): FormField[] {
  const properties = schema?.properties ?? {};
  const required = new Set(schema?.required ?? []);

  return Object.entries(properties).map(([name, property]) => {
    const type = primaryType(property);
    const widget: FieldWidget = property.enum
      ? 'select'
      : type === 'boolean'
        ? 'checkbox'
        : type === 'number' || type === 'integer'
          ? 'number'
          : property['x-multiline']
            ? 'textarea'
            : 'text';
    const isRequired = required.has(name);
    return {
      name,
      label: property.title ?? humanize(name),
      description: property.description ?? null,
      widget,
      required: isRequired,
      options: (property.enum ?? []).map(String),
      nonBlank: isRequired && widget !== 'checkbox' && ((property.minLength ?? 0) > 0 || property.pattern === '\\S' || type === 'string'),
    };
  });
}

/** Empty values for the fields (schema defaults when declared). */
export function emptyValues(fields: FormField[], schema?: JsonSchema | null): FormValues {
  const values: FormValues = {};
  for (const field of fields) {
    const fallback = schema?.properties?.[field.name]?.default;
    values[field.name] = field.widget === 'checkbox'
      ? fallback === true
      : fallback === undefined || fallback === null ? '' : String(fallback);
  }
  return values;
}

/** Form values from a sample input. */
export function valuesFromInput(fields: FormField[], input: Record<string, unknown> | null): FormValues {
  const values: FormValues = {};
  for (const field of fields) {
    const value = input?.[field.name];
    values[field.name] = field.widget === 'checkbox' ? value === true : value === undefined || value === null ? '' : String(value);
  }
  return values;
}

/** Client-side validation: required fields must not be blank; numbers must be numeric. */
export function validateValues(fields: FormField[], values: FormValues): Record<string, string> {
  const errors: Record<string, string> = {};
  for (const field of fields) {
    const value = values[field.name];
    if (field.widget === 'checkbox') continue;
    const text = typeof value === 'string' ? value : '';
    if (field.nonBlank && text.trim() === '') {
      errors[field.name] = `${field.label} is required`;
    } else if (field.widget === 'number' && text.trim() !== '' && Number.isNaN(Number(text))) {
      errors[field.name] = `${field.label} must be a number`;
    }
  }
  return errors;
}

/** The JSON input sent to the engine (numbers converted, empty optional fields omitted). */
export function toInput(fields: FormField[], values: FormValues): Record<string, unknown> {
  const input: Record<string, unknown> = {};
  for (const field of fields) {
    const value = values[field.name];
    if (field.widget === 'checkbox') {
      input[field.name] = value === true;
    } else if (typeof value === 'string') {
      if (value === '' && !field.required) continue;
      input[field.name] = field.widget === 'number' && value.trim() !== '' ? Number(value) : value;
    }
  }
  return input;
}
