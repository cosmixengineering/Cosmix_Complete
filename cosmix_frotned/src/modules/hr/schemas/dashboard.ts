import { z } from 'zod';
import { date, time } from './contracts';

const count = z.number().int().nonnegative().max(Number.MAX_SAFE_INTEGER);
const currency = z.string().min(1);
const metadata = {
  generatedAtUtc: time,
  businessDate: date,
  timeZone: z.literal('Asia/Karachi'),
};

export const dashboardAttendanceSummary = z
  .object({
    ...metadata,
    coverage: z.enum(['all_verified_active_employees', 'partial_locked_evidence_unavailable']),
    activeEmployees: count,
    unavailableSummaryEmployees: count.default(0),
    withAcceptedSessionEvidence: count,
    openSessionEmployees: count,
    closedSessionOnlyEmployees: count,
    noAcceptedSessionEvidenceEmployees: count,
    classifiedEmployees: count,
    notYetClassifiedEmployees: count,
    classifiedStatusCounts: z.record(z.string(), count),
    currentSavedSummaryEmployees: count,
    unsavedOrStaleSummaryEmployees: count,
  })
  .refine(
    (value) =>
      value.withAcceptedSessionEvidence ===
        value.openSessionEmployees + value.closedSessionOnlyEmployees &&
      value.withAcceptedSessionEvidence +
        value.noAcceptedSessionEvidenceEmployees +
        value.unavailableSummaryEmployees ===
        value.activeEmployees &&
      value.classifiedEmployees +
        value.notYetClassifiedEmployees +
        value.unavailableSummaryEmployees ===
        value.activeEmployees &&
      Object.values(value.classifiedStatusCounts).reduce((sum, item) => sum + item, 0) ===
        value.classifiedEmployees &&
      value.currentSavedSummaryEmployees +
        value.unsavedOrStaleSummaryEmployees +
        value.unavailableSummaryEmployees ===
        value.activeEmployees &&
      (value.coverage === 'all_verified_active_employees'
        ? value.unavailableSummaryEmployees === 0
        : value.unavailableSummaryEmployees > 0),
    'Attendance coverage does not match the active employee population.',
  );

export const dashboardPayrollSummary = z
  .object({
    ...metadata,
    periodStart: date,
    periodEndExclusive: date,
    activeEmployees: count,
    monthlySalaryEmployeeCount: count,
    missingSalaryEmployeeCount: count,
    nonMonthlySalaryEmployeeCount: count,
    futureSalaryVersions: count,
    pendingCompensationChangeCount: count,
    effectiveMonthlySalary: z.array(
      z.object({
        currency,
        employeeCount: count,
        baseAmount: z.number().nonnegative(),
        grossAmount: z.number().nonnegative(),
      }),
    ),
    handoff: z.array(
      z.object({
        currency,
        state: z.string().min(1),
        batchCount: count,
        lockedBatchCount: count,
        employeeCount: count,
        lineCount: count,
        signedComponentTotal: z.number(),
      }),
    ),
  })
  .refine(
    (value) =>
      value.monthlySalaryEmployeeCount +
        value.missingSalaryEmployeeCount +
        value.nonMonthlySalaryEmployeeCount ===
        value.activeEmployees &&
      value.effectiveMonthlySalary.reduce((sum, group) => sum + group.employeeCount, 0) ===
        value.monthlySalaryEmployeeCount &&
      new Set(value.effectiveMonthlySalary.map((group) => group.currency)).size ===
        value.effectiveMonthlySalary.length &&
      value.periodStart <= value.businessDate &&
      value.businessDate < value.periodEndExclusive &&
      value.handoff.every((group) => group.lockedBatchCount <= group.batchCount),
    'Salary coverage or period does not match the current business date.',
  );

export type DashboardAttendanceSummary = z.infer<typeof dashboardAttendanceSummary>;
export type DashboardPayrollSummary = z.infer<typeof dashboardPayrollSummary>;
