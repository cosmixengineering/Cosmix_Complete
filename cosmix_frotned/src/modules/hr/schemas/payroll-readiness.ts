import { z } from 'zod';

const count = z.number().int().nonnegative();
const issue = z.object({
  category: z.string(),
  code: z.string(),
  message: z.string(),
  employeeId: z.string().uuid().nullish(),
  employeeCode: z.string().nullish(),
  employeeName: z.string().nullish(),
  isBlocking: z.boolean(),
});

export const companyPayrollReadinessSchema = z
  .object({
    isReady: z.boolean(),
    evaluationPeriod: z.string().regex(/^\d{4}-\d{2}-01$/),
    evaluatedAtUtc: z.string().datetime({ offset: true }),
    totalActiveEmployees: count,
    readyEmployeesCount: count,
    blockingIssues: z.array(issue),
    warnings: z.array(issue),
    coverage: z.object({
      employeesWithSalaryCount: count,
      employeesWithShiftCount: count,
      employeesWithBankCount: count,
      employeesWithStatutoryCount: count,
      activePostingProfilesCount: count,
      activeCostCentersCount: count,
      pendingPolicyApprovalsCount: count,
    }),
  })
  .refine(
    (report) =>
      report.readyEmployeesCount <= report.totalActiveEmployees &&
      (!report.isReady || report.blockingIssues.length === 0),
    'Inconsistent payroll readiness response',
  );

export type CompanyPayrollReadinessReport = z.infer<typeof companyPayrollReadinessSchema>;

/** A cached success is not authority after a failed refresh, period change or lost access. */
export function currentReadinessReport(
  query: {
    data?: CompanyPayrollReadinessReport;
    isSuccess: boolean;
    isFetching: boolean;
    isError: boolean;
  },
  period: string,
  allowed: boolean,
): CompanyPayrollReadinessReport | null {
  return allowed &&
    query.isSuccess &&
    !query.isFetching &&
    !query.isError &&
    query.data?.evaluationPeriod === period
    ? query.data
    : null;
}
