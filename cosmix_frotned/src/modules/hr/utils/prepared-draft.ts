import { z } from 'zod';
import { polygonSchema } from './boundary';
import type {
  PreparedDraft,
  PreparedField,
  PreparedRow,
  PreparedTab,
  PreparedValue,
} from '../types/prepared-workspace';

export const isPreparedFieldVisible = (field: PreparedField, values: PreparedRow) =>
  !field.when || field.when.values.includes(String(values[field.when.field] ?? ''));

export const isOwnEmployeeField = (field: PreparedField) =>
  field.kind === 'reference' &&
  field.reference === 'employees' &&
  ['employee', 'employeeId', 'targetEmployeeId'].includes(field.id);

export function updatePreparedRow(
  fields: readonly PreparedField[],
  values: PreparedRow,
  name: string,
  value: PreparedValue,
) {
  const next = { ...values, [name]: value };
  for (const field of fields) if (!isPreparedFieldVisible(field, next)) delete next[field.id];
  return next;
}

export function validatePreparedDraft(tab: PreparedTab, draft: PreparedDraft) {
  const errors: Record<string, string> = {};
  for (const section of tab.sections) {
    for (const row of draft[section.id] ?? []) {
      for (const field of section.fields) {
        if (!isPreparedFieldVisible(field, row.values)) continue;
        const value = row.values[field.id];
        const path = `${section.id}.${row.key}.${field.id}`;
        const text = typeof value === 'string' ? value.trim() : '';
        if (
          field.required &&
          (value === null ||
            value === undefined ||
            value === '' ||
            value === false ||
            (typeof value === 'string' && !text))
        ) {
          errors[path] = `${field.label} is required.`;
          continue;
        }
        if (value === null || value === undefined || value === '') continue;
        if (field.maxLength && text.length > field.maxLength)
          errors[path] = `Use at most ${field.maxLength} characters.`;
        if (
          (field.kind === 'uuid' || field.kind === 'reference') &&
          !z.uuid().safeParse(text).success
        )
          errors[path] = 'Select a valid reference.';
        if (field.kind === 'number') {
          const number = Number(text);
          if (
            !text ||
            !Number.isFinite(number) ||
            (field.min !== undefined && number < field.min) ||
            (field.min !== undefined && field.minExclusive && number === field.min) ||
            (field.max !== undefined && number > field.max)
          )
            errors[path] = 'Enter a value within the permitted range.';
        }
        if (field.kind === 'date' && !z.iso.date().safeParse(text).success)
          errors[path] = 'Enter a valid date.';
        if (field.kind === 'month' && !/^\d{4}-(0[1-9]|1[0-2])$/.test(text))
          errors[path] = 'Enter a valid month.';
        if (field.kind === 'time' && !/^([01]\d|2[0-3]):[0-5]\d$/.test(text))
          errors[path] = 'Enter a valid time.';
        if (
          field.kind === 'datetime-local' &&
          !z.iso.datetime({ local: true }).safeParse(text.length === 16 ? text + ':00' : text)
            .success
        )
          errors[path] = 'Enter a valid local date and time.';
        if (field.kind === 'select' && !field.options?.some((o) => o.value === text))
          errors[path] = 'Select a supported option.';
        if (field.kind === 'file') {
          if (
            !(typeof File !== 'undefined' && value instanceof File) ||
            value.size < 1 ||
            value.size > 10 * 1024 * 1024 ||
            !['application/pdf', 'image/png', 'image/jpeg'].includes(value.type) ||
            !/\.(pdf|png|jpe?g)$/i.test(value.name)
          )
            errors[path] = 'Choose a PDF, PNG or JPEG up to 10 MiB.';
        }
        if (field.kind === 'geojson') {
          try {
            polygonSchema.parse(JSON.parse(text));
          } catch {
            errors[path] = 'Enter valid Polygon GeoJSON using longitude and latitude pairs.';
          }
        }
      }
      for (const rule of tab.dateOrders ?? []) {
        if (rule.section !== section.id) continue;
        const start = row.values[rule.start];
        const end = row.values[rule.end];
        if (typeof start === 'string' && typeof end === 'string' && start && end && end < start)
          errors[`${section.id}.${row.key}.${rule.end}`] = 'End must not precede start.';
      }
      for (const rule of tab.timeOrders ?? []) {
        if (
          rule.section !== section.id ||
          (rule.nextDayField && row.values[rule.nextDayField] === true)
        )
          continue;
        const start = row.values[rule.start];
        const end = row.values[rule.end];
        if (typeof start === 'string' && typeof end === 'string' && start && end && end <= start)
          errors[`${section.id}.${row.key}.${rule.end}`] = 'Same-day end must be after start.';
      }
    }
  }
  return errors;
}

export function preparedReviewValues(tab: PreparedTab, draft: PreparedDraft) {
  return Object.fromEntries(
    tab.sections.map((section) => [
      section.title,
      (draft[section.id] ?? []).map((row) =>
        Object.fromEntries(
          section.fields
            .filter((field) => isPreparedFieldVisible(field, row.values))
            .map((field) => {
              const value = row.values[field.id];
              return [
                field.label,
                field.kind === 'reference' &&
                field.reference === 'employees' &&
                typeof value === 'string'
                  ? { employeeId: value }
                  : typeof File !== 'undefined' && value instanceof File
                    ? value.name
                    : (value ?? null),
              ];
            }),
        ),
      ),
    ]),
  );
}
