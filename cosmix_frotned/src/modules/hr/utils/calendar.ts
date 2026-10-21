import { z } from 'zod';

const day = 86_400_000;
const iso = (date: Date) =>
  `${String(date.getUTCFullYear()).padStart(4, '0')}-${String(date.getUTCMonth() + 1).padStart(2, '0')}-${String(date.getUTCDate()).padStart(2, '0')}`;

function parseDate(value: string) {
  if (!z.iso.date().safeParse(value).success) throw new Error('Invalid calendar date');
  return new Date(value + 'T12:00:00Z');
}

function monthStart(value: string) {
  if (!/^\d{4}-(0[1-9]|1[0-2])$/.test(value)) throw new Error('Invalid calendar month');
  return parseDate(value + '-01');
}

export function businessToday(now = new Date()) {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Karachi',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(now);
  return ['year', 'month', 'day']
    .map((type) => parts.find((part) => part.type === type)!.value)
    .join('-');
}

export function shiftMonth(value: string, offset: number) {
  const date = monthStart(value);
  if (!Number.isInteger(offset)) throw new Error('Invalid month offset');
  date.setUTCMonth(date.getUTCMonth() + offset);
  if (date.getUTCFullYear() < 1 || date.getUTCFullYear() > 9999)
    throw new Error('Calendar range exceeded');
  return iso(date).slice(0, 7);
}

export function monthDays(value: string) {
  const start = monthStart(value);
  const next = new Date(start);
  next.setUTCMonth(next.getUTCMonth() + 1);
  const count = Math.round((next.getTime() - start.getTime()) / day);
  return Array.from({ length: count }, (_, index) => iso(new Date(start.getTime() + index * day)));
}

export function monthGrid(value: string) {
  const start = monthStart(value);
  start.setUTCDate(start.getUTCDate() - ((start.getUTCDay() + 6) % 7));
  return Array.from({ length: 42 }, (_, index) => iso(new Date(start.getTime() + index * day)));
}

export function weekDays(value: string) {
  const start = parseDate(value);
  start.setUTCDate(start.getUTCDate() - ((start.getUTCDay() + 6) % 7));
  return Array.from({ length: 7 }, (_, index) => iso(new Date(start.getTime() + index * day)));
}

export function dateLabel(
  value: string,
  options: Intl.DateTimeFormatOptions = { dateStyle: 'long' },
) {
  return new Intl.DateTimeFormat('en-GB', { ...options, timeZone: 'UTC' }).format(parseDate(value));
}
