import { AppError } from './errors';
import { isRecord, isTimestamp } from './validation';
import type { DataRecord } from '../types/selfService';

export function record(value: unknown): DataRecord {
  if (!isRecord(value)) {
    throw new AppError('invalid_server_response');
  }
  return value;
}
export function records(value: unknown, maximum = 1000): DataRecord[] {
  if (
    !Array.isArray(value) ||
    value.length > maximum ||
    !value.every(isRecord)
  ) {
    throw new AppError('invalid_server_response');
  }
  return value;
}
export function text(value: unknown, fallback = 'Unavailable'): string {
  return typeof value === 'string' && value.length <= 8000 && value.length > 0
    ? value
    : fallback;
}
export function numeric(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}
export function amount(value: unknown, currency: unknown): string {
  const number = numeric(value);
  const unit =
    typeof currency === 'string' && /^[A-Z]{3}$/.test(currency) ? currency : '';
  if (number === null || !unit) {
    return 'Unavailable';
  }
  return new Intl.NumberFormat('en-PK', {
    style: 'currency',
    currency: unit,
    maximumFractionDigits: 2,
  }).format(number);
}
export function status(value: unknown): string {
  const names = ['Pending', 'Approved', 'Returned', 'Rejected', 'Cancelled'];
  if (
    typeof value === 'number' &&
    Number.isInteger(value) &&
    value >= 0 &&
    value < names.length
  ) {
    return names[value];
  }
  return text(value)
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/_/g, ' ');
}
export function isPending(value: unknown): boolean {
  return value === 0 || value === 'Pending';
}
export function requireDate(value: string): string {
  if (
    !/^\d{4}-\d{2}-\d{2}$/.test(value) ||
    !isTimestamp(value + 'T00:00:00Z') ||
    new Date(value + 'T00:00:00Z').toISOString().slice(0, 10) !== value
  ) {
    throw new AppError('invalid_date');
  }
  return value;
}
export function companyInstant(date: string, time: string): string {
  requireDate(date);
  if (!/^([01]\d|2[0-3]):[0-5]\d$/.test(time)) {
    throw new AppError('invalid_time');
  }
  // Existing backend business time is Asia/Karachi (UTC+05:00, no DST).
  return new Date(`${date}T${time}:00+05:00`).toISOString();
}
export function businessToday(): string {
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Asia/Karachi',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(new Date());
  const part = (name: string) =>
    parts.find(item => item.type === name)?.value ?? '';
  return `${part('year')}-${part('month')}-${part('day')}`;
}
export function dateRange(from: string, to: string): string[] {
  const start = Date.parse(requireDate(from) + 'T00:00:00Z');
  const end = Date.parse(requireDate(to) + 'T00:00:00Z');
  const count = Math.round((end - start) / 86400000) + 1;
  if (count < 1 || count > 366) {
    throw new AppError('invalid_date_range');
  }
  return Array.from({ length: count }, (_, index) =>
    new Date(start + index * 86400000).toISOString().slice(0, 10),
  );
}
export function reason(value: string, maximum = 1000): string {
  const result = value.trim().replace(/\s+/g, ' ');
  if (
    !result ||
    result.length > maximum ||
    Array.from(result).some(
      character =>
        character.charCodeAt(0) < 32 || character.charCodeAt(0) === 127,
    )
  ) {
    throw new AppError('invalid_request_reason');
  }
  return result;
}
export function moneyInput(
  value: string,
  minimum = 1,
  maximum = 10000000,
): number {
  if (!/^\d{1,8}(?:\.\d{1,2})?$/.test(value.trim())) {
    throw new AppError('invalid_amount');
  }
  const result = Number(value);
  if (result < minimum || result > maximum) {
    throw new AppError('invalid_amount');
  }
  return result;
}
export function fraction(value: unknown): string {
  if (
    !isRecord(value) ||
    typeof value.numerator !== 'string' ||
    typeof value.denominator !== 'string' ||
    !/^-?\d+$/.test(value.numerator) ||
    !/^\d+$/.test(value.denominator) ||
    /^0+$/.test(value.denominator)
  ) {
    return 'Unavailable';
  }
  return value.denominator === '1'
    ? `${value.numerator} days`
    : `${value.numerator}/${value.denominator} days`;
}
