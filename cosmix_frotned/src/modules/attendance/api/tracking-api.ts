import { z } from 'zod';
import { ApiError } from '@/services/api/api-error';
import { attendanceGet, queryString } from './attendance-api';
import { calendar, cursorPage, sessionAudit, type Day } from '../operations/review/contracts';
import { approvedAttendanceEmployees, needsAttendanceSetup } from '../schemas/employee-selection';
import { nextDate } from '../operations/review/helpers';
import { attendanceError } from '../utils/messages';

export const trackingEmployees = approvedAttendanceEmployees;
export type TrackingEmployee = z.infer<typeof trackingEmployees>['items'][number];
export type TrackingDay = z.infer<typeof calendar>[number];
export type TrackingRow = {
  employee: TrackingEmployee;
  days: TrackingDay[];
  error?: string;
  errorCode?: string;
};

// One bounded directory page, two HTTP reads at a time. No retries or hidden all-company scan.
export async function loadTrackingRows(
  employees: TrackingEmployee[],
  from: string,
  to: string,
  signal: AbortSignal,
): Promise<TrackingRow[]> {
  if (
    employees.length > 10 ||
    new Set(employees.map((employee) => employee.id)).size !== employees.length
  )
    throw new Error('Select at most ten distinct employees.');
  const output: TrackingRow[] = new Array(employees.length);
  let cursor = 0;
  async function worker() {
    while (cursor < employees.length) {
      signal.throwIfAborted();
      const index = cursor++;
      const employee = employees[index];
      if (!employee) throw new Error('Attendance employee selection changed while loading.');
      if (to < from) {
        output[index] = { employee, days: [] };
        continue;
      }
      try {
        const expectedCount = Math.round((Date.parse(to) - Date.parse(from)) / 86400000) + 1;
        const schema = calendar.refine(
          (rows) =>
            rows.length === expectedCount &&
            new Set(rows.map((row) => row.businessDate)).size === expectedCount &&
            rows.every(
              (row) =>
                row.employeeId === employee.id &&
                row.businessDate >= from &&
                row.businessDate <= to &&
                (row.unavailableReason ||
                  (row.roster.employeeId === employee.id &&
                    row.roster.businessDate === row.businessDate)),
            ),
          'Attendance coverage does not match the requested employee and dates.',
        );
        const days = await attendanceGet(
          `/attendance/employees/${employee.id}/calendar` + queryString({ from, to }),
          schema,
          signal,
        );
        output[index] = { employee, days };
      } catch (error) {
        signal.throwIfAborted();
        output[index] = {
          employee,
          days: [],
          error: attendanceError(error),
          errorCode: error instanceof ApiError && error.status === 409 ? error.code : undefined,
        };
      }
    }
  }
  await Promise.all(Array.from({ length: Math.min(2, employees.length) }, worker));
  return output;
}

export const statusLabels: Record<number, { code: string; label: string; tone: string }> = {
  1: { code: '?', label: 'Unverified', tone: 'neutral' },
  2: { code: 'P', label: 'Present', tone: 'present' },
  3: { code: 'A', label: 'Absent', tone: 'absent' },
  4: { code: 'HD', label: 'Half day', tone: 'neutral' },
  5: { code: 'HR', label: 'Hourly', tone: 'neutral' },
  6: { code: 'LV', label: 'Leave', tone: 'leave' },
  7: { code: 'H', label: 'Holiday', tone: 'neutral' },
  8: { code: 'OFF', label: 'Rest day', tone: 'neutral' },
  9: { code: 'INC', label: 'Incomplete', tone: 'late' },
};
export function trackingStatus(day: TrackingDay | undefined, future = false) {
  if (future) return { code: '—', label: 'Future date', tone: 'future' };
  if (!day || day.unavailableReason)
    return { code: 'N/A', label: 'Record unavailable', tone: 'neutral' };
  if (needsAttendanceSetup(day))
    return { code: 'SET', label: 'Attendance setup required', tone: 'neutral' };
  if (day.policy.status === 'calculated' && day.policy.dayStatus !== null) {
    if (day.policy.dayStatus === 2 && (day.policy.lateMinutes ?? 0) > 0)
      return { code: 'L', label: 'Present · Late', tone: 'late' };
    return (
      statusLabels[day.policy.dayStatus] ?? {
        code: '?',
        label: 'Unknown classification',
        tone: 'neutral',
      }
    );
  }
  if (day.facts.openSessions > 0)
    return { code: 'IN', label: 'Open session · Classification pending', tone: 'neutral' };
  if (day.facts.sessionIds.length > 0)
    return { code: 'REC', label: 'Recorded work · Classification pending', tone: 'neutral' };
  return { code: '—', label: 'No accepted session · Not classified as absent', tone: 'neutral' };
}
export function availableDay(day: TrackingDay | undefined): Day | undefined {
  return day && !day.unavailableReason ? day : undefined;
}
export function trackingDuration(minutes: number | null | undefined) {
  return minutes == null ? 'Unavailable' : `${Math.floor(minutes / 60)}h ${minutes % 60}m`;
}

export async function loadTrackingSessions(employeeId: string, date: string, signal: AbortSignal) {
  const result = await attendanceGet(
    `/attendance/employees/${employeeId}/sessions` +
      queryString({ from: date, to: nextDate(date), pageSize: 100 }),
    cursorPage(sessionAudit),
    signal,
  );
  // The audit API contains historical revisions and may omit unauthorized sources.
  // Only latest rows are displayed, and pagination is explicitly disclosed.
  if (result.items.some((row) => row.session.businessDate !== date))
    throw new Error('Session date mismatch.');
  return { ...result, items: result.items.filter((row) => row.isLatestRevision) };
}
