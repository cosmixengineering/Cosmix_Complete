import { z } from 'zod';
import { date, id, time, version } from './contracts';
export const termsSchema = z.object({
  baseAmount: z.number(),
  grossAmount: z.number(),
  currency: z.string(),
  wageBasis: z.number().int().min(1).max(3),
  paymentMethod: z.number().int().min(1).max(4),
});
export const compensationEntrySchema = z.object({
  id,
  revision: z.number(),
  supersedesCompensationRecordId: id.nullable(),
  terms: termsSchema,
  effectiveFrom: date,
  effectiveTo: date.nullable(),
  recordedEffectiveTo: date.nullable(),
  approvalRequestId: id.nullable(),
  recordedByUserId: z.string(),
  recordedAt: time,
});
export const compensationStateSchema = z.object({
  employeeId: id,
  employeeVersion: version,
  asOf: date,
  effective: compensationEntrySchema.nullable(),
  nextScheduled: compensationEntrySchema.nullable(),
  latestRecordId: id.nullable(),
});
export const workflowRequestSchema = z.object({
  id,
  employeeId: id,
  state: z.number().int().min(0).max(4),
  requesterAccountId: z.string(),
  requestedAt: time,
  closedAt: time.nullable(),
  version,
});
const decisions = z.array(
  z.object({
    id,
    decision: z.number(),
    actorAccountId: z.string(),
    actorRoleCode: z.string(),
    reason: z.string().nullable(),
    decidedAt: time,
  }),
);
export const salaryRequestDetailSchema = z.object({
  request: workflowRequestSchema,
  before: compensationEntrySchema.nullable(),
  proposed: termsSchema,
  effectiveFrom: date,
  reason: z.string(),
  decisions,
});
export const statusHistorySchema = z.object({
  id,
  previousStatus: z.number(),
  newStatus: z.number(),
  effectiveAt: time,
  reason: z.string(),
  actorAccountId: z.string(),
  recordedAt: time,
  approvalRequestId: id.nullable(),
  lifecyclePolicyVersion: z.string().nullable().optional(),
});
export const statusStateSchema = z.object({
  employeeId: id,
  version,
  currentStatus: z.number(),
  currentStatusChangeId: id.nullable(),
  scheduledChange: statusHistorySchema.nullable(),
  isArchived: z.boolean(),
  archivedAt: time.nullable(),
});
export const statusRequestDetailSchema = z.object({
  request: workflowRequestSchema,
  previousStatus: z.number(),
  newStatus: z.number(),
  requestedEffectiveAt: time.nullable(),
  reason: z.string(),
  decisions,
  lifecyclePolicyVersion: z.string().nullable().optional(),
});
const lifecycleReasonSchema = z
  .string()
  .max(1000)
  .refine(
    (value) =>
      ![...value].some((character) => {
        const code = character.charCodeAt(0);
        return code < 32 || (code >= 127 && code <= 159);
      }),
    'Use a reason without control characters.',
  )
  .trim()
  .min(1);
export const statusRequestInputSchema = z.strictObject({
  expectedVersion: version.length(12),
  newStatus: z.union([z.literal(2), z.literal(3), z.literal(4), z.literal(5), z.literal(6)]),
  effectiveAt: time.nullable(),
  reason: lifecycleReasonSchema,
});
export const archiveInputSchema = z.strictObject({
  archived: z.boolean(),
  expectedVersion: version.length(12),
  reason: lifecycleReasonSchema,
});
export const archiveHistorySchema = z.object({
  id,
  previousArchived: z.boolean(),
  isArchived: z.boolean(),
  employmentStatus: z.number().int().min(0).max(6),
  reason: z.string(),
  actorAccountId: z.string(),
  actorRoleCode: z.string(),
  recordedAt: time,
});
