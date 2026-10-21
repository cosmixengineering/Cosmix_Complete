import type { SessionUser } from '@/auth/types';
import { hasReportGrant } from './reports/access';

function all(user: SessionUser | null, permission: string) {
  return Boolean(
    user?.account &&
    user.grants.some(
      (g) => g.permission === permission && g.scopeKind === 7 && g.targetId === null,
    ),
  );
}
function scoped(user: SessionUser | null, permission: string) {
  return hasReportGrant(user, permission);
}

export function attendanceOperationalNavigation(user: SessionUser | null) {
  return [
    {
      to: '/hr/attendance/office-boundaries',
      label: 'Office boundaries',
      allowed: all(user, 'org.company.read'),
    },
    {
      to: '/hr/attendance/capture-policy',
      label: 'Capture validation policy',
      allowed:
        Boolean(user?.account?.webAccessEnabled && user.account.client === 1) &&
        all(user, 'attendance.capture.validation.read'),
    },
    {
      to: '/hr/attendance/daily',
      label: 'Attendance review',
      allowed: scoped(user, 'attendance.summaries.read'),
    },
    {
      to: '/hr/attendance/register',
      label: 'Attendance register',
      allowed: scoped(user, 'attendance.summaries.read'),
    },
    {
      to: '/hr/attendance/calendar',
      label: 'Attendance calendar',
      allowed: scoped(user, 'attendance.summaries.read'),
    },
    {
      to: '/hr/attendance/audit',
      label: 'Attendance audit',
      allowed: scoped(user, 'attendance.punches.read'),
    },
    {
      to: '/hr/attendance/exceptions',
      label: 'Attendance exceptions',
      allowed: scoped(user, 'attendance.exceptions.read'),
    },
    {
      to: '/hr/attendance/corrections',
      label: 'Corrections',
      allowed: scoped(user, 'attendance.corrections.submit'),
    },
    {
      to: '/hr/attendance/corrections/review',
      label: 'Correction review',
      allowed: scoped(user, 'attendance.corrections.review'),
    },
    {
      to: '/hr/attendance/carry-forward/review',
      label: 'Carry-forward review',
      allowed: scoped(user, 'attendance.corrections.review'),
    },
    {
      to: '/hr/attendance/carry-forward/month',
      label: 'Carry-forward ledger',
      allowed: scoped(user, 'attendance.corrections.review'),
    },
    {
      to: '/hr/overtime/review',
      label: 'Overtime review',
      allowed: scoped(user, 'attendance.overtime.review'),
    },
    {
      to: '/hr/overtime/bulk',
      label: 'Named overtime batch',
      allowed:
        scoped(user, 'attendance.overtime.submit') || scoped(user, 'attendance.overtime.decide'),
    },
    {
      to: '/hr/overtime/eligibility',
      label: 'Common overtime eligibility',
      allowed: all(user, 'attendance.overtime.eligibility.read'),
    },
    {
      to: '/hr/overtime/employee-eligibility',
      label: 'Employee overtime eligibility',
      allowed: scoped(user, 'attendance.overtime.eligibility.read'),
    },
    {
      to: '/hr/attendance/carry-forward',
      label: 'Carry-forward requests',
      allowed: scoped(user, 'attendance.corrections.submit'),
    },
    {
      to: '/hr/overtime',
      label: 'Overtime',
      allowed: scoped(user, 'attendance.overtime.submit'),
    },
    {
      to: '/hr/attendance/leaves',
      label: 'Leave review',
      allowed: all(user, 'attendance.leaves.review'),
    },
    {
      to: '/hr/attendance/leave-settings',
      label: 'Leave settings',
      allowed: all(user, 'attendance.leaves.configure'),
    },
    {
      to: '/hr/leave-encashment',
      label: 'Encashment review',
      allowed: all(user, 'attendance.leaves.review'),
    },
    {
      to: '/hr/attendance/leave-benefits',
      label: 'Leave benefits',
      allowed: all(user, 'attendance.leaves.benefits.review'),
    },
    {
      to: '/hr/attendance/recovery-work',
      label: 'Recovery work review',
      allowed: all(user, 'attendance.leaves.review'),
    },
    {
      to: '/hr/attendance/policies',
      label: 'Attendance policies',
      allowed:
        all(user, 'attendance.policies.configure') || all(user, 'attendance.policies.publish'),
    },
    {
      to: '/hr/attendance/zones',
      label: 'Operational zones',
      allowed: scoped(user, 'attendance.zones.read'),
    },
    {
      to: '/hr/attendance/zone-events',
      label: 'Zone events',
      allowed: scoped(user, 'attendance.zones.review'),
    },
    {
      to: '/hr/attendance/zone-windows',
      label: 'Duty windows',
      allowed: scoped(user, 'attendance.zones.location.read'),
    },
    {
      to: '/hr/attendance/site-reports',
      label: 'Site reports',
      allowed: scoped(user, 'attendance.site-reports.read'),
    },
    {
      to: '/hr/attendance/reports',
      label: 'Attendance reports',
      allowed: scoped(user, 'attendance.reports.read') || scoped(user, 'attendance.reports.export'),
    },
    {
      to: '/hr/attendance/month-close',
      label: 'Month closure',
      allowed: all(user, 'attendance.month-locks.read'),
    },
    {
      to: '/hr/attendance/payroll-inputs',
      label: 'Payroll inputs',
      allowed: scoped(user, 'attendance.payroll-inputs.read'),
    },
    { to: '/account/notifications', label: 'Notifications', allowed: Boolean(user?.account) },
  ].map((link) => ({
    ...link,
    section: link.to.startsWith('/account/')
      ? 'Account'
      : /policies|leave-settings|capture-policy/.test(link.to)
        ? 'Policies'
        : /zones|zone-events|zone-windows|site-reports/.test(link.to)
          ? 'Field operations'
          : /reports|month-close|payroll-inputs/.test(link.to)
            ? 'Reports and closure'
            : 'Attendance',
  }));
}
