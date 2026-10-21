import type { SessionState } from '../types/contracts';
import type { ServiceKey } from '../types/selfService';

type ServiceDefinition = {
  key: ServiceKey;
  title: string;
  description: string;
  mark: string;
  permissions: string[][];
};
export const employeeServices: readonly ServiceDefinition[] = [
  {
    key: 'leaves',
    title: 'Leave',
    description: 'Apply, view balances and follow requests',
    mark: 'LV',
    permissions: [['attendance.leaves.self']],
  },
  {
    key: 'loans',
    title: 'Advances & loans',
    description: 'Requests, repayment plans and balances',
    mark: 'AL',
    permissions: [['attendance.advancesloans.self']],
  },
  {
    key: 'resignation',
    title: 'Resignation',
    description: 'Submit and follow your employment request',
    mark: 'ER',
    permissions: [['ess.resignation.self']],
  },
  {
    key: 'allowances',
    title: 'Allowances',
    description: 'Check eligibility and claim work allowances',
    mark: 'AC',
    permissions: [['attendance.allowances.self']],
  },
  {
    key: 'vehicles',
    title: 'Vehicle & fuel',
    description: 'Assigned vehicles, trips and fuel claims',
    mark: 'VF',
    permissions: [['attendance.vehicle.self']],
  },
  {
    key: 'corrections',
    title: 'Attendance requests',
    description: 'Request a correction and track its review',
    mark: 'AR',
    permissions: [['attendance.corrections.self']],
  },
  {
    key: 'overtime',
    title: 'Overtime',
    description: 'View eligible duty and submit overtime requests',
    mark: 'OT',
    permissions: [['attendance.overtime.self']],
  },
  {
    key: 'calendar',
    title: 'Attendance calendar',
    description: 'Monthly facts and company assessments',
    mark: 'CA',
    permissions: [
      ['attendance.summaries.self'],
      ['ess.attendance.read', 'auth.self.read'],
    ],
  },
  {
    key: 'punches',
    title: 'Attendance history',
    description: 'All Check In / Check Out receipts and their server results',
    mark: 'AH',
    permissions: [['attendance.history.self']],
  },
  {
    key: 'shift',
    title: 'My shift',
    description: 'Your dated site or office schedule',
    mark: 'SH',
    permissions: [['attendance.rosters.self']],
  },
  {
    key: 'finances',
    title: 'My finances',
    description: 'Compensation and recorded financial history',
    mark: 'FN',
    permissions: [['ess.finances.read']],
  },
  {
    key: 'payroll',
    title: 'Payroll history',
    description: 'Your recorded payroll periods and handoff summaries',
    mark: 'PR',
    permissions: [['ess.finances.read']],
  },
  {
    key: 'encashment',
    title: 'Leave encashment',
    description: 'Request and track leave valuation',
    mark: 'LE',
    permissions: [['attendance.leaves.self']],
  },
  {
    key: 'profile',
    title: 'My profile',
    description: 'Employment, documents and assigned items',
    mark: 'ME',
    permissions: [['ess.profile.read', 'auth.self.read']],
  },
  {
    key: 'requests',
    title: 'Request overview',
    description: 'Pending requests across your workspace',
    mark: 'RQ',
    permissions: [['ess.requests.read', 'auth.self.read']],
  },
  {
    key: 'notifications',
    title: 'Inbox',
    description: 'Company notifications and reminders',
    mark: 'IN',
    permissions: [],
  },
];

export function canOpenService(key: ServiceKey, state: SessionState): boolean {
  const definition = employeeServices.find(item => item.key === key);
  return (
    state.status === 'authenticated' &&
    !!definition &&
    definition.permissions.every(group =>
      group.some(permission =>
        state.permissions.some(grant => grant.permission === permission),
      ),
    )
  );
}

export function serviceTitle(key: ServiceKey): string {
  return (
    employeeServices.find(item => item.key === key)?.title ?? 'Employee service'
  );
}

export const historyCategories: readonly { key: ServiceKey; title: string }[] =
  [
    { key: 'punches', title: 'Check In / Out' },
    { key: 'leaves', title: 'Leave' },
    { key: 'loans', title: 'Advances & loans' },
    { key: 'allowances', title: 'Allowances' },
    { key: 'overtime', title: 'Overtime' },
    { key: 'corrections', title: 'Attendance requests' },
    { key: 'calendar', title: 'Monthly attendance' },
    { key: 'payroll', title: 'Payroll' },
    { key: 'encashment', title: 'Leave encashment' },
    { key: 'vehicles', title: 'Vehicle & fuel' },
    { key: 'resignation', title: 'Resignation' },
  ];

export function resolveHistoryCategory(
  requested: ServiceKey | undefined,
  state: SessionState,
): ServiceKey | null {
  const available = historyCategories.filter(item =>
    canOpenService(item.key, state),
  );
  return (
    available.find(item => item.key === requested)?.key ??
    available[0]?.key ??
    null
  );
}

export function allowanceTitle(value: unknown): string {
  const names = [
    '',
    'Office meal',
    'Site dinner',
    'Site breakfast',
    'Site lunch',
    'Outstation daily',
    'Work travel',
  ];
  return typeof value === 'number' &&
    Number.isInteger(value) &&
    value > 0 &&
    value < names.length
    ? names[value]
    : 'Allowance claim';
}
