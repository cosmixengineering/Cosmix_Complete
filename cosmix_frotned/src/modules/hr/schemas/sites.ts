import { z } from 'zod';
import { id, date, version, pageOf } from './contracts';

const reference = id.refine(
  (value) => value !== '00000000-0000-0000-0000-000000000000',
  'Select a valid reference.',
);
const expectedVersion = z.string().regex(/^[A-Za-z0-9+/]{11}=$/, 'Reload the record version.');
const text = (max: number) => z.string().trim().min(1, 'This field is required.').max(max);
export const siteSchema = z.object({
  id,
  code: z.string(),
  name: z.string(),
  address: z.string().nullable(),
  cityId: id.nullable(),
  cityName: z.string().nullable(),
  clientName: z.string().nullable(),
  projectId: id.nullable(),
  startsOn: date.nullable(),
  endsOn: date.nullable(),
  supervisorEmployeeId: id.nullable(),
  isActive: z.boolean(),
  version,
});
export type Site = z.infer<typeof siteSchema>;
const details = {
  name: text(150),
  address: text(500),
  cityId: reference,
  clientName: z.string().trim().max(200).nullable(),
  projectId: reference.nullable(),
  startsOn: date.refine((value) => value !== '0001-01-01', 'Choose a start date.'),
  endsOn: date.nullable(),
  supervisorEmployeeId: reference.nullable(),
};
const validDates = (value: { startsOn: string; endsOn: string | null }) =>
  !value.endsOn || value.endsOn >= value.startsOn;
const dateError = { message: 'End date must be on or after start date.', path: ['endsOn'] };
export const createSiteSchema = z
  .object({
    ...details,
    code: z
      .string()
      .regex(
        /^[A-Za-z0-9][A-Za-z0-9_.-]{0,29}$/,
        'Use up to 30 letters, numbers, dots, underscores or hyphens.',
      ),
  })
  .refine(validDates, dateError);
export const updateSiteSchema = z
  .object({ ...details, expectedVersion, reason: text(500) })
  .refine(validDates, dateError);
export const siteStatusSchema = z.object({
  isActive: z.boolean(),
  expectedVersion,
  reason: text(500),
});
export const employeeAssignmentSchema = z.object({
  id,
  employeeId: id,
  departmentId: id,
  designationId: id,
  staffCategoryId: id,
  employmentTypeId: id,
  branchId: id.nullable(),
  siteId: id.nullable(),
  reportingManagerId: id.nullable(),
  effectiveFrom: date,
  effectiveTo: date.nullable(),
  version,
});
export const employeeAssignmentPageSchema = z.object({
  employeeId: id,
  version,
  assignments: pageOf(employeeAssignmentSchema),
});
export type EmployeeAssignmentPage = z.infer<typeof employeeAssignmentPageSchema>;
export const assignEmployeeSiteSchema = z.object({
  expectedVersion,
  siteId: reference,
  effectiveFrom: date.refine((value) => value !== '0001-01-01', 'Choose an effective date.'),
  reason: text(1000).refine(
    (value) => !/\p{Cc}/u.test(value),
    'Reason cannot contain control characters.',
  ),
});
