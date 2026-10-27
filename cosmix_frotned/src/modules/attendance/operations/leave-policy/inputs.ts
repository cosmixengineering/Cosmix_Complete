import type { SessionUser } from '@/auth/types';
import { date } from '../../schemas/common';
import { leaveDayInput, policyRules, type PolicyRules } from './contracts';

export function canAll(user: SessionUser | null, permission: string) {
  return Boolean(
    user?.account &&
    user.grants.some(
      (g) => g.permission === permission && g.scopeKind === 7 && g.targetId === null,
    ),
  );
}
export function canSelfLeave(user: SessionUser | null) {
  const employee = user?.account?.employeeId;
  return Boolean(
    employee &&
    user?.grants.some(
      (g) =>
        g.permission === 'attendance.leaves.self' &&
        (g.scopeKind === 1 || g.scopeKind === 2) &&
        g.targetId === employee,
    ),
  );
}
export function leaveDays(rows: readonly { start: string; end: string }[]) {
  if (rows.length < 1 || rows.length > 366) throw new Error('Enter 1 through 366 exact intervals.');
  const days = rows
    .map((row) => {
      if (
        !/^\d{4}-\d{2}-\d{2}T(?:[01]\d|2[0-3]):[0-5]\d$/.test(row.start) ||
        !/^\d{4}-\d{2}-\d{2}T(?:[01]\d|2[0-3]):[0-5]\d$/.test(row.end)
      )
        throw new Error('Enter whole-minute Pakistan start and end times.');
      const businessDate = date.parse(row.start.slice(0, 10));
      date.parse(row.end.slice(0, 10));
      if (businessDate < '1901-01-01' || businessDate > '9997-12-31')
        throw new Error('Use a supported business date from 1901 through 9997.');
      const start = new Date(row.start + ':00+05:00');
      const end = new Date(row.end + ':00+05:00');
      if (
        !Number.isFinite(start.getTime()) ||
        !Number.isFinite(end.getTime()) ||
        end <= start ||
        end.getTime() - start.getTime() > 86400000
      )
        throw new Error('Each end must follow its start by at most 24 hours.');
      return leaveDayInput.parse({
        date: businessDate,
        startsAtUtc: start.toISOString(),
        endsAtUtc: end.toISOString(),
      });
    })
    .sort((a, b) => a.date.localeCompare(b.date));
  if (
    new Set(days.map((d) => d.date)).size !== days.length ||
    days.some((d, i) => i > 0 && d.startsAtUtc < days[i - 1]!.endsAtUtc)
  )
    throw new Error('Use distinct business dates and non-overlapping intervals.');
  if (Date.parse(days.at(-1)!.date) - Date.parse(days[0]!.date) > 365 * 86400000)
    throw new Error('Intervals must fit within 366 dates.');
  return days;
}
export function localMinute(utc: string) {
  return new Date(new Date(utc).getTime() + 5 * 3600000).toISOString().slice(0, 16);
}
export function blankRules(): PolicyRules {
  const shape = policyRules.shape;
  const empty = (group: { shape: Record<string, unknown> }) =>
    Object.fromEntries(Object.keys(group.shape).map((key) => [key, null]));
  return policyRules.parse({
    schemaVersion: 3,
    arrival: empty(shape.arrival),
    paidTime: empty(shape.paidTime),
    lateDeduction: empty(shape.lateDeduction),
    ordinaryPay: empty(shape.ordinaryPay),
  });
}
export function normalizedRules(input: PolicyRules): PolicyRules {
  const rules = policyRules.parse(input);
  const late = rules.lateDeduction;
  if (late.salaryDivisor !== 'fixed_days') late.fixedSalaryDivisorDays = null;
  if (late.deductionKind !== 'day_fraction') late.deductionDaysPerThreshold = null;
  if (late.deductionKind !== 'fixed_amount') late.fixedAmountPkr = null;
  if (late.deductionKind !== 'hours') late.deductionHoursPerThreshold = null;
  if (late.resetPeriod !== 'weekly') late.weekStartsOn = null;
  if (rules.ordinaryPay.salaryDivisor !== 'fixed_days')
    rules.ordinaryPay.fixedSalaryDivisorDays = null;
  if (rules.overtime == null) delete rules.overtime;
  else if (rules.overtime.salaryDivisor !== 'fixed_days')
    rules.overtime.fixedSalaryDivisorDays = null;
  return rules;
}
export const stateNames = ['Pending', 'Approved', 'Returned', 'Rejected', 'Cancelled'];
export const kindNames = [
  'Other',
  'Sick',
  'Casual',
  'Exam',
  'Emergency',
  'Site injury rest',
  'Personal travel',
];
export const evidenceNames = [
  'Medical',
  'Admit card',
  'Date sheet',
  'Emergency',
  'Injury medical',
  'Other',
];
export const treatmentNames = ['Unresolved', 'Paid', 'Unpaid', 'Does not reduce regular leave'];
export function human(value: string) {
  return value
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replaceAll('_', ' ')
    .replace(/^./, (c) => c.toUpperCase());
}
