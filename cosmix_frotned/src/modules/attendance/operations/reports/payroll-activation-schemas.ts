import { z } from 'zod';
import { date, id, rowVersion, timestamp, timeline } from '../../schemas/common';
import { payrollRevision } from './payroll-schemas';

export const activationRequest = z.object({
  id,
  version: rowVersion,
  state: z.enum(['pending', 'approved', 'returned', 'rejected']),
  requesterAccountId: id,
  requestedAtUtc: timestamp,
  evidenceReference: z.string(),
  evidenceSha256: timeline,
  statement: z.string(),
  reason: z.string(),
  reviewerAccountId: id.nullable(),
  decidedAtUtc: timestamp.nullable(),
  decisionReason: z.string().nullable(),
});
export const activationStatus = z.object({
  binding: z.object({
    employeeId: id,
    monthStart: date,
    sourceRevisionId: id,
    sourceRevision: z.number().int().positive(),
    sourceFingerprint: timeline,
    sourceEvidenceSha256: timeline,
    calculationVersion: z.string(),
    sourceFrom: date,
    sourceTo: date,
    coverageFingerprint: timeline,
    policyVersionIds: z.array(id),
    jurisdictions: z.array(
      z.object({
        businessDate: date,
        siteId: id.nullable(),
        configurationVersionId: id.nullable(),
        jurisdictionCode: z.string().nullable(),
        state: z.string(),
      }),
    ),
  }),
  sourceCurrent: z.boolean(),
  state: z.enum(['unconfigured', 'pending', 'approved', 'returned', 'rejected', 'stale']),
  latestRequestId: id.nullable(),
  activationFingerprint: timeline.nullable(),
  requests: z.array(activationRequest),
  legalCertification: z.literal(false),
  paymentInstruction: z.literal(false),
});
export type PayrollActivationStatus = z.infer<typeof activationStatus>;
export type PayrollSourceRevision = z.infer<typeof payrollRevision>;

export function activationStatusFor(source: PayrollSourceRevision) {
  return activationStatus.refine(
    (value) =>
      value.binding.employeeId.toLowerCase() === source.employeeId.toLowerCase() &&
      value.binding.monthStart === source.monthStart &&
      value.binding.sourceRevisionId.toLowerCase() === source.id.toLowerCase() &&
      value.binding.sourceRevision === source.revision &&
      value.binding.sourceFingerprint.toLowerCase() === source.inputFingerprint.toLowerCase() &&
      value.binding.calculationVersion === source.calculationVersion &&
      new Set(value.requests.map((row) => row.id.toLowerCase())).size === value.requests.length &&
      (value.latestRequestId === null
        ? value.requests.length === 0
        : value.requests.some(
            (row) => row.id.toLowerCase() === value.latestRequestId?.toLowerCase(),
          )),
    'Activation evidence does not match this saved source revision.',
  );
}
