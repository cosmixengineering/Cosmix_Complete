import { z } from 'zod';
import { date, id, pageOf, reason, rowVersion, timeline, timestamp } from '../../schemas/common';

const nullableId = id.nullable();
const nullableTime = timestamp.nullable();
const integer = z.number().int();
const minutes = integer.nonnegative();
const text = z.string();
export const roster = z.object({
  employeeId: id,
  businessDate: date,
  status: text,
  timeZone: text,
  siteId: nullableId,
  assignmentId: nullableId,
  shiftId: nullableId,
  shiftVersionId: nullableId,
  shiftVersionNumber: integer.nullable(),
  startsAt: nullableTime,
  endsAt: nullableTime,
  configuredScheduledMinutes: minutes.nullable(),
  paidBreakMinutes: minutes.nullable(),
  unpaidBreakMinutes: minutes.nullable(),
  branchId: id.optional(),
  missingCheckoutReminderAfterMinutes: minutes.optional(),
  explanation: z
    .object({
      source: text,
      siteAssignmentId: nullableId,
      membershipRecordId: nullableId,
      temporaryChangeId: nullableId,
      originalShiftVersionId: nullableId,
      rule: text,
      officeRosterAssignmentId: id.optional(),
    })
    .nullable(),
  holiday: z
    .object({
      status: text,
      businessDate: date,
      calendarId: nullableId,
      calendarCode: text.nullable(),
      calendarRevision: integer.nullable(),
      holidays: z.array(z.object({ id, name: text })),
      isInformational: z.boolean(),
      doesNotCancelDuty: z.boolean(),
    })
    .nullable(),
});
export const summaryRevision = z.object({
  id,
  revision: integer.positive(),
  status: text,
  policyVersionId: nullableId,
  shiftVersionId: nullableId,
  workedMinutes: minutes,
  payableMinutes: minutes.nullable(),
  lateMinutes: minutes.nullable(),
  overtimeMinutes: minutes.nullable(),
  calculationVersion: text,
  inputFingerprint: timeline,
  calculatedAtUtc: timestamp,
});
export const day = z.object({
  employeeId: id,
  businessDate: date,
  timeZone: text,
  factualStatus: text,
  facts: z.object({
    workedSeconds: minutes,
    workedMinutes: minutes,
    openSessions: minutes,
    sessionIds: z.array(id),
    inputFingerprint: timeline,
  }),
  roster,
  policy: z.object({
    status: text,
    policyVersionId: nullableId,
    calculationVersion: text.nullable(),
    payableMinutes: minutes.nullable(),
    lateMinutes: minutes.nullable(),
    overtimeMinutes: minutes.nullable(),
    dayStatus: integer.nullable(),
    explanations: z.array(text),
    inputProvenance: text.optional(),
    payrollReady: z.boolean().optional(),
  }),
  exceptionCount: minutes,
  isLocked: z.boolean(),
  latestSavedRevision: summaryRevision.nullable(),
});
export const calendar = z.array(day).max(31);
const officeEvidence = {
  branchId: id.optional(),
  officeRosterAssignmentId: id.optional(),
  officeGeofenceVersionId: id.optional(),
};
const punchValidation = z
  .object({
    evidenceId: id,
    capturePolicyVersionId: nullableId,
    capturePolicyRevision: integer.positive().nullable(),
    capturePolicyFingerprint: timeline.nullable(),
    codes: z.array(z.string().min(1)),
    relatedRawPunchIds: z.array(id),
  })
  .refine(
    (value) =>
      new Set(value.relatedRawPunchIds).size === value.relatedRawPunchIds.length &&
      (value.capturePolicyVersionId === null
        ? value.capturePolicyRevision === null && value.capturePolicyFingerprint === null
        : value.capturePolicyRevision !== null && value.capturePolicyFingerprint !== null),
    'Inconsistent capture-validation evidence.',
  );
const validOfficeEvidence = (value: {
  siteId: string | null;
  branchId?: string;
  officeRosterAssignmentId?: string;
  officeGeofenceVersionId?: string;
}) =>
  value.branchId
    ? value.siteId === null
    : !value.officeRosterAssignmentId && !value.officeGeofenceVersionId;
export const punch = z
  .object({
    id,
    action: text,
    receivedAtUtc: timestamp,
    businessDate: date,
    outcome: text,
    reasonCode: text,
    siteId: nullableId,
    shiftVersionId: nullableId,
    sessionKey: nullableId,
    isOffline: z.boolean(),
    assessmentId: nullableId,
    calculationStatus: text,
    businessDateSource: text,
    ...officeEvidence,
    validation: punchValidation.optional(),
  })
  .refine(validOfficeEvidence, 'Office and site source evidence cannot be mixed.');
export const session = z
  .object({
    id,
    sessionKey: id,
    revision: integer.positive(),
    businessDate: date,
    startsAtUtc: timestamp,
    endsAtUtc: nullableTime,
    shiftVersionId: id,
    siteId: nullableId,
    checkInPunchId: nullableId,
    checkOutPunchId: nullableId,
    factualWorkedMinutes: z.number().nonnegative().nullable(),
    ...officeEvidence,
    employeeAssignmentId: id.optional(),
    holidayCalendarId: id.optional(),
  })
  .refine(validOfficeEvidence, 'Office and site session sources cannot be mixed.');
export const cursorPage = <T extends z.ZodType>(item: T) =>
  z.object({ items: z.array(item), nextCursor: text.nullable() });
export const punchAudit = z.object({
  punch,
  assessmentRevision: integer.nullable(),
  assessedAtUtc: nullableTime,
  claimedDeviceCapturedAtUtc: nullableTime,
});
export const sessionAudit = z.object({
  session,
  recordedAtUtc: timestamp,
  isLatestRevision: z.boolean(),
  correctionId: nullableId,
});
export const correctionSession = z.object({
  id,
  sessionKey: id,
  revision: integer,
  startsAtUtc: timestamp,
  endsAtUtc: nullableTime,
  checkInPunchId: nullableId,
  checkOutPunchId: nullableId,
  correctionId: nullableId,
  siteId: nullableId,
  shiftVersionId: id,
  policyVersionId: nullableId,
  endsContinuousDuty: z.boolean(),
});
export const correctionSource = z.object({
  businessDate: date,
  rawPunchId: nullableId,
  action: text.nullable(),
  originalTimeUtc: nullableTime,
  latestAssessmentId: nullableId,
  assessmentOutcome: text.nullable(),
  latestSummaryId: nullableId,
  summaryRevision: integer.nullable(),
  summaryStatus: text.nullable(),
  periodLocked: z.boolean(),
  routingStatus: text,
  summaryCalculationState: text,
  historicalRoster: roster.nullable(),
  sourceFingerprint: timeline.optional(),
  sessionRevisions: z.array(correctionSession).optional(),
  originalPunchContext: z
    .object({
      businessDate: date.nullable(),
      siteId: nullableId,
      employeeAssignmentId: nullableId,
      shiftVersionId: nullableId,
      geofenceVersionId: nullableId,
      siteRosterAssignmentId: nullableId,
      temporaryShiftChangeId: nullableId,
      holidayCalendarId: nullableId,
      isOffline: z.boolean(),
    })
    .optional(),
  dutyProofs: z
    .array(
      z.object({
        workSessionId: id,
        checkInPunchId: nullableId,
        assessmentId: nullableId,
        assessmentOutcome: text.nullable(),
        verificationId: nullableId,
        historicallyVerified: z.boolean(),
      }),
    )
    .optional(),
});
export const correction = z.object({
  id,
  employeeId: id,
  businessDate: date,
  kind: text,
  rawPunchId: nullableId,
  originalSummaryId: nullableId,
  originalTimeUtc: nullableTime,
  requestedTimeUtc: nullableTime,
  requestedEndUtc: nullableTime,
  requestedStatus: text.nullable(),
  reason: text,
  submittedAtUtc: timestamp,
  state: text,
  routingStatus: text,
  version: rowVersion,
  changesAttendance: z.boolean(),
  appliedSessionRevisionId: id.optional(),
  appliedSummaryRevisionId: id.optional(),
  interpretation: text.optional(),
});
export const correctionDetail = z.object({
  request: correction,
  writtenEvidence: text,
  capturedSource: correctionSource,
});
export const correctionDecision = z.object({
  request: correction,
  decisionId: id,
  appliedSessionRevisionId: nullableId,
  appliedSummaryRevisionId: nullableId,
});
export const exceptionNotice = z.object({
  id,
  businessDate: date,
  noticeCode: text,
  detectedAtUtc: timestamp,
  punchId: nullableId,
  sessionRevisionId: nullableId,
  summaryId: nullableId,
  nextAction: text,
});
export const exceptionItem = z.object({
  id,
  employeeId: id,
  businessDate: date,
  kind: text,
  detectedAtUtc: timestamp,
  punchId: nullableId,
  sessionRevisionId: nullableId,
  summaryId: nullableId,
});
export const exceptionDetail = z.object({ exception: exceptionItem, detectionReason: text });
export const carrySession = z.object({
  sessionKey: id,
  sourceWorkSessionId: nullableId,
  startsAtUtc: timestamp,
  endsAtUtc: timestamp,
  siteId: id,
  shiftVersionId: id,
  isManual: z.boolean(),
  originCorrectionId: nullableId,
  checkInPunchId: nullableId,
  checkOutPunchId: nullableId,
});
export const carrySource = z.object({
  employeeId: id,
  sourceBusinessDate: date,
  sourceLockEventId: id,
  sourceSummaryId: id,
  sourceSummaryRevision: integer,
  priorAdjustmentId: nullableId,
  priorRevision: integer,
  targetMonthStart: date,
  targetMonthEndExclusive: date,
  currentWorkedTicks: integer,
  currentWorkedMinutes: integer,
  sourceFingerprint: timeline,
  sessions: z.array(carrySession),
  historicalRoster: roster,
  rawPunchId: nullableId,
  latestAssessmentId: nullableId,
  financialState: text,
});
export const carryRequest = z.object({
  id,
  employeeId: id,
  sourceBusinessDate: date,
  kind: text,
  state: text,
  version: rowVersion,
  submittedAtUtc: timestamp,
  targetMonthStart: date,
  targetMonthEndExclusive: date,
  sourceLockEventId: id,
  sourceSummaryId: id,
  priorAdjustmentId: nullableId,
  appliedAdjustmentId: nullableId,
  routingStatus: text,
  financialState: text,
});
export const carryDetail = z.object({
  request: carryRequest,
  writtenEvidence: text,
  source: carrySource,
  proposedInterpretation: z.array(carrySession),
});
export const carryDecision = z.object({
  request: carryRequest,
  decisionId: id,
  appliedAdjustmentId: nullableId,
  deltaWorkedTicks: integer.nullable(),
  deltaWorkedMinutes: integer.nullable(),
  financialState: text,
});
export const carryEntry = z.object({
  id,
  sourceBusinessDate: date,
  revision: integer,
  correctionId: id,
  sourceLockEventId: id,
  sourceSummaryId: id,
  targetMonthStart: date,
  deltaWorkedTicks: integer,
  deltaWorkedMinutes: integer,
  appliedAtUtc: timestamp,
  financialState: text,
});
export const carryMonth = z.object({
  employeeId: id,
  monthStart: date,
  monthEndExclusive: date,
  entries: z.array(carryEntry),
  totalCount: minutes,
  page: integer.positive(),
  pageSize: integer.positive(),
  totalDeltaWorkedTicks: integer,
  totalDeltaWorkedMinutes: integer,
  interpretation: text,
  financialState: text,
});
export const eligibility = z.object({
  status: text,
  versionId: nullableId,
  allowed: z.boolean().nullable(),
  globalVersionId: nullableId,
  employeeVersionId: nullableId,
});
export const overtimeSession = z.object({
  id,
  sessionKey: id,
  revision: integer,
  startsAtUtc: timestamp,
  endsAtUtc: nullableTime,
  siteId: nullableId,
  shiftVersionId: id,
  policyVersionId: nullableId,
  checkInAssessmentId: nullableId,
  checkOutAssessmentId: nullableId,
  correctionDecisionId: nullableId,
  validDutyEvidence: z.boolean(),
  correctionId: nullableId,
});
export const overtimeSource = z.object({
  employeeId: id,
  businessDate: date,
  staffCategory: text,
  siteId: nullableId,
  roster,
  sessions: z.array(overtimeSession),
  summaryId: nullableId,
  summaryInputFingerprint: text.nullable(),
  isLocked: z.boolean(),
  eligibility,
  durationPolicy: z.object({
    status: text,
    policyVersionId: nullableId,
    calculationVersion: text.nullable(),
    scheduledDutyCompleted: z.boolean().nullable(),
    eligibleIntervals: z.array(z.object({ startsAtUtc: timestamp, endsAtUtc: timestamp })),
    rateRuleCode: text.nullable(),
  }),
  sourceFingerprint: timeline,
});
export const overtime = z.object({
  id,
  employeeId: id,
  businessDate: date,
  workSessionId: id,
  startsAtUtc: timestamp,
  endsAtUtc: timestamp,
  requestedMinutes: minutes,
  approvedMinutes: minutes.nullable(),
  siteId: nullableId,
  jobReference: text.nullable(),
  state: text,
  version: rowVersion,
  requestedAtUtc: timestamp,
  bulkSubmissionId: nullableId,
  decisionId: nullableId,
  durationState: text,
  financialState: text,
  payableAmount: z.null(),
  approvedTicks: integer.nullable(),
  currency: text.nullable(),
});
export const overtimeDetail = z.object({
  request: overtime,
  source: overtimeSource,
  writtenEvidence: text.nullable(),
  evidenceVisibility: text,
});
export const overtimeBulkResponse = z.object({ bulkId: id, employees: z.array(overtime) });
export const overtimeFinancial = z.object({
  requestId: id,
  employeeId: id,
  businessDate: date,
  state: text,
  approvedTicks: integer.nullable(),
  amount: z.number().nullable(),
  currency: text.nullable(),
  formulaVersion: text.nullable(),
});
export const eligibilityEntry = z.object({
  id,
  revision: integer,
  supersedesVersionId: nullableId,
  effectiveFrom: date,
  isEnabled: z.boolean(),
  effect: text,
  actor: text,
  recordedAtUtc: timestamp,
  reason: text,
});
export const eligibilityPreview = z.object({
  target: text,
  employeeId: nullableId,
  asOf: date,
  latestRevision: minutes,
  timelineFingerprint: timeline,
  effective: eligibilityEntry.nullable(),
  state: text,
  eligibility: eligibility.nullable(),
});
export const decisionInput = z.strictObject({
  expectedVersion: rowVersion,
  decision: z.union([z.literal(1), z.literal(2), z.literal(3)]),
  reason: reason(),
});
export const cancelInput = z.strictObject({ expectedVersion: rowVersion, reason: reason() });
export const recalculateInput = z.strictObject({
  businessDate: date,
  expectedRevision: minutes,
  reason: reason(),
});
export const utc = timestamp.refine(
  (v) => v.endsWith('Z') || v.endsWith('+00:00'),
  'Use a UTC timestamp with Z or +00:00.',
);
const kind = z.union([z.literal(1), z.literal(2), z.literal(4)]);
export const correctionInput = z
  .strictObject({
    businessDate: date,
    kind,
    rawPunchId: nullableId,
    expectedAssessmentId: nullableId,
    expectedSummaryId: nullableId,
    requestedTimeUtc: utc,
    requestedEndUtc: utc.nullable(),
    requestedStatus: z.null(),
    reason: reason(),
    writtenEvidence: reason(4000),
    expectedSourceFingerprint: timeline,
    targetWorkSessionId: id.optional(),
    resubmissionOfCorrectionId: id.optional(),
  })
  .superRefine((v, ctx) => {
    if (
      v.kind === 4
        ? !v.requestedEndUtc ||
          v.targetWorkSessionId ||
          Date.parse(v.requestedEndUtc) <= Date.parse(v.requestedTimeUtc)
        : v.requestedEndUtc !== null || (!v.rawPunchId && !(v.kind === 2 && v.targetWorkSessionId))
    )
      ctx.addIssue({
        code: 'custom',
        message: 'Select retained punch/session evidence and valid times for this correction kind.',
      });
    if (!v.rawPunchId && v.expectedAssessmentId)
      ctx.addIssue({ code: 'custom', message: 'Assessment requires raw punch evidence.' });
  });
export const carryInput = z
  .strictObject({
    sourceBusinessDate: date,
    kind,
    targetSessionKey: nullableId,
    rawPunchId: nullableId,
    requestedTimeUtc: utc,
    requestedEndUtc: utc.nullable(),
    expectedSourceLockEventId: id,
    expectedSourceSummaryId: id,
    expectedPriorAdjustmentId: nullableId,
    expectedTargetMonthStart: date,
    expectedSourceFingerprint: timeline,
    reason: reason(),
    writtenEvidence: reason(4000),
    resubmissionOfRequestId: id.optional(),
  })
  .superRefine((v, ctx) => {
    if (
      v.kind === 4
        ? !v.requestedEndUtc ||
          v.targetSessionKey !== null ||
          Date.parse(v.requestedEndUtc) <= Date.parse(v.requestedTimeUtc)
        : !v.targetSessionKey || v.requestedEndUtc !== null
    )
      ctx.addIssue({
        code: 'custom',
        message: 'Select the retained session and valid typed times.',
      });
  });
export const overtimeInput = z
  .strictObject({
    businessDate: date,
    workSessionId: id,
    startsAtUtc: utc,
    endsAtUtc: utc,
    activity: z.union([z.literal(1), z.literal(2)]),
    expectedSourceFingerprint: timeline,
    reason: reason(),
    writtenEvidence: reason(4000),
    jobReference: reason(200).optional(),
    resubmissionOfRequestId: id.optional(),
  })
  .refine(
    (v) =>
      Date.parse(v.endsAtUtc) - Date.parse(v.startsAtUtc) >= 60000 &&
      Date.parse(v.endsAtUtc) - Date.parse(v.startsAtUtc) <= 604800000,
    'The proposed interval must be between one minute and seven days.',
  );
export const eligibilityInput = z.strictObject({
  effectiveFrom: date,
  isEnabled: z.boolean(),
  expectedRevision: minutes,
  expectedTimeline: timeline,
  reason: reason(),
});
export { pageOf };
export type Day = z.infer<typeof day>;
