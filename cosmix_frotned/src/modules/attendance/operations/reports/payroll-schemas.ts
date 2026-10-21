import { z } from 'zod';
import { date, id, pageOf, timestamp, timeline } from '../../schemas/common';
import { matchesMonth } from './schemas';

const safeInteger = z.number().int().min(0).max(Number.MAX_SAFE_INTEGER);
const money = z.number().finite().nonnegative();
export const payrollLine = z.object({
  key: z.string().min(1).max(256),
  kind: z.string(),
  completionDate: date,
  sourceDates: z.array(date),
  policyVersionIds: z.array(id),
  compensationRecordId: id,
  salaryBasis: z.string(),
  monthlySalary: money,
  salaryDivisorDays: z.number().int().positive(),
  deductionDays: money,
  shortfallTicks: safeInteger,
  scheduledTicks: safeInteger,
  candidateAmount: money,
  amount: money,
  currency: z.literal('PKR'),
  rounding: z.string(),
  applicationCode: z.string(),
  ordinaryComparisonAmount: money,
  sourceIds: z.array(id),
  sourceFingerprints: z.array(timeline),
  compensationFingerprint: timeline,
  exactAmountNumerator: z.string().regex(/^\d+$/),
  exactAmountDenominator: z.string().regex(/^[1-9]\d*$/),
});
export const payrollPreview = z
  .object({
    employeeId: id,
    monthStart: date,
    monthEndExclusive: date,
    state: z.enum(['ready', 'unavailable', 'prepared_review_required', 'retained_input_revision']),
    previewFingerprint: timeline,
    expectedRevision: safeInteger,
    latestRevisionId: id.nullable(),
    canSave: z.boolean(),
    isLocked: z.boolean(),
    payrollReady: z.literal(false),
    paymentInstruction: z.literal(false),
    legalStatus: z.string(),
    deductionTotal: money.nullable(),
    currency: z.literal('PKR').nullable(),
    lines: z.array(payrollLine).max(732),
    issues: z.array(
      z.object({
        code: z.string(),
        businessDate: date.nullable(),
        sourceDates: z.array(date).nullable(),
      }),
    ),
    jurisdictions: z.array(
      z.object({
        businessDate: date,
        siteId: id.nullable(),
        configurationVersionId: id.nullable(),
        jurisdictionCode: z.string().nullable(),
        state: z.string(),
      }),
    ),
  })
  .refine(
    (value) =>
      new Set(value.lines.map((line) => line.key)).size === value.lines.length &&
      value.lines.every((line) => line.candidateAmount >= line.amount) &&
      (!value.canSave || (value.state === 'ready' && !value.isLocked && value.issues.length === 0)),
    'Incomplete or locked financial sources cannot be saved.',
  );
export const payrollRevision = z.object({
  id,
  employeeId: id,
  monthStart: date,
  revision: z.number().int().positive(),
  supersedesRevisionId: id.nullable(),
  calculationVersion: z.string(),
  inputFingerprint: timeline,
  createdAtUtc: timestamp,
  actorUserId: id,
  state: z.literal('prepared_review_required'),
});
export const payrollHistory = pageOf(payrollRevision);
export const payrollDetail = z
  .object({ revision: payrollRevision, inputs: payrollPreview })
  .refine(
    (value) =>
      value.revision.employeeId === value.inputs.employeeId &&
      value.revision.monthStart === value.inputs.monthStart &&
      value.inputs.latestRevisionId === value.revision.id &&
      value.inputs.expectedRevision === value.revision.revision &&
      !value.inputs.canSave,
    'Saved financial revision has inconsistent identity.',
  );
export const payrollAttestation = z
  .object({
    id,
    employeeId: id,
    monthStart: date,
    sourceRevisionId: id,
    sourceRevision: z.number().int().positive(),
    sourceFingerprint: timeline,
    previousAttestationId: id.nullable(),
    status: z.enum(['handoff', 'posted', 'settled']),
    confirmsPosting: z.boolean(),
    occurredAtUtc: timestamp,
    postedAtUtc: timestamp.nullable(),
    actorUserId: id,
    createdAtUtc: timestamp,
    lines: z
      .array(z.object({ key: z.string(), amount: money, currency: z.literal('PKR') }))
      .max(732),
    externalSystem: z.string(),
    externalReference: z.string(),
    evidenceReference: z.string(),
    evidenceSha256: timeline,
    statement: z.string(),
    reason: z.string(),
    verificationMode: z.literal('manual_authorized_attestation'),
    payrollReady: z.literal(false),
    paymentInstruction: z.literal(false),
  })
  .refine(
    (value) =>
      value.status === 'handoff'
        ? !value.confirmsPosting && value.postedAtUtc === null
        : value.confirmsPosting && value.postedAtUtc !== null && value.lines.length > 0,
    'Handoff is not posting evidence.',
  );
export const attestationHistory = z.array(payrollAttestation).max(3);

export function payrollPreviewFor(employeeId: string, period: string) {
  return payrollPreview.refine(
    (value) =>
      value.employeeId.toLowerCase() === employeeId.toLowerCase() && matchesMonth(value, period),
    'Payroll inputs do not match this employee and month.',
  );
}
export function payrollHistoryFor(employeeId: string, period: string) {
  return payrollHistory.refine(
    (value) =>
      value.items.every(
        (row) =>
          row.employeeId.toLowerCase() === employeeId.toLowerCase() &&
          row.monthStart === period + '-01',
      ),
    'Revision history does not match this employee and month.',
  );
}
export function payrollDetailFor(employeeId: string, period: string, revisionId?: string) {
  return payrollDetail.refine(
    (value) =>
      value.inputs.employeeId.toLowerCase() === employeeId.toLowerCase() &&
      matchesMonth(value.inputs, period) &&
      (!revisionId || value.revision.id.toLowerCase() === revisionId.toLowerCase()),
    'Revision does not match the selected financial source.',
  );
}
export function attestationHistoryFor(employeeId: string, period: string, revisionId: string) {
  return attestationHistory.refine(
    (value) =>
      value.every(
        (row) =>
          row.employeeId.toLowerCase() === employeeId.toLowerCase() &&
          row.monthStart === period + '-01' &&
          row.sourceRevisionId.toLowerCase() === revisionId.toLowerCase(),
      ),
    'Attestation does not match the selected financial source.',
  );
}
