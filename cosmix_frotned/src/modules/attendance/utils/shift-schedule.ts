import { shiftScheduleSchema, type ShiftSchedule } from '../schemas/shifts';

export const weekdays = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
].map((label, index) => ({ label, value: 1 << index }));
export function weekdayLabel(mask: number) {
  return weekdays
    .filter((day) => (mask & day.value) !== 0)
    .map((day) => day.label.slice(0, 3))
    .join(', ');
}
export function nextDate(date: string) {
  const value = new Date(date + 'T00:00:00Z');
  value.setUTCDate(value.getUTCDate() + 1);
  return value.toISOString().slice(0, 10);
}
export function validateSchedule(schedule: unknown, today: string, latest?: string) {
  const parsed = shiftScheduleSchema.safeParse(schedule);
  if (!parsed.success) return parsed.error.issues[0]?.message ?? 'Check the schedule.';
  if (parsed.data.effectiveFrom < today) return 'The initial schedule cannot start in the past.';
  if (latest !== undefined && parsed.data.effectiveFrom <= (latest > today ? latest : today))
    return 'A new version must follow today and every existing version.';
  return null;
}
export function emptySchedule(effectiveFrom: string): ShiftSchedule {
  return {
    effectiveFrom,
    startTime: '',
    endTime: '',
    endDayOffset: 0,
    scheduledMinutes: 0,
    paidBreakMinutes: 0,
    unpaidBreakMinutes: 0,
    applicableWeekdays: 0,
  };
}
