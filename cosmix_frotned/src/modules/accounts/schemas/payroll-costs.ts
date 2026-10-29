import { z } from 'zod';

const id = z.string().uuid();
const cost = z.object({
  employeeId: id,
  projectId: id.nullable(),
  siteId: id.nullable(),
  basisPoints: z.number().int(),
  amount: z.number().finite(),
  currency: z.string(),
});
export const payrollCostPlanSchema = z.object({
  id,
  batchId: id,
  revision: z.number().int(),
  state: z.enum(['Draft', 'Approved', 'Rejected']),
  version: z.string(),
  createdBy: z.string(),
  createdByEmployeeId: id,
  decidedBy: z.string().nullable(),
  createdAt: z.string(),
  decidedAt: z.string().nullable(),
  costs: z.array(cost),
  reason: z.string(),
});
const target = z.object({ id, code: z.string(), name: z.string(), projectId: id.nullable() });
export const payrollCostWorkspaceSchema = z.object({
  batchId: id,
  sourceFingerprint: z.string(),
  employees: z.array(
    z.object({
      employeeId: id,
      employeeName: z.string().nullable().optional(),
      amount: z.number().finite(),
      currency: z.string(),
    }),
  ),
  targets: z.object({ projects: z.array(target), sites: z.array(target) }),
  plans: z.array(payrollCostPlanSchema),
  effectivePlanId: id.nullable(),
});
export type PayrollCostWorkspace = z.infer<typeof payrollCostWorkspaceSchema>;
