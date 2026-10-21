import { z } from 'zod';
import { id, date, time, version } from './contracts';
export const masterSchema = z.object({
  id,
  code: z.string(),
  name: z.string(),
  isActive: z.boolean(),
  version,
  cityId: id.nullable().optional(),
  cityName: z.string().nullable().optional(),
  province: z.string().nullable().optional(),
  address: z.string().nullable().optional(),
  clientName: z.string().nullable().optional(),
  projectId: id.nullable().optional(),
  startsOn: date.nullable().optional(),
  endsOn: date.nullable().optional(),
  supervisorEmployeeId: id.nullable().optional(),
});
export type Master = z.infer<typeof masterSchema>;
export const calendarSummary = z.object({
  id,
  code: z.string(),
  name: z.string(),
  effectiveFrom: date,
  effectiveTo: date.nullable(),
  revision: z.number(),
  isActive: z.boolean(),
  supersedesCalendarId: id.nullable(),
  version,
});
export const holidaySchema = z.object({
  id,
  date,
  name: z.string(),
  notificationReference: z.string(),
});
export const calendarSchema = z.object({
  calendar: calendarSummary,
  governmentNotificationReference: z.string(),
  holidays: z.array(holidaySchema),
});
export type Calendar = z.infer<typeof calendarSchema>;
export const organizationChange = z.object({
  id,
  operation: z.string(),
  reason: z.string(),
  occurredAt: time,
  actorUserId: z.string(),
  actorRoleCode: z.string(),
  beforeJson: z.string().nullable(),
  afterJson: z.string(),
});
