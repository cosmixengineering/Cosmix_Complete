import { z } from 'zod';
import { date, id, timestamp, timeline, rowVersion } from '../../schemas/common';

const employeeId = id.refine((value) => value !== '00000000-0000-0000-0000-000000000000');
const count = z.number().int().nonnegative().max(Number.MAX_SAFE_INTEGER);
const daysBetween = (from: string, to: string) => (Date.parse(to) - Date.parse(from)) / 86_400_000;
export const reportSelection = z
  .object({
    employeeIds: z.array(employeeId).min(1).max(25),
    from: date,
    to: date,
  })
  .superRefine((value, context) => {
    if (new Set(value.employeeIds.map((x) => x.toLowerCase())).size !== value.employeeIds.length)
      context.addIssue({
        code: 'custom',
        path: ['employeeIds'],
        message: 'Select unique employees.',
      });
    const length = daysBetween(value.from, value.to);
    if (length < 0 || length > 30)
      context.addIssue({
        code: 'custom',
        path: ['to'],
        message: 'Choose an inclusive range of one to 31 days.',
      });
  });
export type ReportSelection = z.infer<typeof reportSelection>;
export function reportQuery(selection: ReportSelection) {
  const valid = reportSelection.parse(selection);
  const params = new URLSearchParams({ from: valid.from, to: valid.to });
  for (const value of valid.employeeIds) params.append('employeeIds', value);
  return '?' + params.toString();
}
export const reportMetadata = z.object({
  generatedAtUtc: timestamp,
  timeZone: z.literal('Asia/Karachi'),
  from: date,
  to: date,
  employeeIds: z.array(employeeId).min(1).max(25),
  lockStatus: z.enum(['all_locked', 'all_unlocked', 'mixed']),
  coverage: z.literal('explicit_selected_employees_and_dates_only'),
  policyStatus: z.literal('factual_only_no_payroll_calculation'),
  dataKind: z.literal('factual_attendance_not_payroll'),
});
export const factualRow = z
  .object({
    employeeId,
    businessDate: date,
    factualStatus: z.enum([
      'closed_session_evidence',
      'open_session',
      'no_accepted_session_evidence',
    ]),
    workedSeconds: count,
    workedMinutes: count,
    openSessions: count,
    sessionCount: count,
    exceptionCount: count,
    isLocked: z.boolean(),
    summaryId: id.nullable(),
    summaryRevision: z.number().int().positive().nullable(),
    inputFingerprint: timeline,
  })
  .refine(
    (row) =>
      row.workedMinutes === Math.floor(row.workedSeconds / 60) &&
      row.openSessions <= row.sessionCount &&
      (row.summaryId === null ? row.summaryRevision === null : row.summaryRevision !== null) &&
      (row.factualStatus === 'open_session'
        ? row.openSessions > 0
        : row.factualStatus === 'closed_session_evidence'
          ? row.sessionCount > 0 && row.openSessions === 0
          : row.sessionCount === 0 && row.workedSeconds === 0),
    'Invalid factual source row.',
  );
function sameSelection(metadata: z.infer<typeof reportMetadata>, selection: ReportSelection) {
  return (
    metadata.from === selection.from &&
    metadata.to === selection.to &&
    metadata.employeeIds
      .map((x) => x.toLowerCase())
      .sort()
      .join() ===
      selection.employeeIds
        .map((x) => x.toLowerCase())
        .sort()
        .join()
  );
}
export function factualBatchFor(selection: ReportSelection) {
  return z
    .object({ metadata: reportMetadata, rows: z.array(factualRow).max(775) })
    .refine(
      (batch) =>
        sameSelection(batch.metadata, selection) &&
        batch.rows.length ===
          selection.employeeIds.length * (daysBetween(selection.from, selection.to) + 1) &&
        new Set(batch.rows.map((row) => row.employeeId.toLowerCase() + ':' + row.businessDate))
          .size === batch.rows.length &&
        batch.rows.every(
          (row) =>
            selection.employeeIds.some((x) => x.toLowerCase() === row.employeeId.toLowerCase()) &&
            row.businessDate >= selection.from &&
            row.businessDate <= selection.to,
        ) &&
        batch.metadata.lockStatus ===
          (batch.rows.every((row) => row.isLocked)
            ? 'all_locked'
            : batch.rows.every((row) => !row.isLocked)
              ? 'all_unlocked'
              : 'mixed'),
      'Report does not match the selected employee and date matrix.',
    );
}
export const factualTotals = z
  .object({
    employeeDays: count,
    closedSessionEvidenceDays: count,
    openSessionDays: count,
    noAcceptedSessionEvidenceDays: count,
    workedSeconds: count,
    sumDailyWorkedMinutes: count,
    sessions: count,
    closedSessions: count,
    openSessions: count,
    exceptionCount: count,
    daysWithExceptions: count,
    lockedDays: count,
    unlockedDays: count,
    currentSavedSummaryDays: count,
    unsavedOrStaleSummaryDays: count,
  })
  .refine(
    (totals) =>
      totals.closedSessionEvidenceDays +
        totals.openSessionDays +
        totals.noAcceptedSessionEvidenceDays ===
        totals.employeeDays &&
      totals.closedSessions + totals.openSessions === totals.sessions &&
      totals.lockedDays + totals.unlockedDays === totals.employeeDays &&
      totals.currentSavedSummaryDays + totals.unsavedOrStaleSummaryDays === totals.employeeDays,
    'Inconsistent report totals.',
  );
export function dashboardFor(selection: ReportSelection) {
  return z
    .object({
      metadata: reportMetadata,
      totals: factualTotals,
      employees: z.array(z.object({ employeeId, totals: factualTotals })).max(25),
    })
    .refine(
      (result) =>
        sameSelection(result.metadata, selection) &&
        result.totals.employeeDays ===
          selection.employeeIds.length * (daysBetween(selection.from, selection.to) + 1) &&
        result.employees.length === selection.employeeIds.length &&
        new Set(result.employees.map((row) => row.employeeId.toLowerCase())).size ===
          result.employees.length &&
        result.employees.every(
          (row) =>
            selection.employeeIds.some((x) => x.toLowerCase() === row.employeeId.toLowerCase()) &&
            row.totals.employeeDays === daysBetween(selection.from, selection.to) + 1,
        ),
      'Totals do not match the selected employees.',
    );
}
export const exportJob = z
  .object({
    id,
    state: z.enum(['pending', 'running', 'completed', 'failed']),
    format: z.union([z.literal(1), z.literal(2), z.literal(3)]),
    from: date,
    to: date,
    requestedAt: timestamp,
    generatedAt: timestamp.nullable(),
    downloadExpiresAt: timestamp.nullable(),
    failureCode: z.string().nullable(),
    dataKind: z.literal('factual_attendance_not_payroll'),
  })
  .refine(
    (value) =>
      value.state !== 'completed' ||
      (value.generatedAt !== null && value.downloadExpiresAt !== null),
    'Completed report has no retained generation evidence.',
  );
export type ExportJob = z.infer<typeof exportJob>;
export function exportJobFor(jobId: string) {
  return exportJob.refine(
    (value) => value.id.toLowerCase() === jobId.toLowerCase(),
    'Export job does not match this request.',
  );
}
export const monthPreview = z
  .object({
    monthStart: date,
    monthEndExclusive: date,
    periodId: id.nullable(),
    version: rowVersion.nullable(),
    state: z.enum(['ready', 'not_ready', 'locked']),
    ready: z.boolean(),
    previewFingerprint: timeline,
    employeeCount: count,
    requiredDayCount: count,
    readySummaryCount: count,
    carryForwardCount: count,
    issueCount: count,
    issues: z.array(
      z.object({ code: z.string(), employeeId: id.nullable(), businessDate: date.nullable() }),
    ),
  })
  .refine(
    (value) =>
      value.issueCount >= value.issues.length &&
      (!value.ready ||
        (value.state === 'ready' &&
          value.issueCount === 0 &&
          value.requiredDayCount > 0 &&
          value.readySummaryCount === value.requiredDayCount)),
    'Blocked or incomplete month cannot be finalized.',
  );
export const monthLock = z.object({
  periodId: id,
  eventId: id,
  revision: z.number().int().positive(),
  monthStart: date,
  monthEndExclusive: date,
  state: z.string(),
  version: rowVersion,
  lockedAtUtc: timestamp,
  actor: z.string(),
  reason: z.string(),
  provenance: z.string(),
  summaryCount: count,
  carryForwardCount: count,
});
export const monthDetail = z.object({
  lock: monthLock,
  summaries: z.array(
    z.object({
      employeeId: id,
      businessDate: date,
      summaryId: id,
      inputFingerprint: timeline,
      policyVersionId: id,
      calculationVersion: z.string(),
    }),
  ),
  carryForwards: z.array(
    z.object({
      adjustmentId: id,
      employeeId: id,
      sourceBusinessDate: date,
      revision: count,
      targetMonthStart: date,
      targetMonthEndExclusive: date,
      deltaWorkedTicks: z.number().int(),
      deltaWorkedMinutes: z.number().int(),
      inputFingerprint: timeline,
      interpretationFingerprint: timeline,
    }),
  ),
  payrollInputs: z
    .array(
      z.object({
        employeeId: id,
        monthStart: date,
        sourceRevisionId: id,
        sourceRevision: z.number().int().positive(),
        inputFingerprint: timeline,
        calculationVersion: z.string(),
        sourceEvidenceSha256: timeline,
        handoffId: id.nullable(),
        handoffEvidenceFingerprint: timeline.nullable(),
        legalActivationFingerprint: timeline.nullable(),
      }),
    )
    .nullable()
    .optional(),
});

export function matchesMonth(
  value: { monthStart: string; monthEndExclusive: string },
  period: string,
) {
  const year = Number(period.slice(0, 4));
  const month = Number(period.slice(5));
  const end = `${month === 12 ? year + 1 : year}-${String(month === 12 ? 1 : month + 1).padStart(2, '0')}-01`;
  return value.monthStart === period + '-01' && value.monthEndExclusive === end;
}
export function monthPreviewFor(period: string) {
  return monthPreview.refine(
    (value) => matchesMonth(value, period),
    'Preview does not match the selected month.',
  );
}
export function monthLockFor(period: string) {
  return monthLock.refine(
    (value) => matchesMonth(value, period),
    'Lock does not match the selected month.',
  );
}
export function monthDetailFor(period: string) {
  return monthDetail.refine(
    (value) =>
      matchesMonth(value.lock, period) &&
      value.lock.summaryCount === value.summaries.length &&
      value.lock.carryForwardCount === value.carryForwards.length &&
      new Set(value.summaries.map((row) => row.summaryId.toLowerCase())).size ===
        value.summaries.length &&
      value.summaries.every(
        (row) =>
          row.businessDate >= value.lock.monthStart &&
          row.businessDate < value.lock.monthEndExclusive,
      ) &&
      value.carryForwards.every(
        (row) =>
          row.targetMonthStart === value.lock.monthStart &&
          row.targetMonthEndExclusive === value.lock.monthEndExclusive,
      ) &&
      (value.payrollInputs ?? []).every((row) => row.monthStart === value.lock.monthStart),
    'Lock membership does not match the retained month.',
  );
}
