import { z } from 'zod';
import { id, date, time, version } from './contracts';
import { addressSchema, emergencySchema, bankSchema, medicalSchema } from './hiring';
export const basicSchema = z.object({
  currentAddress: addressSchema,
  permanentAddress: addressSchema,
  emergencyContact: emergencySchema,
  personalEmail: z.string().nullable(),
  maritalStatus: z.string().nullable(),
});
export const identitySchema = z.object({
  fullLegalName: z.string(),
  fatherOrSpouseName: z.string().nullable(),
  identityDocumentType: z.number(),
  identityDocumentNumber: z.string(),
  identityDocumentExpiresOn: date.nullable(),
  dateOfBirth: date,
  gender: z.string().nullable(),
  personalPhone: z.string(),
  companyPhone: z.string().nullable(),
  officialEmail: z.string().nullable(),
  identityDocumentId: id,
});
export const employmentSchema = z.object({
  departmentId: id,
  designationId: id,
  reportingManagerEmployeeId: id.nullable(),
  branchId: id.nullable(),
});
export const updateDataSchema = z.object({
  basic: basicSchema.nullish(),
  identity: identitySchema.nullish(),
  employment: employmentSchema.nullish(),
  bank: bankSchema.nullish(),
  medical: medicalSchema.nullish(),
  documents: z.object({ documentIds: z.array(id) }).nullish(),
});
export const updateValueSchema = z.object({
  recordId: id.nullable(),
  effectiveFrom: date,
  effectiveTo: date.nullable(),
  data: updateDataSchema,
  bankVerified: z.boolean().nullable(),
});
export const currentUpdateSchema = z.object({
  employeeId: id,
  version,
  section: z.number(),
  asOf: date,
  current: updateValueSchema,
  scheduled: updateValueSchema.nullable(),
  evidenceIssue: z.string().nullish(),
});
export const changeSummarySchema = z.object({
  id,
  employeeId: id,
  section: z.number(),
  effectiveFrom: date,
  approvalRequestId: id.nullable(),
  actorId: z.string(),
  recordedAt: time,
});
export const changeHistorySchema = z.object({
  change: changeSummarySchema,
  before: updateValueSchema,
  after: updateValueSchema,
  reason: z.string(),
});
export const changeRequestSummarySchema = z.object({
  id,
  employeeId: id,
  state: z.number().int().min(0).max(4),
  requesterId: z.string(),
  requestedAt: time,
  closedAt: time.nullable(),
  version,
});
export const changeRequestSchema = z.object({
  request: changeRequestSummarySchema,
  section: z.number(),
  before: updateValueSchema,
  proposed: updateDataSchema,
  repairsIdentityEvidence: z.boolean().optional(),
  effectiveFrom: date.nullable(),
  reason: z.string(),
  decisions: z.array(
    z.object({
      id,
      decision: z.number(),
      actorId: z.string(),
      roleCode: z.string(),
      reason: z.string().nullable(),
      decidedAt: time,
    }),
  ),
});
