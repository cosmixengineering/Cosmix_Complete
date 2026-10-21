import { z } from 'zod';
import { id, date } from './contracts';

export const assignedLocationSchema = z
  .object({
    siteId: id.nullable(),
    siteName: z.string().nullable(),
    siteCode: z.string().nullable(),
    address: z.string().nullable(),
    latitude: z.number().nullable(),
    longitude: z.number().nullable(),
    radiusMeters: z.number().nullable(),
    branchId: id.nullable(),
    branchName: z.string().nullable(),
  })
  .nullable();

export const punchAuditItemSchema = z.object({
  punch: z.object({
    id: id,
    action: z.string(),
    receivedAtUtc: z.string(),
    businessDate: date,
    outcome: z.string(),
    reasonCode: z.string().nullable().optional(),
    siteId: id.nullable().optional(),
    shiftVersionId: id.nullable().optional(),
    sessionKey: id.nullable().optional(),
    isOffline: z.boolean().default(false),
    assessmentId: id.nullable().optional(),
    calculationStatus: z.string().optional(),
    businessDateSource: z.string().optional(),
    branchId: id.nullable().optional(),
    officeRosterAssignmentId: id.nullable().optional(),
    officeGeofenceVersionId: id.nullable().optional(),
  }),
  assessmentRevision: z.number().nullable().optional(),
  assessedAtUtc: z.string().nullable().optional(),
  claimedDeviceCapturedAtUtc: z.string().nullable().optional(),
});

export const attendancePunchAuditPageSchema = z.object({
  items: z.array(punchAuditItemSchema),
  nextCursor: z.string().nullable().optional(),
});

export const punchLocationSchema = z.object({
  id,
  action: z.string(),
  receivedAtUtc: z.string(),
  deviceCapturedAtUtc: z.string().nullable().optional(),
  businessDate: date.nullable().optional(),
  latitude: z.number().nullable().optional(),
  longitude: z.number().nullable().optional(),
  accuracyMeters: z.number().nullable().optional(),
  isOffline: z.boolean().default(false),
  siteName: z.string().nullable().optional(),
  branchName: z.string().nullable().optional(),
  assessmentOutcome: z.string().nullable().optional(),
  distanceMeters: z.number().nullable().optional(),
  insideGeofence: z.boolean().nullable().optional(),
});

export const employeeTrackingSchema = z.object({
  employeeId: id,
  employeeName: z.string().nullable().optional(),
  employeeNumber: z.number().optional(),
  staffCategory: z.string().nullable().optional(),
  assignedLocation: assignedLocationSchema.optional(),
  latestCheckIn: punchLocationSchema.nullable().optional(),
  checkInHistory: z.array(punchLocationSchema).default([]),
});

export type PunchAuditItem = z.infer<typeof punchAuditItemSchema>;
export type AttendancePunchAuditPage = z.infer<typeof attendancePunchAuditPageSchema>;
export type EmployeeTracking = z.infer<typeof employeeTrackingSchema>;
export type PunchLocation = z.infer<typeof punchLocationSchema>;
export type AssignedLocation = z.infer<typeof assignedLocationSchema>;
