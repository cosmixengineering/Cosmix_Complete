import type { SessionUser } from './types';
import { isSystemAdmin } from './contracts.ts';

// Presentation only. Each destination and the API still enforce their own access.
export function hasWorkspaceGrant(user: SessionUser | null, permission: string, scoped = false) {
  return Boolean(
    user?.account &&
    user.grants.some(
      (grant) =>
        grant.permission === permission &&
        ((grant.scopeKind === 7 && grant.targetId === null) ||
          (scoped &&
            [2, 3, 4, 5, 6].includes(grant.scopeKind) &&
            typeof grant.targetId === 'string' &&
            /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
              grant.targetId,
            ) &&
            grant.targetId !== '00000000-0000-0000-0000-000000000000')),
    ),
  );
}

export function canOpenWorkspace(user: SessionUser | null, path: string): boolean {
  const any = (...codes: string[]) => codes.some((code) => hasWorkspaceGrant(user, code));
  const all = (...codes: string[]) => codes.every((code) => hasWorkspaceGrant(user, code));
  const scoped = (...codes: string[]) => codes.some((code) => hasWorkspaceGrant(user, code, true));
  switch (path) {
    case '/account':
      return Boolean(user?.account);
    case '/hr/dashboard':
    case '/hr/employees':
    case '/hr/requests':
      return all('hr.employees.read');
    case '/hr/biometrics':
      return all('hr.employees.read') && any('attendance.face.manage', 'attendance.face.read');
    case '/hr/organization':
      return any('hr.organization.read', 'hr.holidays.read', 'hr.sites.read');
    case '/hr/sites':
      return all('hr.sites.read');
    case '/hr/attendance':
      return scoped('attendance.summaries.read', 'attendance.exceptions.read');
    case '/hr/attendance/tracking':
      return all('hr.employees.read', 'attendance.summaries.read');
    case '/hr/attendance/shifts':
      return all('attendance.shifts.read');
    case '/hr/leaves':
      return scoped('attendance.leaves.team-calendar.read') || all('attendance.leaves.review');
    case '/hr/allowances':
      return (
        any('attendance.allowances.review', 'attendance.allowances.policy') ||
        scoped('attendance.deployments.read')
      );
    case '/hr/advances-loans':
      return all('attendance.advancesloans.review');
    case '/hr/vehicles':
      return any(
        ...[
          'manage',
          'custody.manage',
          'trips.manage',
          'fuel.manage',
          'maintenance.manage',
          'incident.manage',
          'commute.manage',
          'policy.manage',
        ].map((suffix) => `attendance.vehicle.${suffix}`),
      );
    case '/hr/payroll-handoff':
      return any('attendance.payroll-handoff.read', 'attendance.payroll-salary.read');
    case '/hr/payroll-reconciliation':
      return all('attendance.payroll-salary.read');
    case '/hr/payroll-accounting':
      return any(
        'finance.bank-reconciliation.read',
        'finance.ledger.read',
        'payroll.reports.read',
        'payroll.accounting.accrue',
        'payroll.accounting.configure',
        'payroll.accounting.configure.approve',
      );
    case '/hr/statutory-contributions':
      return all('statutory.contributions.manage');
    case '/hr/reports/document-expiry':
      return all('hr.employees.read', 'hr.employees.documents.read');
    case '/hr/reports/hr':
      return scoped('hr.reports.read');
    case '/hr/overtime/review':
      return scoped('attendance.overtime.review');
    case '/hr/attendance/corrections/review':
      return scoped('attendance.corrections.review');
    case '/hr/attendance/site-reports':
      return scoped('attendance.site-reports.read');
    default:
      return false;
  }
}

export function workspaceHome(user: SessionUser | null): string {
  if (isSystemAdmin(user)) return '/admin/dashboard';
  return (
    [
      '/hr/dashboard',
      '/hr/payroll-accounting',
      '/hr/payroll-handoff',
      '/hr/attendance',
      '/hr/overtime/review',
      '/hr/attendance/corrections/review',
      '/hr/attendance/site-reports',
      '/hr/leaves',
      '/hr/allowances',
      '/hr/advances-loans',
      '/hr/vehicles',
      '/hr/organization',
    ].find((path) => canOpenWorkspace(user, path)) ?? '/account'
  );
}

export function workspaceLabel(user: SessionUser | null): string {
  const home = workspaceHome(user);
  if (home.startsWith('/admin/')) return 'Administration';
  if (home === '/hr/dashboard') return 'Human Resources';
  if (home.startsWith('/hr/payroll-')) return 'Payroll & Finance';
  if (home === '/account') return 'My account';
  return 'Operations';
}
