import { z } from 'zod';

export const reconciliationRoot = '/hr/payroll/reconciliation';
export const reconciliationPermissions = {
  read: 'attendance.payroll-salary.read',
  initiate: 'attendance.payroll-salary.request',
  historical: 'attendance.payroll-salary.review',
  hr: 'attendance.payroll-salary.review',
  finance: 'payroll.accounting.configure.approve',
  management: 'attendance.payroll-handoff.lock',
} as const;
export const reconciliationColumns = [
  'EmployeeCode',
  'BasicSalary',
  'Allowances',
  'Overtime',
  'GrossSalary',
  'Deductions',
  'Eobi',
  'Sessi',
  'IncomeTax',
  'NetSalary',
] as const;
const id = z.uuid();
const money = z.number().finite();
const count = z.number().int().nonnegative();
const fingerprint = z.string().regex(/^[a-f0-9]{64}$/i);
const stamp = z.iso.datetime({ offset: true });
const notes = z
  .string()
  .trim()
  .min(1)
  .max(1000)
  .refine((value) => !/\p{Cc}/u.test(value));
export const comparisonReport = z.object({
  erpBatchId: id,
  sourceFingerprint: fingerprint,
  reportId: id,
  sourceFileName: z.string(),
  sourceSha256: fingerprint,
  targetYear: z.number().int().min(2020).max(2035),
  targetMonth: z.number().int().min(1).max(12),
  generatedAt: stamp,
  generatedByUserId: z.string(),
  auditReason: z.string(),
  summary: z.object({
    totalEmployeesInSheet: count,
    matchedEmployeesCount: count,
    missingInErpCount: count,
    missingInSheetCount: count,
    duplicateInSheetCount: count,
    exactMatchCount: count,
    roundingDifferenceCount: count,
    varianceCount: count,
    totalSheetGross: money,
    totalErpGross: money,
    totalGrossVariance: money,
    totalSheetNet: money,
    totalErpNet: money,
    totalNetVariance: money,
  }),
  variances: z.array(
    z.object({
      employeeId: id.nullable(),
      employeeCode: z.string(),
      employeeName: z.string(),
      sheetBasicSalary: money,
      erpBasicSalary: money,
      basicVariance: money,
      sheetAllowances: money,
      erpAllowances: money,
      allowanceVariance: money,
      sheetOvertime: money,
      erpOvertime: money,
      overtimeVariance: money,
      sheetGrossSalary: money,
      erpGrossSalary: money,
      grossVariance: money,
      sheetDeductions: money,
      erpDeductions: money,
      deductionVariance: money,
      sheetStatutory: money,
      erpStatutory: money,
      statutoryVariance: money,
      sheetEobi: money,
      erpEobi: money,
      sheetSessi: money,
      erpSessi: money,
      sheetIncomeTax: money,
      erpIncomeTax: money,
      sheetNetSalary: money,
      erpNetSalary: money,
      netVariance: money,
      classification: z.number().int().min(1).max(6),
      details: z.string(),
    }),
  ),
});
export const varianceExplanation = z.object({
  employeeId: id.nullable(),
  employeeCode: z.string(),
  component: z.string(),
  erpAmount: money,
  legacyAmount: money,
  variance: money,
  rootCause: z.number().int().min(1).max(9),
  justification: z.string().max(1000),
});
export const parallelRun = z
  .object({
    id,
    year: z.number().int().min(2020).max(2035),
    month: z.number().int().min(1).max(12),
    state: z.number().int().min(1).max(6),
    version: z.number().int().positive(),
    preparedByUserId: z.string(),
    preparedAt: stamp,
    hrReviewerUserId: z.string().nullable(),
    hrReviewedAt: stamp.nullable(),
    hrNotes: z.string().nullable(),
    financeReviewerUserId: z.string().nullable(),
    financeReviewedAt: stamp.nullable(),
    financeNotes: z.string().nullable(),
    managementApproverUserId: z.string().nullable(),
    managementApprovedAt: stamp.nullable(),
    managementNotes: z.string().nullable(),
    approvalFingerprint: fingerprint.nullable(),
    erpBatchId: id,
    sourceFingerprint: fingerprint,
    sourceSha256: fingerprint,
    report: comparisonReport,
    preparedByEmployeeId: id,
    hrReviewerEmployeeId: id.nullable(),
    financeReviewerEmployeeId: id.nullable(),
    managementApproverEmployeeId: id.nullable(),
    financeFingerprint: fingerprint.nullable(),
    summary: z.object({
      totalEmployees: count,
      matchedEmployees: count,
      discrepancyCount: count,
      erpTotalGross: money,
      legacyTotalGross: money,
      grossVariance: money,
      erpTotalNet: money,
      legacyTotalNet: money,
      netVariance: money,
      allVariancesExplained: z.boolean(),
    }),
    variances: z.array(varianceExplanation),
  })
  .refine(
    (run) =>
      run.report.targetYear === run.year &&
      run.report.targetMonth === run.month &&
      run.report.erpBatchId === run.erpBatchId &&
      run.report.sourceFingerprint.toLowerCase() === run.sourceFingerprint.toLowerCase() &&
      run.report.sourceSha256.toLowerCase() === run.sourceSha256.toLowerCase(),
    'The retained comparison does not match its payroll source.',
  );
export const parallelRunPage = z.object({
  items: z.array(parallelRun),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  hasMore: z.boolean(),
});
export const parallelRunHistory = z.array(parallelRun);
export const parallelRunFor = (runId: string) =>
  parallelRun.refine((run) => run.id === runId, 'Wrong payroll run returned.');
export type ParallelRun = z.infer<typeof parallelRun>;
export type ComparisonReport = z.infer<typeof comparisonReport>;

export const reconciliationUpload = z
  .object({
    year: z.number().int().min(2020).max(2035),
    month: z.number().int().min(1).max(12),
    erpBatchId: id,
    reason: notes,
    fileName: z
      .string()
      .min(1)
      .max(150)
      .regex(/\.(csv|xlsx)$/i),
    contentType: z.enum([
      'text/csv',
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    ]),
    base64: z.string().min(1).max(5592410),
    columnMapping: z.record(z.enum(reconciliationColumns), z.string().trim().min(1)),
  })
  .refine(
    (value) => new Set(Object.values(value.columnMapping)).size === reconciliationColumns.length,
    'Map every field to a distinct source column.',
  );

export function reviewExplanations(
  run: ParallelRun,
  edits: { rootCause: number; justification: string }[],
) {
  if (edits.length !== run.variances.length) throw new Error('Review every variance.');
  // Preserve server-owned employee, component and amount fields exactly.
  return run.variances.map((row, index) => {
    z.number().int().min(1).max(8).parse(edits[index]!.rootCause);
    if (row.variance !== 0 && edits[index]!.rootCause === 1)
      throw new Error('A variance cannot be classified as an exact match.');
    return varianceExplanation.parse({
      ...row,
      rootCause: edits[index]!.rootCause,
      justification: notes.parse(edits[index]!.justification),
    });
  });
}
