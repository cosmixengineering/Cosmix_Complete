export {
  AttendanceDailyPage,
  MyAttendanceDailyPage,
  AttendanceRegisterPage,
  MyAttendanceRegisterPage,
  AttendanceCalendarPage,
  MyAttendanceCalendarPage,
} from './AttendancePages';
export { AttendanceAuditPage, MyAttendanceAuditPage } from './AuditPages';
export { AttendanceExceptionsPage, MyAttendanceExceptionsPage } from './ExceptionsPages';
export {
  AttendanceCorrectionsPage,
  MyAttendanceCorrectionsPage,
  AttendanceCorrectionReviewPage,
  AttendanceCarryForwardPage,
  MyAttendanceCarryForwardPage,
  AttendanceCarryForwardReviewPage,
  AttendanceOvertimePage,
  MyAttendanceOvertimePage,
  AttendanceOvertimeReviewPage,
} from './RequestPages';
export {
  AttendanceCarryForwardMonthPage,
  MyAttendanceCarryForwardMonthPage,
} from './CarryForwardMonthPages';
export {
  AttendanceOvertimeEligibilityPage,
  EmployeeOvertimeEligibilityPage,
} from './EligibilityPages';
export { AttendanceOvertimeBulkPage } from './OvertimeBulkPage';

type RoutePermission = {
  readAnyOf: readonly string[];
  permissionScope: 'self' | 'scoped' | 'all';
  separateWrites?: readonly string[];
  separateFinancialRead?: string;
};
// Scoped routes accept matching Employee/Department/Team/Site/Project or actual All grants.
// Read access never confers any of the separate write or financial permissions.
const routePermissions = {
  AttendanceDailyPage: {
    readAnyOf: ['attendance.summaries.read'],
    permissionScope: 'scoped',
    separateWrites: ['attendance.summaries.calculate'],
  },
  AttendanceRegisterPage: {
    readAnyOf: ['attendance.summaries.read'],
    permissionScope: 'scoped',
    separateWrites: ['attendance.summaries.calculate'],
  },
  AttendanceCalendarPage: {
    readAnyOf: ['attendance.summaries.read'],
    permissionScope: 'scoped',
    separateWrites: ['attendance.summaries.calculate'],
  },
  MyAttendanceDailyPage: { readAnyOf: ['attendance.summaries.self'], permissionScope: 'self' },
  MyAttendanceRegisterPage: { readAnyOf: ['attendance.summaries.self'], permissionScope: 'self' },
  MyAttendanceCalendarPage: { readAnyOf: ['attendance.summaries.self'], permissionScope: 'self' },
  AttendanceAuditPage: { readAnyOf: ['attendance.punches.read'], permissionScope: 'scoped' },
  MyAttendanceAuditPage: { readAnyOf: ['attendance.history.self'], permissionScope: 'self' },
  AttendanceExceptionsPage: {
    readAnyOf: ['attendance.exceptions.read'],
    permissionScope: 'scoped',
  },
  MyAttendanceExceptionsPage: {
    readAnyOf: ['attendance.exceptions.self'],
    permissionScope: 'self',
  },
  AttendanceCorrectionsPage: {
    readAnyOf: ['attendance.corrections.submit'],
    permissionScope: 'scoped',
    separateWrites: ['attendance.corrections.submit'],
  },
  MyAttendanceCorrectionsPage: {
    readAnyOf: ['attendance.corrections.self'],
    permissionScope: 'self',
    separateWrites: ['attendance.corrections.self'],
  },
  AttendanceCorrectionReviewPage: {
    readAnyOf: ['attendance.corrections.review'],
    permissionScope: 'scoped',
    separateWrites: ['attendance.corrections.decide'],
  },
  AttendanceCarryForwardPage: {
    readAnyOf: ['attendance.corrections.submit'],
    permissionScope: 'scoped',
    separateWrites: ['attendance.corrections.submit'],
  },
  MyAttendanceCarryForwardPage: {
    readAnyOf: ['attendance.corrections.self'],
    permissionScope: 'self',
    separateWrites: ['attendance.corrections.self'],
  },
  AttendanceCarryForwardReviewPage: {
    readAnyOf: ['attendance.corrections.review'],
    permissionScope: 'scoped',
    separateWrites: ['attendance.corrections.decide'],
  },
  AttendanceCarryForwardMonthPage: {
    readAnyOf: ['attendance.corrections.review'],
    permissionScope: 'scoped',
  },
  MyAttendanceCarryForwardMonthPage: {
    readAnyOf: ['attendance.corrections.self'],
    permissionScope: 'self',
  },
  AttendanceOvertimePage: {
    readAnyOf: ['attendance.overtime.submit'],
    permissionScope: 'scoped',
    separateWrites: ['attendance.overtime.submit'],
    separateFinancialRead: 'attendance.overtime.financial.read',
  },
  MyAttendanceOvertimePage: {
    readAnyOf: ['attendance.overtime.self'],
    permissionScope: 'self',
    separateWrites: ['attendance.overtime.self'],
    separateFinancialRead: 'attendance.overtime.financial.self',
  },
  AttendanceOvertimeReviewPage: {
    readAnyOf: ['attendance.overtime.review'],
    permissionScope: 'scoped',
    separateWrites: ['attendance.overtime.decide'],
    separateFinancialRead: 'attendance.overtime.financial.read',
  },
  AttendanceOvertimeEligibilityPage: {
    readAnyOf: ['attendance.overtime.eligibility.read'],
    permissionScope: 'all',
    separateWrites: ['attendance.overtime.eligibility.manage'],
  },
  EmployeeOvertimeEligibilityPage: {
    readAnyOf: ['attendance.overtime.eligibility.read'],
    permissionScope: 'scoped',
    separateWrites: ['attendance.overtime.eligibility.manage'],
  },
  AttendanceOvertimeBulkPage: {
    readAnyOf: ['attendance.overtime.submit', 'attendance.overtime.review'],
    permissionScope: 'scoped',
    separateWrites: ['attendance.overtime.submit', 'attendance.overtime.decide'],
  },
} as const satisfies Record<string, RoutePermission>;

// Parent composition owns mounting these pages behind the existing authenticated layout.
export const attendanceReviewRouteMap = (
  [
    { path: '/hr/attendance/daily', page: 'AttendanceDailyPage' },
    { path: '/hr/attendance/employees/:employeeId/daily', page: 'AttendanceDailyPage' },
    { path: '/hr/attendance/register', page: 'AttendanceRegisterPage' },
    { path: '/hr/attendance/employees/:employeeId/register', page: 'AttendanceRegisterPage' },
    { path: '/hr/attendance/calendar', page: 'AttendanceCalendarPage' },
    { path: '/hr/attendance/employees/:employeeId/calendar', page: 'AttendanceCalendarPage' },
    { path: '/hr/attendance/audit', page: 'AttendanceAuditPage' },
    { path: '/hr/attendance/employees/:employeeId/audit', page: 'AttendanceAuditPage' },
    { path: '/hr/attendance/exceptions', page: 'AttendanceExceptionsPage' },
    { path: '/hr/attendance/corrections', page: 'AttendanceCorrectionsPage' },
    { path: '/hr/attendance/employees/:employeeId/corrections', page: 'AttendanceCorrectionsPage' },
    { path: '/hr/attendance/corrections/review', page: 'AttendanceCorrectionReviewPage' },
    { path: '/hr/attendance/carry-forward', page: 'AttendanceCarryForwardPage' },
    {
      path: '/hr/attendance/employees/:employeeId/carry-forward',
      page: 'AttendanceCarryForwardPage',
    },
    { path: '/hr/attendance/carry-forward/review', page: 'AttendanceCarryForwardReviewPage' },
    { path: '/hr/attendance/carry-forward/month', page: 'AttendanceCarryForwardMonthPage' },
    {
      path: '/hr/attendance/employees/:employeeId/carry-forward/month',
      page: 'AttendanceCarryForwardMonthPage',
    },
    { path: '/hr/overtime', page: 'AttendanceOvertimePage' },
    { path: '/hr/overtime/employees/:employeeId', page: 'AttendanceOvertimePage' },
    { path: '/hr/overtime/review', page: 'AttendanceOvertimeReviewPage' },
    { path: '/hr/overtime/bulk', page: 'AttendanceOvertimeBulkPage' },
    { path: '/hr/overtime/eligibility', page: 'AttendanceOvertimeEligibilityPage' },
    {
      path: '/hr/overtime/employees/:employeeId/eligibility',
      page: 'EmployeeOvertimeEligibilityPage',
    },
    { path: '/me/attendance/daily', page: 'MyAttendanceDailyPage' },
    { path: '/me/attendance/register', page: 'MyAttendanceRegisterPage' },
    { path: '/me/attendance/calendar', page: 'MyAttendanceCalendarPage' },
    { path: '/me/attendance/audit', page: 'MyAttendanceAuditPage' },
    { path: '/me/attendance/exceptions', page: 'MyAttendanceExceptionsPage' },
    { path: '/me/attendance/corrections', page: 'MyAttendanceCorrectionsPage' },
    { path: '/me/attendance/carry-forward', page: 'MyAttendanceCarryForwardPage' },
    { path: '/me/attendance/carry-forward/month', page: 'MyAttendanceCarryForwardMonthPage' },
    { path: '/me/overtime', page: 'MyAttendanceOvertimePage' },
  ] as const
).map((route) => ({ ...route, ...routePermissions[route.page] }));
