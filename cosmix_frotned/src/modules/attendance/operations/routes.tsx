import { lazy, type ComponentType } from 'react';

function page<T extends string>(loader: () => Promise<Record<T, ComponentType>>, name: T) {
  return lazy(async () => ({ default: (await loader())[name] }));
}

const review = () => import('./review');
const leave = () => import('./leave-policy');
const field = () => import('./field');
const reports = () => import('./reports');
const capturePolicy = () => import('./capture-policy');
const Daily = page(review, 'AttendanceDailyPage');
const Register = page(review, 'AttendanceRegisterPage');
const Calendar = page(review, 'AttendanceCalendarPage');
const Audit = page(review, 'AttendanceAuditPage');
const Exceptions = page(review, 'AttendanceExceptionsPage');
const Corrections = page(review, 'AttendanceCorrectionsPage');
const CarryForward = page(review, 'AttendanceCarryForwardPage');
const CarryMonth = page(review, 'AttendanceCarryForwardMonthPage');
const Overtime = page(review, 'AttendanceOvertimePage');

export const attendanceOperationalRoutes = [
  {
    path: '/hr/attendance/capture-policy',
    Page: page(capturePolicy, 'CaptureValidationPolicyPage'),
  },
  { path: '/hr/attendance/daily', Page: Daily },
  { path: '/hr/attendance/employees/:employeeId/daily', Page: Daily },
  { path: '/hr/attendance/register', Page: Register },
  { path: '/hr/attendance/employees/:employeeId/register', Page: Register },
  { path: '/hr/attendance/calendar', Page: Calendar },
  { path: '/hr/attendance/employees/:employeeId/calendar', Page: Calendar },
  { path: '/hr/attendance/audit', Page: Audit },
  { path: '/hr/attendance/employees/:employeeId/audit', Page: Audit },
  { path: '/hr/attendance/exceptions', Page: Exceptions },
  { path: '/hr/attendance/corrections', Page: Corrections },
  { path: '/hr/attendance/employees/:employeeId/corrections', Page: Corrections },
  {
    path: '/hr/attendance/corrections/review',
    Page: page(review, 'AttendanceCorrectionReviewPage'),
  },
  { path: '/hr/attendance/carry-forward', Page: CarryForward },
  { path: '/hr/attendance/employees/:employeeId/carry-forward', Page: CarryForward },
  {
    path: '/hr/attendance/carry-forward/review',
    Page: page(review, 'AttendanceCarryForwardReviewPage'),
  },
  { path: '/hr/attendance/carry-forward/month', Page: CarryMonth },
  { path: '/hr/attendance/employees/:employeeId/carry-forward/month', Page: CarryMonth },
  { path: '/hr/overtime', Page: Overtime },
  { path: '/hr/overtime/employees/:employeeId', Page: Overtime },
  { path: '/hr/overtime/review', Page: page(review, 'AttendanceOvertimeReviewPage') },
  { path: '/hr/overtime/bulk', Page: page(review, 'AttendanceOvertimeBulkPage') },
  { path: '/hr/overtime/eligibility', Page: page(review, 'AttendanceOvertimeEligibilityPage') },
  {
    path: '/hr/overtime/employees/:employeeId/eligibility',
    Page: page(review, 'EmployeeOvertimeEligibilityPage'),
  },
  {
    path: '/hr/overtime/employee-eligibility',
    Page: page(review, 'EmployeeOvertimeEligibilityPage'),
  },
  { path: '/hr/attendance/leaves', Page: page(leave, 'LeaveAdminPage') },
  { path: '/hr/attendance/leaves/:leaveId', Page: page(leave, 'LeaveAdminDetailPage') },
  { path: '/hr/attendance/leave-settings', Page: page(leave, 'LeaveSettingsPage') },
  {
    path: '/hr/attendance/leave-benefits',
    Page: page(() => import('./leave-benefits/LeaveBenefitsPage'), 'LeaveBenefitsPage'),
  },
  {
    path: '/hr/attendance/recovery-work',
    Page: page(() => import('./leave-benefits/LeaveRecoveryReviewPage'), 'LeaveRecoveryReviewPage'),
  },
  { path: '/hr/attendance/policies', Page: page(leave, 'AttendancePoliciesPage') },
  { path: '/hr/attendance/policies/:policyId', Page: page(leave, 'AttendancePolicyDetailPage') },
  { path: '/hr/attendance/zones/:siteId?', Page: page(field, 'OperationalZonesPage') },
  { path: '/hr/attendance/zone-events/:siteId?', Page: page(field, 'ZoneEventsPage') },
  {
    path: '/hr/attendance/zone-windows/:siteId?/:employeeId?',
    Page: page(field, 'ZoneDutyWindowsPage'),
  },
  { path: '/hr/attendance/site-reports/:siteId?', Page: page(field, 'SiteReportQueuePage') },
  { path: '/account/notifications', Page: page(field, 'MyNotificationsPage') },
  { path: '/hr/attendance/reports', Page: page(reports, 'AttendanceReportsPage') },
  { path: '/hr/attendance/month-close', Page: page(reports, 'MonthClosurePage') },
  { path: '/hr/attendance/payroll-inputs', Page: page(reports, 'PayrollInputsPage') },
] as const;

export const connectedAttendanceWorkspaces: Readonly<Record<string, string>> = {
  'shifts-rosters': '/hr/attendance/shifts',
  'attendance-review': '/hr/attendance/daily',
  overtime: '/hr/overtime',
  'operational-zones': '/hr/attendance/zones',
  'site-report-review': '/hr/attendance/site-reports',
  'attendance-policies': '/hr/attendance/policies',
  'attendance-month-close': '/hr/attendance/month-close',
  leave: '/hr/leaves',
};
