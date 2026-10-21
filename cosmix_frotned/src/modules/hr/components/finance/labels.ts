import { formatNumber } from '@/utils/format';

export const allowanceKindLabels: Record<number, string> = {
  1: 'Office meal',
  2: 'Site dinner',
  3: 'Site breakfast',
  4: 'Site lunch',
  5: 'Outstation daily',
  6: 'Work travel',
};

export const advanceKindLabels: Record<number, string> = {
  1: 'Advance',
  2: 'Loan',
};

export const allowanceKindOptions = [
  { value: '1', label: 'Office meal' },
  { value: '2', label: 'Site dinner' },
  { value: '3', label: 'Site breakfast' },
  { value: '4', label: 'Site lunch' },
  { value: '5', label: 'Outstation daily' },
  { value: '6', label: 'Work travel' },
];

export const advanceKindOptions = [
  { value: '1', label: 'Advance' },
  { value: '2', label: 'Loan' },
];

export const approvalStateOptions = [
  { value: '', label: 'All states' },
  { value: '0', label: 'Pending' },
  { value: '1', label: 'Approved' },
  { value: '2', label: 'Returned' },
  { value: '3', label: 'Rejected' },
  { value: '4', label: 'Cancelled' },
];

export const decisionOptions = [
  { value: '1', label: 'Approve' },
  { value: '2', label: 'Return for correction' },
  { value: '3', label: 'Reject' },
];

export const repaymentKindOptions = [
  { value: '1', label: 'Payroll deduction' },
  { value: '2', label: 'Received payment' },
  { value: '3', label: 'Reversal' },
  { value: '4', label: 'Adjustment' },
];

export const journeyPurposeOptions = [
  { value: '1', label: 'Site to site' },
  { value: '2', label: 'Complaint visit' },
  { value: '3', label: 'Field visit' },
  { value: '4', label: 'Outstation' },
  { value: '5', label: 'Commute' },
];

export const serviceKindOptions = [
  { value: '1', label: 'Tuning' },
  { value: '2', label: 'Engine overhaul' },
  { value: '3', label: 'Other' },
];

export const policyTargetOptions = [
  { value: '1', label: 'Employee' },
  { value: '2', label: 'Department' },
  { value: '3', label: 'Staff category' },
  { value: '4', label: 'Site' },
];

const positive = new Set([
  'Approved',
  'approved',
  'eligible',
  'RecordedAsPaid',
  'settled',
  'locked',
  'effective',
]);

const negative = new Set([
  'Rejected',
  'rejected',
  'not_met',
  'policy_missing',
  'inapplicable_staff_category',
  'deployment_workflow_unavailable',
  'amount_pending_management_decision',
]);

export function statusLabel(value: string) {
  return value
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[_-]+/g, ' ')
    .replace(/^./, (c) => c.toUpperCase());
}

export function statusClass(value: string) {
  if (positive.has(value)) return 'hr-status hr-status--positive';
  if (negative.has(value)) return 'hr-status hr-status--negative';
  return 'hr-status hr-status--neutral';
}

export function money(value: number, currency?: string | null) {
  return currency ? `${formatNumber(value)} ${currency}` : formatNumber(value);
}

export function dayRange(days: number) {
  const end = new Date();
  const start = new Date();
  start.setDate(end.getDate() - days);
  return { from: toDay(start), to: toDay(end) };
}

function toDay(value: Date) {
  return value.toISOString().slice(0, 10);
}
