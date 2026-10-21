import { z } from 'zod';
import { id, reason } from '../../schemas/common';
import { decisionInput, overtimeInput, overtimeSource } from './contracts';
import { overtimeReady } from './helpers';

export function reviewedOvertime(
  source: z.infer<typeof overtimeSource>,
  proposal: z.input<typeof overtimeInput>,
) {
  const parsed = overtimeInput.parse(proposal);
  if (
    !overtimeReady(source) ||
    parsed.businessDate !== source.businessDate ||
    parsed.expectedSourceFingerprint !== source.sourceFingerprint
  )
    throw new Error('Overtime source is unavailable or changed. Reload and review.');
  const session = source.sessions.find(
    (row) => row.id === parsed.workSessionId && row.endsAtUtc && row.validDutyEvidence,
  );
  if (
    !session ||
    !session.endsAtUtc ||
    !source.roster.endsAt ||
    Date.parse(parsed.startsAtUtc) < Date.parse(session.startsAtUtc) ||
    Date.parse(parsed.endsAtUtc) > Date.parse(session.endsAtUtc) ||
    Date.parse(parsed.startsAtUtc) < Date.parse(source.roster.endsAt)
  )
    throw new Error(
      'Choose a proposed interval inside the retained closed duty and after the scheduled end.',
    );
  return parsed;
}
export const overtimeBulkSubmit = z
  .strictObject({
    employees: z
      .array(z.strictObject({ employeeId: id, request: overtimeInput }))
      .min(1)
      .max(25),
    reason: reason(),
  })
  .refine(
    (v) => new Set(v.employees.map((e) => e.employeeId)).size === v.employees.length,
    'Name each employee once.',
  );
export const overtimeBulkDecide = z
  .strictObject({
    employees: z
      .array(z.strictObject({ employeeId: id, requestId: id, decision: decisionInput }))
      .min(1)
      .max(25),
    reason: reason(),
  })
  .refine(
    (v) =>
      new Set(v.employees.map((e) => e.requestId)).size === v.employees.length &&
      new Set(v.employees.map((e) => e.employeeId)).size === v.employees.length,
    'Name each employee and request once.',
  );
