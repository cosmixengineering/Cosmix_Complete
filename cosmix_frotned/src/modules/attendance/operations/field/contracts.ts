import { z } from 'zod';
import { date, id, pageOf, reason, rowVersion, timestamp } from '../../schemas/common';

const integer = z.number().int().nonnegative();
const number = z.number().finite();
export const boundary = z.object({
  kind: z.union([z.literal(1), z.literal(2)]),
  latitude: number.nullable(),
  longitude: number.nullable(),
  radiusMeters: number.nullable(),
  polygonGeoJson: z.string().nullable(),
});
export const zoneVersion = z.object({
  id,
  version: integer,
  type: z.string(),
  boundary,
  effectiveFromUtc: timestamp,
  effectiveToUtc: timestamp,
  employeeInstruction: z.string(),
  reasonCode: z.string(),
});
export const zone = z.object({
  id,
  siteId: id,
  code: z.string(),
  name: z.string(),
  isActive: z.boolean(),
  versions: z.array(zoneVersion),
});
export const zonesPage = z.object({
  items: z.array(zone),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive().max(100),
  hasMore: z.boolean(),
});
export const configuration = z.object({
  id,
  version: integer,
  effectiveFromUtc: timestamp,
  effectiveToUtc: timestamp,
  maximumAccuracyMeters: number,
  warningDistanceMeters: number,
  minimumIntervalSeconds: integer,
  retentionDays: integer,
  maximumMonitoringSeconds: integer,
  liveVisibilitySeconds: integer,
  visibleNotice: z.string(),
});
export const assignment = z.object({
  id,
  zoneVersionId: id,
  populationKind: z.number().int().min(1).max(4),
  targetId: id,
  effectiveFromUtc: timestamp,
  effectiveToUtc: timestamp,
  isEmployeeOverride: z.boolean(),
  endId: id.nullable(),
});
export const myZones = z.object({
  monitoringAvailable: z.boolean(),
  state: z.string(),
  siteId: id.nullable(),
  sessionKey: id.nullable(),
  configuration: configuration.nullable(),
  zones: z.array(
    z.object({ assignmentId: id, zoneId: id, name: z.string(), version: zoneVersion }),
  ),
});
export const dutyWindow = z.object({
  id,
  assignmentId: id,
  configurationVersionId: id,
  startsAtUtc: timestamp,
  endsAtUtc: timestamp,
  checkpointVersion: z.string(),
});
export const zoneEvent = z.object({
  id,
  employeeId: id,
  zoneVersionId: id,
  windowId: id,
  kind: z.number().int().min(1).max(5),
  source: z.number().int().min(1).max(3),
  receivedAtUtc: timestamp,
  locationExpiresAtUtc: timestamp.nullable(),
  instruction: z.string().nullable(),
  lastResponseId: id.nullable(),
  lastResponseKind: z.number().int().min(1).max(3).nullable(),
});
export const eventsPage = z.object({
  items: z.array(zoneEvent),
  nextCursor: z.string().nullable(),
});
export const eventResponseInput = z.strictObject({
  kind: z.number().int().min(1).max(3),
  reason: reason(),
  expectedLastResponseId: id.nullable(),
});

const effective = { effectiveFromUtc: timestamp, effectiveToUtc: timestamp };
const versionInputFields = {
  type: z.enum(['OPERATIONAL', 'RED']),
  boundary,
  ...effective,
  employeeInstruction: reason(2000),
  reasonCode: reason(100),
  reason: reason(),
};
export const createZoneInput = z.strictObject({
  code: reason(50),
  name: reason(150),
  ...versionInputFields,
});
export const publishZoneInput = z.strictObject({
  expectedLatestVersion: integer,
  ...versionInputFields,
});
export const configureInput = z
  .strictObject({
    expectedLatestVersion: integer,
    ...effective,
    maximumAccuracyMeters: number.positive().max(100000),
    warningDistanceMeters: number.nonnegative().max(100000),
    minimumIntervalSeconds: z.number().int().min(1).max(86400),
    retentionDays: z.number().int().min(1).max(3650),
    maximumMonitoringSeconds: z.number().int().min(1).max(172800),
    liveVisibilitySeconds: z.number().int().min(1).max(86400),
    visibleNotice: reason(2000),
    reason: reason(),
  })
  .refine(
    (v) =>
      v.minimumIntervalSeconds <= v.liveVisibilitySeconds &&
      v.liveVisibilitySeconds <= v.maximumMonitoringSeconds,
    'Visibility must fit the interval and duty duration.',
  );
export const assignInput = z.strictObject({
  zoneVersionId: id,
  populationKind: z.number().int().min(1).max(4),
  targetId: id,
  ...effective,
  isEmployeeOverride: z.literal(false),
  reason: reason(),
  replaceAssignmentId: id.nullable(),
});
export const endAssignmentInput = z.strictObject({ effectiveToUtc: timestamp, reason: reason() });

export const reportSummary = z.object({
  id,
  employeeId: id,
  siteId: id,
  kind: z.number().int().min(1).max(5),
  taskReference: z.string().nullable(),
  receivedAtUtc: timestamp,
  deviceCapturedAtUtc: timestamp.nullable(),
  isOffline: z.boolean(),
  submissionStatus: z.string(),
  evidenceStatus: z.string(),
  hasAttachment: z.boolean(),
  reviewSequence: integer,
  responseSequence: integer,
});
export const reportDetail = z.object({
  report: reportSummary,
  employeeAssignmentId: id,
  registeredDeviceId: id,
  notes: z.string(),
  latitude: number.nullable(),
  longitude: number.nullable(),
  accuracyMeters: number.nullable(),
  reviewWorkflowStatus: z.string(),
  reviews: z.array(
    z.object({
      sequence: integer,
      outcome: z.number().int().min(1).max(4),
      reviewer: z.string(),
      reviewerRole: z.string(),
      reason: z.string(),
      reviewedAtUtc: timestamp,
      id: id.nullable(),
    }),
  ),
  responses: z
    .array(
      z.object({
        id,
        sequence: integer,
        returnedReviewId: id,
        submittedAtUtc: timestamp,
        writtenResponse: z.string(),
        hasAttachment: z.boolean(),
      }),
    )
    .nullable(),
});
export const reportsPage = pageOf(reportSummary);
export const reportFilter = z.object({ from: date, to: date }).refine((v) => {
  const days = (Date.parse(v.to) - Date.parse(v.from)) / 86400000;
  return days >= 0 && days <= 365;
}, 'Choose an inclusive date range of at most 366 days.');
export const decisionInput = z.strictObject({
  expectedReviewSequence: integer.max(256),
  expectedResponseSequence: integer.max(256),
  outcome: z.number().int().min(1).max(3),
  reason: reason(),
});
export const attachmentInput = z.strictObject({
  fileName: z.string().min(1).max(180),
  contentType: z.enum(['image/png', 'image/jpeg', 'application/pdf']),
  content: z.string().min(1),
});
export const responseInput = z.strictObject({
  expectedReviewSequence: z.number().int().min(1).max(256),
  expectedResponseSequence: integer.max(255),
  returnedReviewId: id,
  writtenResponse: z
    .string()
    .trim()
    .min(1)
    .max(4000)
    .refine(
      (v) =>
        ![...v].some(
          (c) =>
            (c.charCodeAt(0) < 32 || (c.charCodeAt(0) >= 127 && c.charCodeAt(0) <= 159)) &&
            !['\r', '\n', '\t'].includes(c),
        ),
      'Unsupported control character.',
    ),
  attachment: attachmentInput.nullable(),
});
export const notification = z.object({
  id,
  templateCode: z.string(),
  title: z.string(),
  body: z.string(),
  createdAt: timestamp,
  readAt: timestamp.nullable(),
  version: rowVersion,
});
export const notificationsPage = pageOf(notification);
export const notificationRead = z.object({ notification, changed: z.boolean() });
export const notificationReadInput = z.strictObject({ expectedVersion: rowVersion });
export type Zone = z.infer<typeof zone>;
export type ZoneVersion = z.infer<typeof zoneVersion>;
export type Configuration = z.infer<typeof configuration>;
export type Assignment = z.infer<typeof assignment>;
export type ZoneEvent = z.infer<typeof zoneEvent>;
export type ReportDetail = z.infer<typeof reportDetail>;
export type Attachment = z.infer<typeof attachmentInput>;

export function effectiveWindow(from: string, to: string) {
  timestamp.parse(from);
  timestamp.parse(to);
  if (Date.parse(to) <= Date.parse(from)) throw new Error('The end must follow the start.');
  return { effectiveFromUtc: from, effectiveToUtc: to };
}
export function reportReviewAllowed(detail: ReportDetail, employeeId: string | undefined) {
  return Boolean(
    employeeId &&
    detail.report.employeeId !== employeeId &&
    detail.report.submissionStatus === 'received_pending_review' &&
    detail.reviewWorkflowStatus === 'hr_review_pending' &&
    reportHeadAvailable(detail),
  );
}
export function reportHeadAvailable(detail: ReportDetail) {
  return (
    detail.reviews.length === detail.report.reviewSequence &&
    (detail.responses ?? []).length === detail.report.responseSequence &&
    detail.reviews.every((r, i) => r.sequence === i + 1 && Boolean(r.id)) &&
    (detail.responses ?? []).every((r, i) => r.sequence === i + 1)
  );
}
export function returnedReview(detail: ReportDetail) {
  const last = detail.reviews.at(-1);
  return reportHeadAvailable(detail) &&
    detail.report.submissionStatus === 'returned' &&
    detail.reviewWorkflowStatus === 'employee_response_required' &&
    last?.outcome === 2 &&
    last.id
    ? last
    : undefined;
}
