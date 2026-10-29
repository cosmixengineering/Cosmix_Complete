import { requireDate, status, text } from './serviceValues';

export function payslipPeriod(start: unknown, endExclusive: unknown): string {
  try {
    const first = Date.parse(requireDate(text(start)) + 'T00:00:00Z');
    const end = Date.parse(requireDate(text(endExclusive)) + 'T00:00:00Z');
    if (end <= first) return 'Unavailable';
    const formatter = new Intl.DateTimeFormat('en-GB', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
      timeZone: 'UTC',
    });
    return `${formatter.format(first)} – ${formatter.format(end - 86400000)}`;
  } catch {
    return 'Unavailable';
  }
}

export function payslipStatus(value: unknown): string {
  return value === 'ManagementApproved' ? 'Management approved' : status(value);
}
