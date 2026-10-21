import type { z } from 'zod';
import type { employeeSchema, writeResult } from '../schemas/contracts';
export type EmployeeSummary = z.infer<typeof employeeSchema>;
export type EmployeeWriteResult = z.infer<typeof writeResult>;
export interface Page<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
export const onboardingLabels = [
  'Draft',
  'Pending approval',
  'Approved',
  'Returned',
  'Rejected',
] as const;
export const employmentLabels = [
  'Draft',
  'Pending approval',
  'Active',
  'Suspended',
  'Resigned',
  'Terminated',
  'Retired',
] as const;
export const approvalLabels = ['Pending', 'Approved', 'Returned', 'Rejected', 'Cancelled'] as const;
