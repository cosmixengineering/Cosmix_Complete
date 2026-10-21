import { date } from '../../schemas/common';

export function today() {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Karachi',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date());
}
export function monthRange(month: string) {
  if (!/^\d{4}-\d{2}$/.test(month)) return null;
  const from = `${month}-01`;
  if (!date.safeParse(from).success) return null;
  const end = new Date(`${from}T00:00:00Z`);
  end.setUTCMonth(end.getUTCMonth() + 1);
  end.setUTCDate(0);
  return { from, to: end.toISOString().slice(0, 10) };
}
export function validRange(from: string, to: string, inclusive = false) {
  if (!date.safeParse(from).success || !date.safeParse(to).success) return false;
  const days = (Date.parse(to) - Date.parse(from)) / 86400000;
  return inclusive ? days >= 0 && days <= 30 : days > 0 && days <= 366;
}
export function nextDate(value: string) {
  return new Date(Date.parse(value) + 86400000).toISOString().slice(0, 10);
}
export function formatTime(value: string | null | undefined, zone?: string) {
  if (!value) return 'Unavailable';
  if (!zone) return value;
  try {
    return new Intl.DateTimeFormat('en-GB', {
      timeZone: zone,
      dateStyle: 'medium',
      timeStyle: 'long',
    }).format(new Date(value));
  } catch {
    return `${value} (timezone unavailable: ${zone})`;
  }
}
export function overtimeReady(source: {
  staffCategory: string;
  isLocked: boolean;
  eligibility: { status: string; allowed: boolean | null };
  durationPolicy: { status: string; scheduledDutyCompleted: boolean | null };
}) {
  return (
    source.staffCategory === 'SITE' &&
    !source.isLocked &&
    source.eligibility.status === 'configured' &&
    source.eligibility.allowed === true &&
    source.durationPolicy.status === 'evaluated' &&
    source.durationPolicy.scheduledDutyCompleted === true
  );
}
