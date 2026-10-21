using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.Allowances;
using Cosmix.Application.HR.AdvancesLoans;
using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.Compensation;
using Cosmix.Application.HR.Policies;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.Reports;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.SiteReports;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Attendance.Punches.CaptureValidation;
using Cosmix.Application.Attendance.Reports;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.Audit;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.LegalReview;
using Cosmix.Application.HR.CompanySettings;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Application.HR.FoundationPolicies;
using Cosmix.Application.Common.Approvals;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Attendance.Waiting;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Application.HR.EmployeeSelfService;

namespace Cosmix.Application.Authentication.Access;

public static class AccessPermissionCatalog
{
    public const string SelfRead = "auth.self.read";
    public const string SelfSessionsManage = "auth.self.sessions.manage";
    public const string RolesRead = "access.roles.read";
    public const string RolesCreate = "access.roles.create";
    public const string RolePermissionsManage = "access.roles.permissions.manage";
    public const string AccountsRead = "access.accounts.read";
    public const string AccountsAssign = "access.accounts.assign";
    public const string AccountsMobileManage = "access.accounts.mobile.manage";
    public const string AccountsWebManage = "access.accounts.web.manage";

    public static IReadOnlyList<AccessPermissionDefinition> All { get; } = Array.AsReadOnly<AccessPermissionDefinition>([
        .. ApprovalPermissions.Definitions,
        new(EmployeeBankPermissions.Verify, "Verify or revoke current bank verification with private evidence and immutable history", true, true, false),
        new(EmployeeDocumentPermissions.Expire, "Expire private employee documents with dated history", true, true, false),
        new(EmployeeDocumentPermissions.Revoke, "Revoke private employee documents with reason and history", true, true, false),
        new(EmployeeDocumentPermissions.Archive, "Archive private employee documents without deleting retained evidence", true, true, false),
        new(EmployeeLifecyclePermissions.Archive, "Archive or unarchive inactive employee records without restoring access; fixed Admin required", true, true, false),
        new(EmployeeRecordPermissions.AssetsRead, "Read employee asset custody and retained history", true, true, false),
        new(EmployeeRecordPermissions.AssetsRequest, "Request employee asset issue, return or transfer", true, true, false),
        new(EmployeeRecordPermissions.AssetsReview, "Review employee asset operations; fixed Admin required", true, true, false),
        new(EmployeeRecordPermissions.SimsRead, "Read employee SIM assignments and retained history", true, true, false),
        new(EmployeeRecordPermissions.SimsRequest, "Request employee SIM issue, return or transfer", true, true, false),
        new(EmployeeRecordPermissions.SimsReview, "Review employee SIM operations; fixed Admin required", true, true, false),
        new(EmployeeRecordPermissions.CertificationsRead, "Read employee certification and licence history", true, true, false),
        new(EmployeeRecordPermissions.CertificationsRequest, "Request employee certification issue, renewal or status changes", true, true, false),
        new(EmployeeRecordPermissions.CertificationsReview, "Review employee certification operations; fixed Admin required", true, true, false),
        .. SchedulingPermissions.Definitions,
        .. AttendancePolicyPermissions.Definitions,
        .. KpiPermissions.Definitions,
        .. KpiDisputePermissionDefs.Definitions,
        .. WaitingPermissions.Definitions,
        .. WorkTreatmentPermissionDefs.Definitions,
        .. LeavePermissions.Definitions,
        .. LeaveTeamCalendarPermissions.Definitions,
        .. LeaveBenefitPermissions.Definitions,
        .. SiteReportPermissions.Definitions,
        .. AttendanceSummaryPermissions.Definitions,
        .. AttendanceCapturePermissions.Definitions,
        .. CaptureValidationPermissions.Definitions,
        .. AttendanceReportPermissions.Definitions,
        .. HrReportPermissions.Definitions,
        .. CorrectionPermissions.Definitions,
        .. AttendanceExceptionPermissions.Definitions,
        .. AttendanceAuditPermissions.Definitions,
        .. MonthLockPermissions.Definitions,
        .. OvertimePermissions.Definitions,
        .. AllowancePermissions.Definitions,
        new(DeploymentService.ReadPermission, "Read scoped outstation trips, home breaks and retained calculations", true, true, false),
        new(DeploymentService.RequestPermission, "Record scoped outstation trip, home-break and allowance requests", false, true, false),
        new(DeploymentService.DecidePermission, "Approve outstation trips, home breaks and claims as an explicitly authorized Admin", true, true, false),
        .. PayrollHandoffPermissions.Definitions,
        .. AdvanceLoanPermissions.Definitions,
        .. VehicleFuelPermissions.Definitions,
        .. VehiclePolicyPermissions.Definitions,
        .. EmployeeSelfServicePermissions.Definitions,
        .. ZonePermissions.Definitions,
        .. AttendancePayrollInputPermissions.Definitions,
        .. AttendancePayrollAttestationPermissions.Definitions,
        .. AttendanceLegalReviewPermissions.Definitions,
        .. CompanySettingsPermissions.Definitions,
        .. HrFoundationPolicyPermissions.Definitions,
        new(StaffTaxonomyPermissions.Read, "Read retained staff taxonomy policies and publication requests", true, true, false),
        new(StaffTaxonomyPermissions.Request, "Request approved staff taxonomy publication without changing existing history", true, true, false),
        new(StaffTaxonomyPermissions.Review, "Review staff taxonomy publication under configured authority", true, true, false),
        new(EmployeePermissions.Read, "Read confidential hiring profiles and employee directory", true, true, false),
        new(EmployeeUpdatePermissions.BasicEdit, "Update approved basic employee details with audit history", true, true, false),
        new(EmployeeUpdatePermissions.Request, "Request approved employee detail changes and transfers", true, true, false),
        new(EmployeeUpdatePermissions.Review, "Decide employee changes; fixed Admin/SuperAdmin required", true, true, false),
        new(EmployeeUpdatePermissions.IdentityRead, "Read restricted current identity and identity change history", true, true, false),
        new(EmployeeUpdatePermissions.IdentityRequest, "Request legal identity and contact changes, not login credentials", true, true, false),
        new(EmployeeUpdatePermissions.BankRead, "Read restricted current employee bank details and history", true, true, false),
        new(EmployeeUpdatePermissions.BankRequest, "Request bank detail changes without marking them verified", true, true, false),
        new(EmployeePermissions.Edit, "Create and edit employee hiring drafts", true, true, false),
        new(EmployeePermissions.Submit, "Submit completed hiring forms for Admin review", true, true, false),
        new(EmployeePermissions.Review, "Read hiring submission queue; fixed Admin/SuperAdmin required", true, true, false),
        new(EmployeePermissions.FinancialRead, "Read hiring salary and bank proposals", true, true, false),
        new(EmployeePermissions.FinancialEdit, "Edit hiring salary and bank proposals", true, true, false),
        new(EmployeePermissions.MedicalRead, "Read authorized hiring medical records", true, true, false),
        new(EmployeePermissions.MedicalEdit, "Edit authorized hiring medical records", true, true, false),
        new(EmployeePermissions.DocumentsRead, "Download verified private employee documents", true, true, false),
        new(EmployeePermissions.DocumentsManage, "Upload and rescan private hiring documents", true, true, false),
        new(EmployeePermissions.DocumentsVerify, "Review clean hiring documents and record verification", true, true, false),
        new(EmployeeLifecyclePermissions.Read, "Read confidential employee status requests and history", true, true, false),
        new(EmployeeLifecyclePermissions.Request, "Request staff suspension, resignation or termination", true, true, false),
        new(EmployeeLifecyclePermissions.Review, "Decide staff status requests; fixed Admin/SuperAdmin required", true, true, false),
        new(EmployeeCompensationPermissions.Read, "Read restricted effective salary, change requests and history", true, true, false),
        new(EmployeeCompensationPermissions.Request, "Submit salary changes for Admin approval", true, true, false),
        new(EmployeeCompensationPermissions.Review, "Decide salary changes; fixed Admin/SuperAdmin required", true, true, false),
        new(Permissions.OrganizationRead, "Read organizational reference data", false, true, false),
        new(Permissions.OrganizationManage, "Manage organization masters and their history", false, true, false),
        new(Permissions.HolidaysRead, "Read holiday calendars and notification references", false, true, false),
        new(Permissions.HolidaysManage, "Create and revise holiday calendars", false, true, false),
        new(Permissions.SitesRead, "Read site reference data; no employee assignments", false, true, false),
        new(Permissions.SitesManage, "Manage sites and history; fixed Admin/SuperAdmin assignment required", false, true, false),
        new(SelfRead, "Read own account and current assigned-site attendance configuration", false, false, true),
        new(SelfSessionsManage, "Manage own sessions and devices", false, false, true),
        new(RolesRead, "Read access roles and the permission catalog", true, true, false),
        new(RolesCreate, "Create an empty access role", true, true, false),
        new(RolePermissionsManage, "Grant permissions to an unassigned role and delegate roles", true, true, false),
        new(AccountsRead, "Read account access metadata", true, true, false),
        new(AccountsAssign, "Assign and revoke account roles", true, true, false),
        new(AccountsMobileManage, "Change mobile account access", true, true, false),
        new(AccountsWebManage, "Change web account access", true, true, false)
    ]);

    public static AccessPermissionDefinition? Find(string? code) =>
        All.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.Ordinal));

    public static AccessPermissionDefinition Require(string? code) =>
        Find(code) ?? throw new AuthenticationFailure("unsupported_permission", 400);
}
