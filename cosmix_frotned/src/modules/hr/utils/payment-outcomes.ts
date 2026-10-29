import { z } from 'zod';
import type { SalaryPayment } from '../schemas/payroll-accounting';
import { date } from '../schemas/contracts';

export type PaymentOutcome = 'confirm' | 'failure' | 'reversal' | 'approve';
export type PaymentOutcomeAccess = { actorId: string; canRequest: boolean; canConfirm: boolean };
const reasonSchema = z
  .string()
  .trim()
  .min(1)
  .max(500)
  .refine((value) => !/\p{Cc}/u.test(value));
const evidenceSchema = z
  .string()
  .trim()
  .min(1)
  .max(8000)
  .refine((value) => !/\p{Cc}/u.test(value.replace(/[\r\n\t]/g, '')));

export function paymentOutcomeChoices(
  payment: SalaryPayment,
  access: PaymentOutcomeAccess,
): { value: PaymentOutcome; label: string }[] {
  if (!access.actorId || !['Posted', 'Confirmed'].includes(payment.state)) return [];
  const independent = access.actorId.toLowerCase() !== payment.createdByUserId.toLowerCase();
  if (payment.reversalRequestedAt) {
    return access.canConfirm &&
      independent &&
      payment.reversalRequestedByUserId &&
      access.actorId.toLowerCase() !== payment.reversalRequestedByUserId.toLowerCase()
      ? [
          {
            value: 'approve',
            label: payment.reversalIsFailure
              ? 'Approve failed-payment reversal'
              : 'Approve payment reversal',
          },
        ]
      : [];
  }
  return [
    ...(access.canConfirm && independent && payment.state === 'Posted'
      ? [{ value: 'confirm' as const, label: 'Confirm recorded payment' }]
      : []),
    ...(access.canRequest && payment.state === 'Posted'
      ? [{ value: 'failure' as const, label: 'Request failed-payment reversal' }]
      : []),
    ...(access.canRequest
      ? [{ value: 'reversal' as const, label: 'Request payment reversal' }]
      : []),
  ];
}

export function preparePaymentOutcome(
  payment: SalaryPayment,
  access: PaymentOutcomeAccess,
  outcome: PaymentOutcome,
  reason: string,
  evidence: string,
  postingDate: string,
) {
  const choice = paymentOutcomeChoices(payment, access).find((item) => item.value === outcome);
  if (!choice || !Number.isInteger(payment.version) || payment.version < 1)
    throw new Error('Payment action is unavailable. Refresh the payment record.');
  const body = {
    reason: reasonSchema.parse(reason),
    evidence: evidenceSchema.parse(evidence),
    expectedVersion: payment.version,
  };
  const route = `/payroll-accounting/payments/${payment.id}`;
  return outcome === 'confirm'
    ? { path: `${route}/confirm`, body, label: choice.label }
    : outcome === 'approve'
      ? {
          path: `${route}/reversal-approve`,
          body: { ...body, postingDate: date.parse(postingDate) },
          label: choice.label,
        }
      : {
          path: `${route}/reversal-request`,
          body: { ...body, paymentFailed: outcome === 'failure' },
          label: choice.label,
        };
}
