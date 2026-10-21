import { z } from 'zod';
import { date, id, reason, rowVersion, timeline, timestamp } from './common.ts';

export const siteAssignment = z.object({
  id,
  siteId: id,
  shiftId: id,
  effectiveFrom: date,
  effectiveTo: date.nullable(),
  holidayCalendarId: id.nullable(),
  legacyApprovalLinked: z.boolean(),
});
export const temporaryChange = z.object({
  id,
  siteId: id,
  businessDate: date,
  originalShiftVersionId: id,
  replacementShiftVersionId: id,
  recordedBy: z.string(),
  recordedAt: timestamp,
  affectedEmployeeIds: z.array(id),
  legacyApprovalLinked: z.boolean(),
  status: z.enum(['active', 'cancelled', 'superseded']),
  amendmentId: id.nullable(),
  supersededByChangeId: id.nullable(),
});
export const amendment = z.object({
  id,
  originalChangeId: id,
  replacementChangeId: id.nullable(),
  recordedBy: z.string(),
  recordedAt: timestamp,
});
export const siteRosterHistory = z.object({
  siteId: id,
  timeline,
  assignments: z.array(siteAssignment),
  temporaryChanges: z.array(temporaryChange),
  temporaryAmendments: z.array(amendment),
});
export const rosterWrite = z.object({
  timeline,
  assignment: siteAssignment,
  ended: siteAssignment.nullable(),
});
export const rosterInput = z
  .strictObject({
    shiftId: id,
    effectiveFrom: date,
    effectiveTo: date.nullable(),
    holidayCalendarId: id.nullable(),
    expectedTimeline: timeline,
    expectedShiftVersion: rowVersion,
    reason: reason(),
  })
  .refine((value) => !value.effectiveTo || value.effectiveTo > value.effectiveFrom, {
    path: ['effectiveTo'],
    message: 'End date must follow the start date (exclusive).',
  });
export const temporaryPreview = z.object({
  siteId: id,
  businessDate: date,
  timeline,
  staffToken: timeline,
  originalShiftVersionId: id,
  replacementShiftVersionId: id,
  affectedEmployeeIds: z.array(id),
});
export const amendmentPreview = z.object({
  siteId: id,
  changeId: id,
  businessDate: date,
  timeline,
  staffToken: timeline,
  baseShiftVersionId: id,
  replacementShiftVersionId: id.nullable(),
  previousAffectedEmployeeIds: z.array(id),
  affectedEmployeeIds: z.array(id),
});
export const temporaryWrite = z.object({ timeline, change: temporaryChange });
export const amendmentWrite = z.object({
  timeline,
  amendment,
  original: temporaryChange,
  replacement: temporaryChange.nullable(),
  affectedEmployeeIds: z.array(id),
});
export type SiteRosterHistory = z.infer<typeof siteRosterHistory>;
