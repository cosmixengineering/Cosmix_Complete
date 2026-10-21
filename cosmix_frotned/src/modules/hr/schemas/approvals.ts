import { z } from 'zod';
import { id, time, version } from './contracts';
import { documentsSchema, profileSchema } from './hiring';

export const submissionSummarySchema = z.object({
  id,
  employeeId: id,
  employeeNumber: z.number().int(),
  name: z.string().nullable(),
  version: z.number().int().positive(),
  submittedBy: id.nullable(),
  submittedAt: time,
  status: z.number().int().min(0).max(4),
});
export type SubmissionSummary = z.infer<typeof submissionSummarySchema>;
export const submissionProfileSchema = z.object({
  submissionId: id,
  employeeId: id,
  version: z.number().int().positive(),
  profile: profileSchema,
  documents: documentsSchema,
});
export const accessSetupSchema = z.object({
  employeeId: id,
  account: z
    .object({
      id,
      employeeId: id,
      status: z.number().int().min(0).max(5),
      mobileAccessEnabled: z.boolean(),
      webAccessEnabled: z.boolean(),
      mfaRequired: z.boolean(),
      version,
    })
    .nullable(),
  mobileAccessRequested: z.boolean(),
  initialAccessAssigned: z.boolean(),
  mobilePhoneEnrolled: z.boolean(),
});
