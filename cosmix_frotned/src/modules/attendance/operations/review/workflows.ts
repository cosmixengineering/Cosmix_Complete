import { z } from 'zod';
import {
  carryDecision,
  carryDetail,
  carryRequest,
  carrySource,
  correction,
  correctionDecision,
  correctionDetail,
  correctionSource,
  cursorPage,
  overtime,
  overtimeDetail,
  overtimeSource,
  pageOf,
} from './contracts';

export type Workflow = 'correction' | 'carry' | 'overtime';
export const workflows = {
  correction: {
    title: 'Attendance corrections',
    permission: 'attendance.corrections',
    source: correctionSource,
    response: correction,
    detail: correctionDetail,
    decision: correctionDecision,
    queue: '/attendance/corrections/review',
  },
  carry: {
    title: 'Locked-month factual corrections',
    permission: 'attendance.corrections',
    source: carrySource,
    response: carryRequest,
    detail: carryDetail,
    decision: carryDecision,
    queue: '/attendance/carry-forward/requests',
  },
  overtime: {
    title: 'Overtime',
    permission: 'attendance.overtime',
    source: overtimeSource,
    response: overtime,
    detail: overtimeDetail,
    decision: overtime,
    queue: '/attendance/overtime/review',
  },
} as const;
export function workflowPaths(workflow: Workflow, employeeId: string, self: boolean) {
  if (workflow === 'correction') {
    const base = self
      ? '/me/attendance/corrections'
      : `/attendance/employees/${employeeId}/correction-requests`;
    return { source: base + '/sources', requests: base };
  }
  const base =
    workflow === 'carry'
      ? self
        ? '/me/attendance/carry-forward'
        : `/hr/employees/${employeeId}/attendance-carry-forward`
      : self
        ? '/me/overtime'
        : `/attendance/employees/${employeeId}/overtime`;
  return { source: base + '/sources', requests: base + '/requests' };
}
export function reviewPath(workflow: Workflow, employeeId: string, requestId: string) {
  if (workflow === 'correction')
    return `/attendance/employees/${employeeId}/corrections/${requestId}`;
  if (workflow === 'carry')
    return `/hr/employees/${employeeId}/attendance-carry-forward/requests/${requestId}`;
  return `/attendance/overtime/employees/${employeeId}/requests/${requestId}`;
}
export function historySchema(workflow: Workflow, self: boolean) {
  if (workflow === 'correction') return self ? pageOf(correction) : cursorPage(correction);
  return workflow === 'carry' ? cursorPage(carryRequest) : cursorPage(overtime);
}
export const detailSchema = z.union([correctionDetail, carryDetail, overtimeDetail]);
export const queueSchema = z.union([
  cursorPage(correction),
  cursorPage(carryRequest),
  cursorPage(overtime),
]);
