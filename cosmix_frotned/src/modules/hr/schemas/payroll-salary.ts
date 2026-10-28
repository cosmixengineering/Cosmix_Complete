import { z } from 'zod';
import { id, date, version } from './contracts';
export const salaryRules = z.object({
  schemaVersion: z.number(),
  proration: z.number(),
  fixedDivisorDays: z.number().nullable(),
  rounding: z.number(),
  grossTreatment: z.number(),
});
export const salaryPolicy = z.object({
  id,
  revision: z.number(),
  effectiveFrom: date,
  rules: salaryRules,
  approvalRequestId: id,
  state: z.string(),
  version,
});
export const salaryComponent = z.object({
  id,
  employeeId: id,
  code: z.string(),
  kind: z.number(),
  amount: z.number(),
  currency: z.string(),
  effectiveFrom: date,
  effectiveToExclusive: date.nullable(),
  prorateWithBase: z.boolean(),
  revision: z.number(),
  supersedesId: id.nullable(),
  sourceImportId: id.nullable(),
  approvalRequestId: id,
  state: z.string(),
  version,
});
export const salaryComponentPage = z.object({
  items: z.array(salaryComponent),
  page: z.number(),
  pageSize: z.number(),
  hasMore: z.boolean(),
});
export const importInspection = z.object({
  sourceId: id,
  sourceSha256: z.string(),
  sheets: z.array(
    z.object({ name: z.string(), columns: z.array(z.string()), rowCount: z.number() }),
  ),
  supportedTargets: z.array(
    z.object({ key: z.string(), label: z.string(), required: z.boolean() }),
  ),
});
export const importPreview = z.object({
  sourceId: id,
  previewFingerprint: z.string(),
  canApply: z.boolean(),
  summary: z.object({ rowCount: z.number(), validRowCount: z.number(), errorCount: z.number() }),
  rows: z.array(
    z.object({
      rowNumber: z.number(),
      employeeId: id.nullable(),
      employeeLabel: z.string().nullable(),
      values: z.record(z.string(), z.string()),
      issues: z.array(z.object({ code: z.string(), column: z.string().nullable().optional() })),
    }),
  ),
});
export const importApplied = z.object({
  sourceId: id,
  state: z.literal('Submitted'),
  sourceSha256: z.string(),
  requests: z.array(
    z.object({ rowNumber: z.number(), employeeId: id, requestId: id, requestType: z.string() }),
  ),
});
export type SalaryPolicy = z.infer<typeof salaryPolicy>;
export type SalaryComponent = z.infer<typeof salaryComponent>;
export const importHistory = z.object({
  items: z.array(
    z.object({
      sourceId: id,
      fileName: z.string(),
      sourceSha256: z.string(),
      state: z.string(),
      uploadedAt: z.string(),
      submittedAt: z.string().nullable(),
    }),
  ),
  page: z.number(),
  pageSize: z.number(),
  hasMore: z.boolean(),
});
