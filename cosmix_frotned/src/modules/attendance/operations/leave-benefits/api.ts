import { z } from 'zod';
import { queryString } from '../../api/attendance-api';
import { date, id, reason, rowVersion, timeline, timestamp } from '../../schemas/common';
import { configurableLeaveSettings, leavePolicy, leaveSettings } from '../leave-policy/contracts';

export const permissions = {
  benefits: 'attendance.leaves.benefits.review',
  recovery: 'attendance.leaves.review',
  configure: 'attendance.leaves.configure',
} as const;

// Read-only extension bags retain the complete published policy, without supplying defaults.
const extendedSettingsBlock = z.record(z.string(), z.json()).nullable().optional();
const extendedLeaveSettings = z
  .object({
    ...configurableLeaveSettings.shape,
    settingsVersion: z.union([z.literal(3), z.literal(4)]),
    encashment: extendedSettingsBlock,
    recovery: extendedSettingsBlock,
    benefits: extendedSettingsBlock,
    recoveryFulfilment: extendedSettingsBlock,
  })
  .catchall(z.json())
  .refine(
    (value) => value.yearStartDay <= new Date(Date.UTC(2001, value.yearStartMonth, 0)).getUTCDate(),
    'Annual boundary must be valid in every year.',
  )
  .refine(
    (value) =>
      value.encashment != null ||
      value.recovery != null ||
      value.benefits != null ||
      value.recoveryFulfilment != null,
    'Extended settings must contain an explicit configuration block.',
  )
  .refine(
    (value) =>
      value.settingsVersion === 4 || (value.benefits == null && value.recoveryFulfilment == null),
    'Benefits and recovery fulfilment require v4 settings.',
  )
  .refine(
    (value) => value.recoveryFulfilment == null || value.recovery != null,
    'Recovery fulfilment requires recovery settings.',
  );
export const readableLeavePolicy = leavePolicy
  .extend({ settings: z.union([leaveSettings, extendedLeaveSettings]) })
  .catchall(z.json());
export const readableLeavePolicies = z.array(readableLeavePolicy);
export type PublishedLeavePolicy = z.infer<typeof readableLeavePolicy>;

const minutes = z.number().int().nonnegative();
const allocation = z.strictObject({ ledgerEntryId: id, minutes: z.number().int().positive() });
export const benefit = z
  .object({
    id,
    employeeId: id,
    leaveTypeId: id,
    sourceFrom: date,
    sourceToExclusive: date,
    leaveYearFrom: date,
    leaveYearToExclusive: date,
    availableFrom: date,
    minutes,
    standardDayMinutes: z.number().int().min(1).max(1440),
    encashmentEligible: z.boolean(),
    policyVersionId: id,
    kpiFinalizationId: id,
    status: z.string(),
    evidenceFingerprint: timeline,
    recordedAtUtc: timestamp,
    correction: z
      .object({
        id,
        removedUnusedMinutes: minutes,
        consumedMinutes: minutes,
        protectedConsumedMinutes: minutes,
        status: z.string(),
        consumedUsage: z.array(allocation),
        recordedAtUtc: timestamp,
      })
      .nullable()
      .optional(),
  })
  .catchall(z.json());
export const benefits = z.array(benefit);
export type Benefit = z.infer<typeof benefit>;

const usageEntry = z
  .object({
    id,
    employeeId: id,
    leaveTypeId: id,
    policyVersionId: id,
    yearFrom: date,
    yearTo: date,
    type: z.union([z.literal(2), z.literal(3), z.literal(4), z.literal(6)]),
    minutes: z.number().int(),
    requestId: id.nullable(),
    reversesId: id.nullable(),
    recordedAt: timestamp,
    standardDayMinutes: z.number().int().min(1).max(1440),
  })
  .catchall(z.json());
export const benefitImpact = z.object({
  usageFingerprint: timeline,
  entries: z
    .array(usageEntry)
    .max(10000)
    .refine((rows) => new Set(rows.map((row) => row.id)).size === rows.length),
});
export type BenefitImpact = z.infer<typeof benefitImpact>;
export const usageNames = {
  2: 'Reservation',
  3: 'Utilization',
  4: 'Release',
  6: 'Encashment',
} as const;

export const awardInput = z.strictObject({
  leaveTypeId: id,
  policyVersionId: id,
  kpiYearFrom: date.refine((value) => value >= '1901-01-01' && value <= '9991-12-31'),
  expectedKpiFinalizationId: id,
  reason: reason(),
});
const revokeInput = z.strictObject({ expectedEvidenceFingerprint: timeline, reason: reason() });
const correctionInput = revokeInput.extend({
  expectedUsageFingerprint: timeline,
  consumedUsage: z
    .array(allocation)
    .max(1000)
    .refine(
      (rows) => new Set(rows.map((row) => row.ledgerEntryId)).size === rows.length,
      'Select each source only once.',
    ),
});

export const benefitRoot = (employeeId: string) =>
  `/leaves/admin/employees/${id.parse(employeeId)}/benefits`;
export const benefitHistoryPath = (employeeId: string, yearFrom: string) =>
  benefitRoot(employeeId) + queryString({ yearFrom: date.parse(yearFrom) });
export const benefitImpactPath = (row: Benefit) =>
  `${benefitRoot(row.employeeId)}/${id.parse(row.id)}/correction-impact`;

export function awardIntent(employeeId: string, body: unknown) {
  return {
    path: benefitRoot(employeeId),
    method: 'POST' as const,
    schema: benefit,
    body: awardInput.parse(body),
  };
}
export function revokeIntent(row: Benefit, inputReason: string) {
  if (row.status !== 'awarded') throw new Error('Reload an active award before revoking.');
  return {
    path: `${benefitRoot(row.employeeId)}/${id.parse(row.id)}/revoke`,
    method: 'POST' as const,
    schema: benefit,
    body: revokeInput.parse({
      expectedEvidenceFingerprint: row.evidenceFingerprint,
      reason: inputReason,
    }),
  };
}
export function releasedIds(impact: BenefitImpact) {
  return new Set(impact.entries.filter((row) => row.type === 4).map((row) => row.reversesId));
}
export function hasPendingReservation(impact: BenefitImpact) {
  const released = releasedIds(impact);
  return impact.entries.some((row) => row.type === 2 && !released.has(row.id));
}
export function allocationSources(row: Benefit, impact: BenefitImpact) {
  const released = releasedIds(impact);
  return impact.entries.filter(
    (entry) =>
      (entry.type === 3 || (entry.type === 6 && row.encashmentEligible)) &&
      !released.has(entry.id) &&
      entry.minutes < 0 &&
      entry.employeeId === row.employeeId &&
      entry.leaveTypeId === row.leaveTypeId &&
      entry.yearFrom === row.leaveYearFrom &&
      entry.yearTo === row.leaveYearToExclusive &&
      Date.parse(entry.recordedAt) >= Date.parse(row.recordedAtUtc),
  );
}
export function correctionIntent(
  row: Benefit,
  impact: BenefitImpact,
  consumedUsage: unknown,
  inputReason: string,
) {
  if (row.status !== 'awarded') throw new Error('Reload an active award before correcting.');
  if (hasPendingReservation(impact))
    throw new Error(
      'Pending reservations must complete their existing workflow before correction.',
    );
  const body = correctionInput.parse({
    expectedEvidenceFingerprint: row.evidenceFingerprint,
    expectedUsageFingerprint: impact.usageFingerprint,
    consumedUsage,
    reason: inputReason,
  });
  const sources = allocationSources(row, impact);
  for (const selected of body.consumedUsage) {
    const source = sources.find((entry) => entry.id === selected.ledgerEntryId);
    if (!source || selected.minutes > -source.minutes)
      throw new Error('An allocation exceeds its exact source or the source is unavailable.');
  }
  // Keep each allocation in its source's minutes. Conversion and shared capacity are server-owned.
  return {
    path: `${benefitRoot(row.employeeId)}/${id.parse(row.id)}/correct`,
    method: 'POST' as const,
    schema: benefit,
    body,
  };
}

export const recoveryClaim = z
  .object({
    id,
    employeeId: id,
    leaveRequestId: id,
    commitmentId: id,
    policyVersionId: id,
    state: z.number().int().min(0).max(4),
    version: rowVersion,
    requestedMinutes: minutes,
    approvedMinutes: minutes.nullable(),
    submittedAt: timestamp,
    decidedAt: timestamp.nullable(),
  })
  .catchall(z.json());
export const recoveryQueue = z.array(recoveryClaim);
export type RecoveryClaim = z.infer<typeof recoveryClaim>;
const evidenceObject = z.record(z.string(), z.json());
export const recoveryDetail = z
  .object({
    claim: recoveryClaim,
    request: z
      .object({
        policyVersionId: id,
        expectedSourceFingerprint: timeline,
        reason: z.string(),
        intervals: z.array(
          z
            .object({ sessionId: id, businessDate: date, startUtc: timestamp, endUtc: timestamp })
            .catchall(z.json()),
        ),
      })
      .catchall(z.json()),
    retainedSource: z
      .object({
        parent: evidenceObject,
        policy: z
          .object({
            versionId: id,
            staffCategoryId: id,
            rulesJson: z.string(),
            effectiveFrom: date,
            effectiveTo: date.nullable(),
            publishedAt: timestamp,
            settings: evidenceObject,
          })
          .catchall(z.json()),
        employment: evidenceObject,
        rosters: z.array(evidenceObject),
        sessions: z.array(evidenceObject),
        fingerprint: timeline,
      })
      .catchall(z.json()),
  })
  .catchall(z.json());
export const recoveryProgress = z
  .object({
    leaveRequestId: id,
    commitmentId: id,
    committedMinutes: minutes,
    fulfilledMinutes: minutes,
    remainingMinutes: minutes,
    status: z.string(),
    claims: z.array(recoveryClaim),
  })
  .catchall(z.json());
export const recoveryQueuePath = '/leaves/admin/recovery-work/queue';
export const recoveryDetailPath = (employeeId: string, claimId: string) =>
  `/leaves/admin/recovery-work/${id.parse(employeeId)}/${id.parse(claimId)}`;
export const recoveryProgressPath = (claim: RecoveryClaim) =>
  `/leaves/admin/${id.parse(claim.leaveRequestId)}/recovery-commitments/${id.parse(claim.commitmentId)}/progress`;
export function recoveryDecisionIntent(
  claim: RecoveryClaim,
  decision: string,
  inputReason: string,
) {
  if (claim.state !== 0) throw new Error('Only a pending recovery claim can be reviewed.');
  return {
    path: recoveryDetailPath(claim.employeeId, claim.id) + '/decisions',
    method: 'POST' as const,
    schema: recoveryClaim,
    body: z
      .strictObject({
        expectedVersion: rowVersion,
        decision: z.union([z.literal(1), z.literal(2), z.literal(3)]),
        reason: reason(),
      })
      .parse({ expectedVersion: claim.version, decision: Number(decision), reason: inputReason }),
  };
}
