import { Suspense } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from '@/auth/use-auth';
import { isSystemAdmin } from '@/auth/contracts';
import { LoadingState } from '@/components/feedback/LoadingState';
import { RequireAuth } from '@/auth/RequireAuth';
import { SignInPage } from '@/pages/SignInPage';
import { ForgotPasswordPage } from '@/pages/ForgotPasswordPage';
import { AccountPage } from '@/pages/AccountPage';
import { AccountSessionsPage } from '@/pages/AccountSessionsPage';
import { AdminDashboardPage } from '@/pages/AdminDashboardPage';
import { AdminApprovalsPage } from '@/pages/AdminApprovalsPage';
import { AdminModulePage } from '@/pages/AdminModulePage';
import { AppLayout } from '@/layouts/AppLayout';
import { AdminLayout } from '@/layouts/AdminLayout';
import {
  ShiftsPage,
  SiteRostersPage,
  SiteGeofencesPage,
  OfficeRostersPage,
  OfficeBoundariesPage,
  AttendanceProcessingPage,
  EmployeeAttendanceDetailPage,
  LeaveManagementPage,
  LeaveEncashmentPage,
  attendanceOperationalRoutes,
  connectedAttendanceWorkspaces,
} from '@/modules/attendance';
import {
  HrDashboardPage,
  EmployeesPage,
  DocumentExpiryPage,
  OrganizationPage,
  EmployeeHiringPage,
  NewEmployeePage,
  EmployeeDetailPage,
  HrRequestsPage,
  HiringApprovalsPage,
  SitesPage,
  SiteDetailPage,
  HrPreparedWorkspacePage,
  EmployeeAssetsPage,
  EmployeeSimsPage,
  EmployeeCertificationsPage,
  foundationPolicyRoutes,
  staffTaxonomyRoutes,
  financeRoutes,
} from '@/modules/hr';
import { ApprovalRoutingPage } from '@/modules/approvals';
import { HrReportsPage } from '@/modules/reports';

function RootRedirect() {
  const { user, status } = useAuth();
  if (status === 'loading') return null;
  if (status !== 'authenticated') return <Navigate to="/sign-in" replace />;
  const isAdmin = isSystemAdmin(user);
  return <Navigate to={isAdmin ? '/admin/dashboard' : '/hr/dashboard'} replace />;
}

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/sign-in" element={<SignInPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route path="/account" element={<AccountPage />} />
          <Route path="/account/sessions" element={<AccountSessionsPage />} />
          <Route path="/self-service" element={<Navigate to="/hr/dashboard" replace />} />
          <Route path="/hr/dashboard" element={<HrDashboardPage />} />
          <Route path="/hr/employees" element={<EmployeesPage />} />
          <Route path="/hr/reports/document-expiry" element={<DocumentExpiryPage />} />
          <Route path="/hr/reports/hr" element={<HrReportsPage />} />
          <Route path="/hr/employees/new" element={<NewEmployeePage />} />
          <Route path="/hr/employees/:employeeId/hiring" element={<EmployeeHiringPage />} />
          <Route path="/hr/employees/:employeeId" element={<EmployeeDetailPage />} />
          <Route path="/hr/employees/:employeeId/assets" element={<EmployeeAssetsPage />} />
          <Route path="/hr/employees/:employeeId/sims" element={<EmployeeSimsPage />} />
          <Route
            path="/hr/employees/:employeeId/certifications"
            element={<EmployeeCertificationsPage />}
          />
          <Route path="/hr/organization" element={<OrganizationPage />} />
          <Route path="/hr/requests" element={<HrRequestsPage />} />
          <Route path="/hr/approvals" element={<HiringApprovalsPage />} />
          <Route path="/hr/approval-routing" element={<ApprovalRoutingPage />} />
          <Route path="/hr/sites" element={<SitesPage />} />
          <Route path="/hr/sites/:siteId" element={<SiteDetailPage />} />
          {[...foundationPolicyRoutes, ...staffTaxonomyRoutes, ...financeRoutes].map(
            ({ path, element }) => (
              <Route key={path} path={path} element={element} />
            ),
          )}
          <Route path="/hr/attendance" element={<AttendanceProcessingPage />} />
          <Route path="/hr/attendance/processing" element={<AttendanceProcessingPage />} />
          <Route path="/hr/leaves" element={<LeaveManagementPage />} />
          <Route path="/hr/leave-management" element={<LeaveManagementPage />} />
          <Route path="/hr/leave-encashment" element={<LeaveEncashmentPage />} />
          <Route
            path="/hr/attendance/employees/:employeeId"
            element={<EmployeeAttendanceDetailPage />}
          />
          <Route path="/hr/shifts" element={<ShiftsPage />} />
          <Route path="/hr/attendance/shifts" element={<ShiftsPage />} />
          <Route path="/hr/attendance/sites" element={<SiteRostersPage />} />
          <Route path="/hr/attendance/sites/:siteId/rosters" element={<SiteRostersPage />} />
          <Route path="/hr/attendance/geofences" element={<SiteGeofencesPage />} />
          <Route path="/hr/attendance/sites/:siteId/geofences" element={<SiteGeofencesPage />} />
          <Route path="/hr/attendance/offices" element={<OfficeRostersPage />} />
          <Route path="/hr/attendance/offices/:branchId/rosters" element={<OfficeRostersPage />} />
          <Route path="/hr/attendance/office-boundaries" element={<OfficeBoundariesPage />} />
          <Route
            path="/hr/attendance/offices/:branchId/boundaries"
            element={<OfficeBoundariesPage />}
          />
          {attendanceOperationalRoutes.map(({ path, Page }) => (
            <Route
              key={path}
              path={path}
              element={
                <Suspense fallback={<LoadingState />}>
                  <Page />
                </Suspense>
              }
            />
          ))}
          {Object.entries(connectedAttendanceWorkspaces).map(([id, to]) => (
            <Route key={id} path={`/hr/workspaces/${id}`} element={<Navigate to={to} replace />} />
          ))}
          <Route path="/hr/workspaces/:workspaceId" element={<HrPreparedWorkspacePage />} />
          <Route path="/hr" element={<Navigate to="/hr/dashboard" replace />} />
        </Route>
        <Route path="/admin" element={<AdminLayout />}>
          <Route index element={<Navigate to="/admin/dashboard" replace />} />
          <Route path="dashboard" element={<AdminDashboardPage />} />
          <Route path="employees" element={<EmployeesPage />} />
          <Route path="employees/new" element={<NewEmployeePage />} />
          <Route path="employees/:employeeId/hiring" element={<EmployeeHiringPage />} />
          <Route path="employees/:employeeId" element={<EmployeeDetailPage />} />
          <Route path="employees/:employeeId/assets" element={<EmployeeAssetsPage />} />
          <Route path="employees/:employeeId/sims" element={<EmployeeSimsPage />} />
          <Route
            path="employees/:employeeId/certifications"
            element={<EmployeeCertificationsPage />}
          />
          <Route path="approvals" element={<AdminApprovalsPage />} />
          <Route path="panel" element={<AdminModulePage moduleKey="panel" />} />
          <Route path="finance" element={<AdminModulePage moduleKey="finance" />} />
          <Route path="inventory" element={<AdminModulePage moduleKey="inventory" />} />
          <Route path="procurement" element={<AdminModulePage moduleKey="procurement" />} />
          <Route path="sales" element={<AdminModulePage moduleKey="sales" />} />
          <Route path="operations" element={<AdminModulePage moduleKey="operations" />} />
          <Route path="projects" element={<AdminModulePage moduleKey="projects" />} />
          <Route path="reports" element={<AdminModulePage moduleKey="reports" />} />
          <Route path="users" element={<AdminModulePage moduleKey="users" />} />
          <Route path="departments" element={<AdminModulePage moduleKey="departments" />} />
          <Route path="settings" element={<AdminModulePage moduleKey="settings" />} />
          <Route path="attendance" element={<Navigate to="/hr/attendance" replace />} />
          <Route path="leaves" element={<Navigate to="/hr/leaves" replace />} />
          <Route path="shifts" element={<Navigate to="/hr/attendance/shifts" replace />} />
          <Route path="help" element={<AdminModulePage moduleKey="help" />} />
          <Route path="account" element={<AccountPage />} />
        </Route>
      </Route>
      <Route path="/" element={<RootRedirect />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
