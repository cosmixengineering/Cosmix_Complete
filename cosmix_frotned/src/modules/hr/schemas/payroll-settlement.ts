import { z } from 'zod';
import { date, id, time } from './contracts';

const amount = z.number().finite().nonnegative();
export const payrollSettlementBatchPage = z.object({
  items: z
    .array(
      z.object({
        batchId: id,
        batchReference: z.string().min(1),
        periodStart: date,
        periodEndExclusive: date,
        currency: z.string().regex(/^[A-Z]{3}$/),
      }),
    )
    .max(25),
  page: z.number().int().positive(),
  pageSize: z.literal(25),
  hasMore: z.boolean(),
});
export const settlementState = z.enum([
  'NoPaymentDue',
  'Unpaid',
  'Pending',
  'PartiallyPaid',
  'Paid',
  'Failed',
  'Reversed',
]);
export const payrollSettlementReport = z
  .object({
    batchId: id,
    batchReference: z.string().min(1),
    periodStart: date,
    periodEndExclusive: date,
    currency: z.string().regex(/^[A-Z]{3}$/),
    sourceFingerprint: z.string().min(1),
    generatedAtUtc: time,
    employees: z.array(
      z.object({
        employeeId: id,
        employeeNumber: z.number().int().safe(),
        employeeName: z.string(),
        grossEarnings: amount,
        deductions: amount,
        netEntitlement: amount,
        payments: z.object({
          confirmed: amount,
          posted: amount,
          pending: amount,
          failed: amount,
          reversed: amount,
          outstanding: amount,
          remainingToInitiate: amount,
          state: settlementState,
        }),
      }),
    ),
    components: z.array(
      z.object({ employeeId: id, component: z.string(), amount: z.number().finite() }),
    ),
    grossEarnings: amount,
    deductions: amount,
    netEntitlement: amount,
    confirmedPayments: amount,
    reservedPayments: amount,
    outstandingPayments: amount,
    paymentLedgerNet: z.number().finite(),
    paymentLedgerDifference: z.number().finite(),
    issues: z.array(z.string()),
  })
  .refine((report) => report.periodEndExclusive > report.periodStart, {
    message: 'Invalid payroll period',
  });

export const payrollSettlementExport = z.object({
  fileName: z
    .string()
    .min(1)
    .max(180)
    .refine(
      (value) =>
        /^[^\\/:*?"<>|]+\.csv$/i.test(value) &&
        ![...value].some((char) => char.charCodeAt(0) < 32 || char.charCodeAt(0) === 127),
    ),
  contentType: z.enum(['text/csv', 'text/csv; charset=utf-8']),
  base64: z
    .string()
    .min(4)
    .max(28000000)
    .regex(/^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/),
  sha256: z.string().regex(/^[a-f0-9]{64}$/i),
});
export type PayrollSettlementReport = z.infer<typeof payrollSettlementReport>;
