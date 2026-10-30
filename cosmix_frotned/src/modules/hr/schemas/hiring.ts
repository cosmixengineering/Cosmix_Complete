import { z } from 'zod';
import { date, employeeSchema, id, time, version } from './contracts';
const text = (length: number) => z.string().max(length).nullable();
const day = date.nullable();
const reference = id.nullable();
export const addressSchema = z.object({
  line1: text(250),
  line2: text(250),
  city: text(100),
  cityId: reference,
  province: text(100),
  postalCode: text(20),
});
export const emergencySchema = z.object({
  name: text(200),
  relationship: text(100),
  phone: text(16),
  alternatePhone: text(16),
});
export const previousEmploymentSchema = z.object({
  entryId: id,
  employerName: text(200),
  roleTitle: text(150),
  startedOn: day,
  endedOn: day,
  leavingReason: text(1000),
});
export const certificationSchema = z.object({
  documentId: reference,
  type: text(100),
  number: text(100),
  issuer: text(200),
  issuedOn: day,
  expiresOn: day,
});
export const assetSchema = z.object({
  assetReference: text(100),
  issueCondition: text(1000),
  returnCondition: text(1000),
  issuedOn: day,
  returnedOn: day,
  returnedToAccountId: reference,
  evidenceDocumentId: reference,
});
export const profileSchema = z.object({
  fullLegalName: text(200),
  fatherOrSpouseName: text(200),
  identityDocumentType: z.union([z.literal(1), z.literal(2)]).nullable(),
  identityDocumentNumber: text(50),
  identityDocumentExpiresOn: day,
  dateOfBirth: day,
  gender: text(40),
  maritalStatus: text(40),
  personalPhone: text(16),
  companyPhone: text(16),
  simIssuedOn: day,
  simStatus: text(40),
  simCreditTier: text(50),
  personalEmail: text(320),
  officialEmail: text(320),
  joinedOn: day,
  probationEndsOn: day,
  departmentId: reference,
  designationId: reference,
  staffCategoryId: reference,
  employmentTypeId: reference,
  branchId: reference,
  reportingManagerEmployeeId: reference,
  proposedShiftId: reference,
  loginPhoneNumber: text(16),
  mobileAccessRequested: z.boolean(),
  currentAddress: addressSchema.nullable(),
  permanentAddress: addressSchema.nullable(),
  emergencyContact: emergencySchema.nullable(),
  documentIds: z.array(id).max(100),
  previousEmployment: z.array(previousEmploymentSchema).max(100),
  certifications: z.array(certificationSchema).max(100),
  assets: z.array(assetSchema).max(100),
});
export const profileResponse = z.object({ employee: employeeSchema, profile: profileSchema });
export const bankSchema = z.object({
  bankName: text(200),
  accountTitle: text(200),
  accountNumber: text(100),
  iban: text(34),
});
export const financialSchema = z.object({
  baseSalary: z.number().nullable(),
  grossSalary: z.number().nullable(),
  currency: text(3),
  wageBasis: z.number().int().min(1).max(3).nullable(),
  paymentMethod: z.number().int().min(1).max(4).nullable(),
  effectiveFrom: day,
  bank: bankSchema.nullable(),
  previousSalaries: z
    .array(z.object({ employmentEntryId: id, amount: z.number().nullable(), currency: text(3) }))
    .max(100),
});
export const medicalSchema = z.object({
  details: text(10000),
  authorizationReference: text(500),
  supportingDocumentId: reference,
});
export const sectionResponse = <T extends z.ZodType>(schema: T) =>
  z.object({ employeeId: id, version, data: schema.nullable() });
export const documentSchema = z.object({
  id,
  type: z.number().int().min(1).max(10),
  fileName: z.string(),
  contentType: z.string(),
  sizeBytes: z.number(),
  issuedOn: day,
  expiresOn: day,
  scanStatus: z.number().int().min(0).max(3),
  verificationStatus: z.number().int().min(0).max(2),
  isAvailable: z.boolean(),
  version,
  privacy: z.number().int().min(0).max(1),
  lifecycleStatus: z.number().int().min(0).max(3).optional().default(0),
  isScanInProgress: z.boolean().optional().default(false),
  historyRevision: z.number().int().min(0).optional().default(0),
});
export const documentsSchema = z.array(documentSchema);
export const readinessSchema = z.object({
  canSubmit: z.boolean(),
  missingFields: z.array(z.string()),
  unavailableDocumentIds: z.array(id),
});
export const hiringDecisionsSchema = z.array(
  z.object({
    id,
    submissionId: id,
    decision: z.number(),
    actorAccountId: reference,
    actorRoleId: reference,
    reason: z.string().nullable(),
    decidedAt: time,
  }),
);

export type Profile = z.infer<typeof profileSchema>;
export type FinancialSection = z.infer<typeof financialSchema>;
export type MedicalSection = z.infer<typeof medicalSchema>;
