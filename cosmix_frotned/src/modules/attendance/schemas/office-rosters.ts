import { z } from 'zod';
import { date, id, timeline, timestamp } from './common.ts';

export const officeAssignment = z.object({
  id,
  branchId: id,
  shiftId: id,
  effectiveFrom: date,
  effectiveTo: date.nullable(),
  holidayCalendarId: id.nullable(),
  recordedBy: z.string(),
  recordedAtUtc: timestamp,
});
export const officeEnding = z.object({
  id,
  officeRosterAssignmentId: id,
  effectiveTo: date,
  reason: z.string(),
  recordedBy: z.string(),
  recordedAtUtc: timestamp,
});
export const officeRosterHistory = z.object({
  branchId: id,
  timeline,
  assignments: z.array(officeAssignment),
  endings: z.array(officeEnding),
});
export const officeRosterWrite = z.object({
  timeline,
  assignment: officeAssignment,
  ended: officeAssignment.nullable(),
});
export type OfficeRosterHistory = z.infer<typeof officeRosterHistory>;
