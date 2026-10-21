import { z } from 'zod';
import { date, id, reason, rowVersion, timestamp } from './common';

const precise = (places: number) =>
  z
    .number()
    .refine(
      (value) => Number(value.toFixed(places)) === value,
      `Use at most ${places} decimal places.`,
    );
const geometry = {
  latitude: precise(7).min(-90).max(90),
  longitude: precise(7).min(-180).max(180),
  radiusMeters: precise(2).positive().max(99999999.99),
  maximumAccuracyMeters: precise(2).positive().max(99999999.99),
};
export const officeJurisdiction = z.enum(['PK-PB', 'PK-SD', 'PK-KP', 'PK-BA', 'PK-ICT']);
export const officeBoundary = z.object({
  id,
  branchId: id,
  versionNumber: z.number().int().positive(),
  effectiveFrom: date,
  ...geometry,
  jurisdictionCode: officeJurisdiction,
  approvalReference: reason(500),
  recordedBy: reason(500),
  recordedAtUtc: timestamp.refine((value) => /(?:Z|\+00:00)$/.test(value)),
});
export const officeConfiguration = z
  .object({
    branchId: id,
    businessDate: date,
    status: z.enum(['configured', 'not_configured']),
    branchVersion: rowVersion,
    latestVersionNumber: z.number().int().nonnegative(),
    boundary: officeBoundary.nullable(),
  })
  .refine(
    (value) =>
      value.status === 'not_configured'
        ? value.boundary === null
        : value.boundary !== null &&
          value.boundary.branchId === value.branchId &&
          value.boundary.effectiveFrom <= value.businessDate &&
          value.boundary.versionNumber <= value.latestVersionNumber,
    'Office boundary evidence is inconsistent.',
  );
export function officeConfigurationFor(branchId: string, businessDate?: string) {
  return officeConfiguration.refine(
    (value) =>
      value.branchId === branchId && (!businessDate || value.businessDate === businessDate),
    'Unexpected office or business date.',
  );
}
export const officePublication = z.strictObject({
  effectiveFrom: date.refine((value) => value >= '1901-01-01' && value <= '9997-12-31'),
  ...geometry,
  expectedBranchVersion: rowVersion,
  expectedLatestVersionNumber: z.number().int().nonnegative(),
  managementApproved: z.literal(true),
  jurisdictionCode: officeJurisdiction,
  approvalReference: reason(500),
  reason: reason(500),
});
export function validateOfficePublication(input: unknown, today: string) {
  return officePublication
    .refine((value) => value.effectiveFrom > today, 'Publication requires a future business date.')
    .safeParse(input);
}
export function officeReceiptFor(branchId: string, input: z.infer<typeof officePublication>) {
  return officeBoundary.refine(
    (value) =>
      value.branchId === branchId &&
      value.versionNumber === input.expectedLatestVersionNumber + 1 &&
      value.effectiveFrom === input.effectiveFrom &&
      value.latitude === input.latitude &&
      value.longitude === input.longitude &&
      value.radiusMeters === input.radiusMeters &&
      value.maximumAccuracyMeters === input.maximumAccuracyMeters &&
      value.jurisdictionCode === input.jurisdictionCode &&
      value.approvalReference === input.approvalReference,
    'The publication receipt does not match the submitted office boundary.',
  );
}
