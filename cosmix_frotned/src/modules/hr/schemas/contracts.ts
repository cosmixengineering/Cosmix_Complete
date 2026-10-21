import { z } from 'zod';

export const id = z.uuid();
export const date = z.iso.date();
export const time = z.iso.datetime({ offset: true });
export const version = z.string().min(1);
export const pageOf = <T extends z.ZodType>(item: T) =>
  z.object({
    items: z.array(item),
    totalCount: z.number().int().nonnegative(),
    page: z.number().int().positive(),
    pageSize: z.number().int().positive(),
  });
export const employeeSchema = z.object({
  id,
  employeeNumber: z.number().int(),
  name: z.string().nullable(),
  status: z.number().int().min(0).max(6),
  onboardingStatus: z.number().int().min(0).max(4),
  version,
  phone: z.string().nullable().optional(),
  email: z.string().nullable().optional(),
  joinedOn: z.string().nullable().optional(),
  gender: z.string().nullable().optional(),
  identityNumber: z.string().nullable().optional(),
  createdAt: z.string().nullable().optional(),
  mobileAccessRequested: z.boolean().nullable().optional(),
});
export const employeePage = pageOf(employeeSchema);
export const writeResult = z.object({
  employeeId: id,
  version,
  status: z.number(),
  documentId: id.nullable().optional(),
  submissionId: id.nullable().optional(),
  changeRequestId: id.nullable().optional(),
  compensationRequestId: id.nullable().optional(),
  lifecycleRequestId: id.nullable().optional(),
});
export type EmployeeSummary = z.infer<typeof employeeSchema>;
