import { useCallback, useRef, useState } from 'react';
import { z } from 'zod';
import { request } from '@/services/api/http-client';
import { useHrLifetime } from '../hooks/use-hr-lifetime';
import { useFinanceAccess } from '../hooks/use-finance-access';
import { financeError } from './finance-messages';

export const vehicleManagementPermissions = {
  policy: 'attendance.vehicle.policy.manage',
  monthly: 'attendance.vehicle.fuel.manage',
  attribution: 'attendance.vehicle.trips.manage',
  incident: 'attendance.vehicle.incident.manage',
};
export const vehicleManagementRoot = '/hr/vehicles';
const id = z.uuid();
const text = z.string();
const version = text.min(1);
const date = z.iso.date();
const stamp = z.iso.datetime({ offset: true });
const number = z.number();
const nullableText = text.nullable();
const rule = z.record(z.string(), z.unknown());
const pageOf = <T extends z.ZodType>(item: T) =>
  z.object({
    items: z.array(item),
    page: number.int().positive(),
    pageSize: number.int().positive(),
    totalCount: number.int().nonnegative(),
    hasMore: z.boolean(),
  });
export const vehiclePolicy = z.object({
  id,
  code: text,
  name: text,
  isActive: z.boolean(),
  revision: number.int().nonnegative(),
  version,
});
export const vehiclePolicyPage = pageOf(vehiclePolicy);
export const vehiclePolicyVersion = z.object({
  id,
  policyId: id,
  number: number.int().positive(),
  effectiveFrom: date,
  effectiveTo: date.nullable(),
  rulesSchemaVersion: text,
  rulesJson: text,
  configurationSha256: text,
  publishedAt: stamp,
  publishedByUserId: text,
  authority: text,
});
export const vehiclePolicyVersionPage = pageOf(vehiclePolicyVersion);
const policyErrors = z.array(z.object({ path: text, code: text }));
export const vehiclePolicyValidation = z.object({
  isValid: z.boolean(),
  canonicalJson: nullableText,
  configurationSha256: nullableText,
  errors: policyErrors,
});
export const vehiclePolicyPreview = z.object({
  canPublish: z.boolean(),
  revision: number.int().nonnegative(),
  version,
  canonicalJson: nullableText,
  configurationSha256: nullableText,
  previewFingerprint: version,
  errors: policyErrors,
});
export const vehiclePolicyPublication = z.object({
  policy: vehiclePolicy,
  published: vehiclePolicyVersion,
});

const monthlyLine = z.object({
  mileageEntryId: id,
  tripId: id,
  businessDate: date,
  policyVersionId: id,
  approvedBusinessKm: number,
  eligibleKm: number,
  commuteDistanceId: id.nullable(),
  baselineKm: number.nullable(),
  nearerReductionKm: number,
  evidenceFingerprint: text,
});
const partition = z.object({
  policyVersionId: id,
  rule,
  eligibleKm: number,
  nearerReductionKm: number,
  adjustmentKm: number,
  adjustedEligibleKm: number,
});
export const vehicleMonthlyCalculation = z.object({
  vehicleId: id,
  employeeId: id,
  monthStart: date,
  policyVersionId: id,
  eligibleKm: number,
  nearerReductionKm: number,
  adjustmentKm: number,
  adjustedEligibleKm: number,
  rule,
  lines: z.array(monthlyLine),
  sourceFingerprint: version,
  partitions: z.array(partition).optional(),
});
export const vehicleMonthlyPreview = z.object({
  calculation: vehicleMonthlyCalculation,
  latestRequestId: id.nullable(),
  latestApprovedRequestId: id.nullable(),
  previewFingerprint: version,
});
const decisionRecord = {
  requestId: id,
  revision: number.int().positive(),
  version,
  state: text,
  evidenceReference: text,
  reason: text,
  requestedByAccountId: id,
  requestedAt: stamp,
  decisionId: id.nullable(),
};
export const vehicleMonthlyRevision = z.object({
  ...decisionRecord,
  calculation: vehicleMonthlyCalculation,
  previousApprovedRequestId: id.nullable(),
  reviewReason: nullableText,
});
export const vehicleMonthlyHistory = z.array(vehicleMonthlyRevision);
export const vehicleMonthlyEntitlement = z.object({
  vehicleId: id,
  employeeId: id,
  monthStart: date,
  state: text,
  latestRequestId: id.nullable(),
  retainedApprovedRevision: vehicleMonthlyRevision.nullable(),
  adjustedEligibleKm: number.nullable(),
  blockCode: nullableText,
});
const incidentProposalFields = {
  outcome: number.int().min(1).max(5),
  evidenceReference: text,
  reason: text,
  proposedEmployeeLiability: number.nullable(),
  currency: nullableText,
  insuranceClaimReference: nullableText,
};
export const vehicleIncidentReview = z.object({
  ...incidentProposalFields,
  incidentId: id,
  approvalRequestId: id,
  decisionId: id,
  approvalState: text,
  policyVersionId: id,
  recordedAt: stamp,
  employeeDeductionAuthorized: z.boolean(),
  insuranceSettled: z.boolean(),
});
export const vehicleIncidentAmendment = z.object({
  requestId: id,
  revision: number.int().positive(),
  version,
  state: text,
  incidentId: id,
  previousDecisionId: id,
  policyVersionId: id,
  proposal: z.object({ ...incidentProposalFields, expectedDecisionId: id }),
  requestedByAccountId: id,
  requestedAt: stamp,
  decisionId: id.nullable(),
  reviewReason: nullableText,
});
export const vehicleIncidentHistory = z.object({
  original: vehicleIncidentReview,
  current: vehicleIncidentReview,
  amendments: z.array(vehicleIncidentAmendment),
});
export const vehicleAttributionContext = z.object({
  tripId: id,
  employeeId: id,
  tripApprovalRequestId: id,
  tripState: text,
  tripVersion: version,
  latestRequestId: id.nullable(),
});
export const vehicleSegmentRequest = z.object({
  businessDate: date,
  startedAt: stamp,
  endedAt: stamp,
  odometerStartKm: number.nonnegative(),
  odometerEndKm: number.nonnegative(),
  mapDistanceKm: number.nonnegative(),
  policyVersionId: id,
  commuteDistanceId: id.nullable(),
  evidenceReference: text.min(1).max(500),
});
export const vehicleAttribution = z.object({
  ...decisionRecord,
  tripId: id,
  tripApprovalRequestId: id,
  segments: z.array(
    z.object({
      source: vehicleSegmentRequest,
      approvedBusinessKm: number,
      excludedCommuteKm: number,
      eligibleKm: number,
      baselineKm: number.nullable(),
      rule,
      evidenceFingerprint: text,
    }),
  ),
});
export const vehicleAttributionHistory = z.array(vehicleAttribution);
export type VehiclePolicy = z.infer<typeof vehiclePolicy>;
export type VehiclePolicyVersion = z.infer<typeof vehiclePolicyVersion>;
export type VehicleMonthlyPreview = z.infer<typeof vehicleMonthlyPreview>;

export function requiredText(value: string, label: string, max = 1000) {
  const trimmed = value.trim();
  if (
    !trimmed ||
    trimmed.length > max ||
    [...trimmed].some((char) => char.charCodeAt(0) < 32 || char.charCodeAt(0) === 127)
  )
    throw new Error(
      label + ' is required and must contain ordinary text (maximum ' + max + ' characters).',
    );
  return trimmed;
}
export function requiredId(value: string, label: string) {
  const parsed = id.safeParse(value.trim());
  if (!parsed.success) throw new Error(label + ' must be a valid identifier.');
  return parsed.data;
}
export function explicitNumber(
  value: string,
  label: string,
  scale: number,
  minimum = 0,
  maximum = 999999999.999,
) {
  if (!value.trim() || !/^\d+(\.\d+)?$/.test(value.trim()))
    throw new Error(label + ' requires an explicit number.');
  const parsed = Number(value);
  if (
    !Number.isFinite(parsed) ||
    parsed < minimum ||
    parsed > maximum ||
    Number(parsed.toFixed(scale)) !== parsed
  )
    throw new Error(label + ' is outside its permitted range or precision.');
  return parsed;
}
export function inputError(error: unknown) {
  return error instanceof Error ? error.message : 'Check the entered values.';
}

// Read-like POSTs use the existing authenticated/CSRF transport, without a write receipt.
export function useVehiclePreview<T>(permission: string, path: string, schema: z.ZodType<T>) {
  const access = useFinanceAccess();
  const lifetime = useHrLifetime();
  const active = useRef(false);
  const generation = useRef(0);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [value, setValue] = useState<{ input: string; result: T } | null>(null);
  const clear = useCallback(() => {
    generation.current += 1;
    setValue(null);
    setError(null);
  }, []);
  async function run(body: unknown) {
    if (active.current || !access.can(permission)) return;
    const signal = lifetime();
    if (signal.aborted) return;
    active.current = true;
    const requestGeneration = ++generation.current;
    setPending(true);
    setError(null);
    setValue(null);
    try {
      const result = schema.parse(await request(path, { method: 'POST', body, signal }));
      if (!signal.aborted && requestGeneration === generation.current)
        setValue({ input: JSON.stringify(body), result });
    } catch (failure) {
      if (!signal.aborted && requestGeneration === generation.current)
        setError(financeError(failure));
    } finally {
      active.current = false;
      if (!signal.aborted) setPending(false);
    }
  }
  return { run, pending, error, value, clear };
}
