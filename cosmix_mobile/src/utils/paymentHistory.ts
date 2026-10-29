import type { DataRecord, ServiceData } from '../types/selfService';
import { AppError } from './errors';
import { record, records, requireDate } from './serviceValues';
import { isGuid, isTimestamp } from './validation';

const paymentStates = ['Pending', 'Posted', 'Confirmed', 'Failed', 'Reversed'];
const settlementStates = [
  'NoPaymentDue',
  'Unpaid',
  'Pending',
  'PartiallyPaid',
  'Paid',
  'Failed',
  'Reversed',
];
const totals = [
  'netEntitlement',
  'confirmedAmount',
  'postedAmount',
  'pendingAmount',
  'failedAmount',
  'reversedAmount',
  'outstandingAmount',
  'remainingToInitiate',
];

function invalid(): never {
  throw new AppError('invalid_server_response');
}

function date(value: unknown): string {
  if (typeof value !== 'string') return invalid();
  try {
    return requireDate(value);
  } catch {
    return invalid();
  }
}

export function paymentHistoryGroup(value: unknown): DataRecord {
  const row = record(value);
  if (
    !isGuid(row.batchId) ||
    typeof row.batchReference !== 'string' ||
    !row.batchReference.trim() ||
    row.batchReference.length > 200 ||
    typeof row.currency !== 'string' ||
    !/^[A-Z]{3}$/.test(row.currency) ||
    !settlementStates.includes(String(row.paymentState)) ||
    !Number.isInteger(row.paymentsPage) ||
    Number(row.paymentsPage) < 1 ||
    row.paymentsPageSize !== 20 ||
    typeof row.hasMorePayments !== 'boolean' ||
    totals.some(
      key =>
        typeof row[key] !== 'number' ||
        !Number.isFinite(row[key]) ||
        Number(row[key]) < 0,
    )
  )
    return invalid();
  if (date(row.periodEndExclusive) <= date(row.periodStart)) return invalid();
  const payments = records(row.payments, 20);
  if (new Set(payments.map(payment => payment.id)).size !== payments.length)
    return invalid();
  for (const payment of payments) {
    if (
      !isGuid(payment.id) ||
      typeof payment.amount !== 'number' ||
      !Number.isFinite(payment.amount) ||
      payment.amount <= 0 ||
      !paymentStates.includes(String(payment.state)) ||
      !isTimestamp(payment.createdAtUtc) ||
      ![payment.confirmedAtUtc, payment.reversedAtUtc].every(
        timestamp => timestamp === null || isTimestamp(timestamp),
      )
    )
      return invalid();
    if (payment.valueDate !== null) date(payment.valueDate);
  }
  return row;
}

export function paymentHistoryPage(value: unknown, page: number): ServiceData {
  const root = record(value);
  if (
    root.page !== page ||
    root.pageSize !== 20 ||
    typeof root.hasMore !== 'boolean'
  )
    return invalid();
  const rows = records(root.items, 20).map(paymentHistoryGroup);
  if (new Set(rows.map(row => row.batchId)).size !== rows.length)
    return invalid();
  return { root, rows };
}

export function paymentStatus(value: unknown): string {
  const labels: Record<string, string> = {
    NoPaymentDue: 'No payment due',
    Unpaid: 'Unpaid',
    Pending: 'Pending',
    PartiallyPaid: 'Partially paid',
    Paid: 'Paid · confirmed',
    Posted: 'Posted · awaiting confirmation',
    Confirmed: 'Confirmed',
    Failed: 'Failed',
    Reversed: 'Reversed',
  };
  return labels[String(value)] ?? 'Unavailable';
}
