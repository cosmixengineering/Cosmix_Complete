import { z } from 'zod';
import { date, id, pageOf, reason, rowVersion, timestamp, timeline } from '../../schemas/common';

const unique = <T>(rows: T[]) => new Set(rows).size === rows.length;
export const leaveKind = z.number().int().min(0).max(6);
export const leaveType = z.object({
  id,
  code: z.string(),
  name: z.string(),
  kind: leaveKind,
  requiresEvidence: z.boolean(),
  requiresAdvanceApproval: z.boolean(),
  reducesRegularBalance: z.boolean(),
  isActive: z.boolean(),
  version: rowVersion,
});
export const leaveTypes = z.array(leaveType);
const leaveSettingsBase = {
  standardDayMinutes: z.number().int().min(1).max(1440),
  eligibleStaffCategoryIds: z.array(id).min(1).max(100).refine(unique),
  holidayCalendarId: id,
  holidayTreatment: z.union([z.literal(1), z.literal(2)]),
  paidKinds: z.array(leaveKind).max(7).refine(unique),
};
const legacyLeaveSettings = z.strictObject({
  ...leaveSettingsBase,
  annualDays: z.literal(18),
  sickDays: z.literal(6),
  casualDays: z.literal(12),
  minimumServiceMonths: z.literal(4),
  settingsVersion: z.literal(1).optional(),
});
const configurableCount = z.number().int().min(0).max(2147483647);
export const configurableLeaveSettings = z
  .strictObject({
    ...leaveSettingsBase,
    settingsVersion: z.literal(2),
    annualDays: configurableCount,
    sickDays: configurableCount,
    casualDays: configurableCount,
    minimumServiceMonths: configurableCount,
    yearStartMonth: z.number().int().min(1).max(12),
    yearStartDay: z.number().int().min(1).max(31),
  })
  .refine(
    (value) => value.yearStartDay <= new Date(Date.UTC(2001, value.yearStartMonth, 0)).getUTCDate(),
    'Annual boundary must be valid in every year.',
  );
export const leaveSettings = z.union([legacyLeaveSettings, configurableLeaveSettings]);
export const leavePolicy = z.object({
  policyId: id,
  versionId: id,
  code: z.string(),
  name: z.string(),
  effectiveFrom: date,
  effectiveTo: date.nullable(),
  settings: leaveSettings,
  authority: z.string(),
});
export const leavePolicies = z.array(leavePolicy);
const approvalState = z.number().int().min(0).max(4);
export const leaveDecisionRecord = z.object({
  id,
  approvalRequestId: id,
  decision: approvalState,
  actorRole: z.string(),
  reason: z.string().nullable(),
  atUtc: timestamp,
});
export const leaveCancellation = z.object({
  id,
  approvalRequestId: id,
  state: approvalState,
  reason: z.string(),
  approvalVersion: rowVersion,
  requestedAtUtc: timestamp,
  decisions: z.array(leaveDecisionRecord),
});
export const leaveRecord = z.object({
  id,
  employeeId: id,
  leaveTypeId: id,
  kind: leaveKind,
  typeName: z.string(),
  previousRequestId: id.nullable(),
  supersededByRequestId: id.nullable(),
  startDate: date,
  endDate: date,
  requestedMinutes: z.number().int().nonnegative(),
  reason: z.string(),
  contactDuringLeave: z.string().nullable(),
  relieverEmployeeId: id.nullable(),
  handoverNotes: z.string().nullable(),
  hasEvidence: z.boolean(),
  approvalRequestId: id,
  state: approvalState,
  approvalVersion: rowVersion,
  treatment: z.number().int().min(0).max(3),
  policyVersionId: id.nullable(),
  days: z.array(
    z.object({
      id,
      date,
      startsAtUtc: timestamp,
      endsAtUtc: timestamp,
      minutes: z.number().int().positive(),
    }),
  ),
  decisions: z.array(leaveDecisionRecord),
  cancellations: z.array(leaveCancellation),
  financialStatus: z.string(),
});
export type LeaveRecord = z.infer<typeof leaveRecord>;
export type LeavePolicy = z.infer<typeof leavePolicy>;
export const leavePage = pageOf(leaveRecord);
const exactDays = z.object({
  numerator: z.string().regex(/^-?\d+$/),
  denominator: z.string().regex(/^[1-9]\d*$/),
});
const quota = z.object({
  limitDays: z.number().int(),
  usedDays: exactDays,
  remainingDays: exactDays,
});
export const selfPolicies = z.object({
  status: z.string(),
  policies: leavePolicies,
  reasons: z.array(z.string()),
  evaluatedAtUtc: timestamp,
});
export const leaveBalance = z.object({
  status: z.string(),
  asOf: date,
  fiscalYearStart: date,
  fiscalYearEndExclusive: date,
  evaluatedAtUtc: timestamp,
  usageBasis: z.string(),
  policyVersionId: id.nullable(),
  leaveType: leaveType.nullable(),
  policy: leavePolicy.nullable(),
  paidKindConfigured: z.boolean().nullable(),
  serviceEligible: z.boolean().nullable(),
  annualBalance: quota.nullable(),
  typeBalance: quota.nullable(),
  reasons: z.array(z.string()),
});
export const leavePreview = z.object({
  status: z.string(),
  evaluatedAtUtc: timestamp,
  approvalGuaranteed: z.literal(false),
  requestedMinutes: z.number().int().nonnegative(),
  requestedPolicyDays: exactDays.nullable(),
  balance: leaveBalance,
  requiresEvidence: z.boolean(),
  requiredEvidenceKinds: z.array(z.number().int().min(1).max(6)),
  reasons: z.array(z.string()),
});
export const leaveDayInput = z.strictObject({ date, startsAtUtc: timestamp, endsAtUtc: timestamp });
export const leaveSubmit = z.strictObject({
  leaveTypeId: id,
  days: z.array(leaveDayInput).min(1).max(366),
  reason: reason(),
  contactDuringLeave: z.string().max(200).nullable(),
  relieverEmployeeId: id.nullable(),
  handoverNotes: z.string().max(2000).nullable(),
  attachments: z
    .array(
      z.strictObject({
        kind: z.number().int().min(1).max(6),
        fileName: reason(200),
        contentType: z.enum(['image/png', 'image/jpeg', 'application/pdf']),
        content: z.string().min(1),
      }),
    )
    .max(4),
  previousRequestId: id.nullable(),
});
export const leaveDecision = z
  .strictObject({
    decision: z.number().int().min(1).max(3),
    expectedApprovalVersion: rowVersion,
    reason: reason(),
    treatment: z.number().int().min(0).max(3),
    policyVersionId: id.nullable(),
  })
  .refine(
    (v) =>
      v.decision === 1
        ? v.treatment !== 0 && v.policyVersionId !== null
        : v.treatment === 0 && v.policyVersionId === null,
    'Approval requires explicit treatment and policy; return/reject require unresolved treatment and no policy.',
  );
export const cancellationInput = z.strictObject({
  expectedApprovalVersion: rowVersion,
  reason: reason(),
});
export const cancellationDecision = cancellationInput.extend({
  decision: z.number().int().min(1).max(3),
});
export const code = z
  .string()
  .trim()
  .regex(/^[A-Za-z0-9][A-Za-z0-9_.-]{0,39}$/);
export const leavePolicyInput = z
  .strictObject({
    code,
    name: reason(200),
    effectiveFrom: date,
    effectiveTo: date,
    settings: leaveSettings,
    reason: reason(),
  })
  .refine((v) => v.effectiveTo > v.effectiveFrom, 'Exclusive end must follow start.');
export const leaveTypeInput = z
  .strictObject({
    code,
    name: reason(200),
    kind: leaveKind,
    requiresEvidence: z.boolean(),
    requiresAdvanceApproval: z.boolean(),
    reducesRegularBalance: z.boolean(),
    reason: reason(),
  })
  .refine(
    (v) =>
      (![1, 3, 5].includes(v.kind) || v.requiresEvidence) &&
      (v.kind !== 2 || v.requiresAdvanceApproval) &&
      (v.kind !== 5 || !v.reducesRegularBalance) &&
      (![1, 2].includes(v.kind) || v.reducesRegularBalance),
    'Type flags contradict the supported leave kind.',
  );

const numeric = z.number().finite().nullable();
const integer = z.number().int().nullable();
const choice = <T extends readonly [string, ...string[]]>(values: T) => z.enum(values).nullable();
export const salaryDivisor = ['actual_calendar_month_days', 'fixed_days'] as const;
export const rounding = [
  'whole_currency_unit_half_up',
  'two_decimal_places_half_up',
  'whole_currency_unit_half_even',
  'two_decimal_places_half_even',
] as const;
export const ordinaryPay = z.strictObject({
  salaryBasis: choice(['base_salary', 'gross_salary']),
  salaryDivisor: choice(salaryDivisor),
  fixedSalaryDivisorDays: integer,
  fullUnpaidDayDeductionDays: numeric,
  unpaidHalfDayDeductionDays: numeric,
  hourlyShortfallCalculation: choice(['daily_rate_over_scheduled_hours']),
  paidLeaveTreatment: choice(['no_deduction']),
  weeklyOffTreatment: choice(['no_deduction']),
  applicablePaidHolidayTreatment: choice(['no_deduction']),
  rounding: choice(rounding),
});
export const overtimePay = z.strictObject({
  standardDutySeconds: z.number().int().min(60).max(86400).nullable(),
  salaryBasis: choice(['base_salary']),
  salaryDivisor: choice(salaryDivisor),
  fixedSalaryDivisorDays: z.number().int().min(1).max(366).nullable(),
  rateMultiplier: z.number().positive().max(100).multipleOf(0.0001).nullable(),
  rounding: choice(rounding),
  breakTreatment: choice(['count_all_breaks']),
});
export type OvertimePay = z.infer<typeof overtimePay>;
export const policyRules = z.strictObject({
  schemaVersion: z.literal(3),
  arrival: z.strictObject({
    lateAfterShiftStartSeconds: integer,
    halfDayAfterShiftStartSeconds: integer,
    hourlyAfterShiftStartSeconds: integer,
    lateThresholdComparison: choice(['strictly_after', 'at_or_after']).optional(),
  }),
  paidTime: z.strictObject({
    basis: choice(['actual_elapsed_duty', 'scheduled_window_overlap']),
    breakTreatment: choice(['count_all_breaks', 'deduct_scheduled_unpaid_break']),
    earlyExitTreatment: choice(['actual_worked_time', 'half_day_credit']),
    halfDayCreditSeconds: integer,
    offDayTreatment: choice(['actual_worked_time', 'no_credit', 'scheduled_day_credit']),
    holidayTreatment: choice(['actual_worked_time', 'no_credit', 'scheduled_day_credit']),
    partialLeaveHalfDayTreatment: choice(['half_day_total_includes_paid_leave']),
  }),
  lateDeduction: z.strictObject({
    occurrenceThreshold: integer,
    resetPeriod: choice(['calendar_month', 'calendar_year', 'fiscal_july_june', 'weekly']),
    countHalfDayArrival: z.boolean().nullable(),
    countHourlyArrival: z.boolean().nullable(),
    salaryBasis: choice(['base_salary', 'gross_salary']),
    salaryDivisor: choice(salaryDivisor),
    fixedSalaryDivisorDays: integer,
    deductionDaysPerThreshold: numeric,
    rounding: choice(rounding),
    policyChangeCountingStrategy: choice(['restart_on_late_rule_change']),
    salaryEffectiveDateBasis: choice(['threshold_completion_date']),
    enabled: z.boolean().nullable(),
    deductionKind: choice(['day_fraction', 'fixed_amount', 'hours']),
    fixedAmountPkr: numeric,
    deductionHoursPerThreshold: numeric,
    weekStartsOn: choice([
      'sunday',
      'monday',
      'tuesday',
      'wednesday',
      'thursday',
      'friday',
      'saturday',
    ]),
    overlapTreatment: choice(['larger_of_ordinary_or_late']),
  }),
  ordinaryPay,
  overtime: overtimePay.nullable().optional(),
});
export type PolicyRules = z.infer<typeof policyRules>;
export const ruleErrors = z.array(
  z.object({ path: z.string(), code: z.string(), message: z.string() }),
);
export const settingsValidation = z.object({
  isValid: z.boolean(),
  published: z.literal(false),
  canonicalJson: z.string().nullable(),
  configurationSha256: timeline.nullable(),
  errors: ruleErrors,
});
export const financialRecommendations = z.object({
  schemaVersion: z.literal(3),
  published: z.literal(false),
  requiresExplicitPublication: z.literal(true),
  ordinaryPay,
  policyChangeCountingStrategy: z.literal('restart_on_late_rule_change'),
  salaryEffectiveDateBasis: z.literal('threshold_completion_date'),
  overlapTreatment: z.literal('larger_of_ordinary_or_late'),
});
export const policyDefinition = z.object({
  id,
  code: z.string(),
  name: z.string(),
  revision: z.number().int().nonnegative(),
  version: rowVersion,
});
export type PolicyDefinition = z.infer<typeof policyDefinition>;
export const policyVersion = z.object({
  id,
  number: z.number().int().positive(),
  effectiveFrom: date,
  effectiveTo: date.nullable(),
  canonicalJson: z.string(),
  configurationSha256: timeline,
  publishedAt: timestamp,
  authority: z.string(),
  publishedByUserId: z.string(),
});
export const policyAssignment = z.object({
  id,
  policyVersionId: id,
  target: z.enum(['employee', 'department', 'staff_category', 'site', 'role']),
  targetId: id,
  effectiveFrom: date,
  effectiveTo: date.nullable(),
  endId: id.nullable(),
  reason: z.string(),
  authority: z.string(),
  recordedByUserId: z.string(),
  recordedAt: timestamp,
  originalEffectiveTo: date.nullable(),
  endingReason: z.string().nullable(),
  endedByUserId: z.string().nullable(),
  endRecordedAt: timestamp.nullable(),
});
export const policyPageOf = <T extends z.ZodType>(item: T) =>
  z.object({
    items: z.array(item),
    page: z.number().int().positive(),
    pageSize: z.number().int().positive().max(100),
    hasMore: z.boolean(),
  });
export const publicationPreview = z.object({
  canPublish: z.boolean(),
  revision: z.number().int(),
  version: rowVersion,
  canonicalJson: z.string().nullable(),
  configurationSha256: timeline.nullable(),
  previewFingerprint: timeline,
  errors: ruleErrors,
  affectsBaseSalary: z.literal(false),
});
export const publicationResult = z.object({ policy: policyDefinition, published: policyVersion });
export const assignmentResult = z.object({
  policy: policyDefinition,
  assignment: policyAssignment,
});
