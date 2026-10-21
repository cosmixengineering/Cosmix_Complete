using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Audit;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Reports;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.SiteReports;
using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Allowances;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.HR.Reports;

public sealed record HrReportAccess(AuthenticatedAccount Identity, IReadOnlyList<PermissionGrant> Grants,
    IReadOnlyList<string> RequiredPermissions, DateTimeOffset Now)
{
    public bool HasAllScope => RequiredPermissions.Count > 0 && RequiredPermissions.All(permission =>
        AccessPermissionEvaluator.HasAllScope(Identity, Grants, permission));

    public bool Allows(IReadOnlyList<AttendanceResourceScope> scopes) => RequiredPermissions.Count > 0 &&
        RequiredPermissions.All(permission => AttendanceScopeEvaluation.Allows(Identity, Grants, permission, scopes));
}

// This selection is resolved on the server inside the authorization transaction, never from an HTTP body.
public sealed record HrReportSelection(IReadOnlyList<Guid> EmployeeIds, bool IncludeUnscopedAudit)
{
    public IReadOnlyList<Guid> ReadRoleAssignmentIds { get; init; } = [];
    public IReadOnlyList<AttendanceResourceScope> ScopeGroups { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    internal List<AttendanceResourceScope> AdditionalScopeGroups { get; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    internal bool FullExport { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    internal HrReportAccess? Access { get; set; }
}

public sealed class HrReportAuthorization(ICurrentAuthentication current, IAccountAccessReader access,
    IAccessManagementRepository authorization, ISchedulingJournalRepository journal,
    IHrReportRepository reports, IPrivateFileStore files, TimeProvider clock)
{
    public async Task<T> ReadAsync<T>(string reportType, HrReportFilter filter, bool export, string? reason,
        AuthRequestContext context, Func<HrReportSelection, CancellationToken, Task<T>> action, CancellationToken ct,
        Func<HrReportAccess, CancellationToken, Task<HrReportSelection?>>? retainedSelection = null,
        Func<CancellationToken, Task>? revalidateAfterAudit = null)
    {
        var identity = current.Require();
        if (identity.Client != SessionClient.Web || !identity.MfaSatisfied) throw AttendanceOperation.Denied();
        var required = RequiredPermissions(reportType, export);
        if (export && (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 || reason.Any(char.IsControl)))
            throw AttendanceOperation.Invalid("invalid_hr_report_reason");
        var authorizedAt = clock.GetUtcNow();
        var completed = await authorization.InTransactionAsync(async token =>
        {
            var allowed = await CurrentAccessAsync(identity, required, token);
            var selection = (retainedSelection is null ? null : await retainedSelection(allowed, token))
                ?? await reports.SelectScopeAsync(reportType, filter, allowed, token);
            var readGrants = allowed.Grants.Where(x => x.Permission == HrReportPermissions.Read).ToArray();
            if (selection.EmployeeIds.Any(id => id == Guid.Empty) ||
                selection.EmployeeIds.Distinct().Count() != selection.EmployeeIds.Count ||
                selection.ReadRoleAssignmentIds.Any(id => !readGrants.Any(g => g.RoleAssignmentId == id)) ||
                selection.ReadRoleAssignmentIds.Distinct().Count() != selection.ReadRoleAssignmentIds.Count ||
                (selection.EmployeeIds.Count > 0 || selection.IncludeUnscopedAudit) && selection.ReadRoleAssignmentIds.Count == 0 ||
                selection.IncludeUnscopedAudit && (reportType != HrReportTypes.Audit || !allowed.HasAllScope ||
                    filter.EmployeeId.HasValue || filter.DepartmentId.HasValue || filter.SiteId.HasValue || filter.BranchId.HasValue))
                throw AttendanceOperation.Conflict("hr_report_scope_invalid");
            // The basis must contribute to this selection; a role for another site is not its authority.
            var basis = readGrants.FirstOrDefault(g => selection.IncludeUnscopedAudit
                ? selection.ReadRoleAssignmentIds.Contains(g.RoleAssignmentId) &&
                    AccessPermissionEvaluator.HasAllScope(identity, [g], HrReportPermissions.Read)
                : selection.EmployeeIds.Count == 0 || selection.ReadRoleAssignmentIds.Contains(g.RoleAssignmentId))
                ?? throw AttendanceOperation.Denied();
            var role = await authorization.GetAssignmentRoleCodeAsync(basis.RoleAssignmentId, token)
                ?? throw AttendanceOperation.Denied();
            selection.Access = allowed;
            var result = await action(selection, token);
            var operation = export ? "hr.report.export" : "hr.report.read";
            var evidence = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new
                { reportType, filter, reason, selection, required }),
                $"hr:report:audit:{identity.AccountId:N}", token);
            // Effective resource membership can expire while grants remain unchanged.
            if (revalidateAfterAudit is not null) await revalidateAfterAudit(token);
            // Revalidate after report generation/private IO; a token alone is not current authority.
            var rechecked = await CurrentAccessAsync(identity, required, token);
            if (GrantFingerprint(allowed) != GrantFingerprint(rechecked)) throw AttendanceOperation.Denied();
            journal.AddAudit(new AuditEvent(operation, identity.AccountId.ToString(), role, "HrReport",
                reportType, clock.GetUtcNow(), context.CorrelationId, employeeScopeId: filter.EmployeeId,
                protectedChangesReference: AttendanceOperation.Reference(evidence), ipAddress: context.IpAddress,
                deviceReference: identity.SessionId.ToString()));
            RequireBusinessDate(authorizedAt);
            await journal.SaveChangesAsync(token);
            RequireBusinessDate(authorizedAt);
            return result;
        }, ct);
        // Commit IO may cross midnight after the last check inside the transaction.
        RequireBusinessDate(authorizedAt);
        return completed;
    }

    internal void RequireBusinessDate(DateTimeOffset authorizedAt)
    {
        if (RosterSchedule.Today(authorizedAt) != RosterSchedule.Today(clock.GetUtcNow()))
            throw AttendanceOperation.Denied();
    }

    internal async Task<HrReportAccess> CurrentAccessAsync(AuthenticatedAccount identity,
        IReadOnlyList<string> required, CancellationToken ct)
    {
        if (identity.Client != SessionClient.Web || !identity.MfaSatisfied) throw AttendanceOperation.Denied();
        if (!await journal.IsCurrentSessionAsync(identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        var now = clock.GetUtcNow();
        var grants = AccessPermissionEvaluator.ForSession(identity,
            (await access.GetGrantsAsync(identity.AccountId, now, ct)).Where(x => x.IsEffectiveAt(now)))
            .Where(x => required.Contains(x.Permission)).ToArray();
        if (required.Any(permission => !grants.Any(x => x.Permission == permission))) throw AttendanceOperation.Denied();
        return new(identity, grants, required, now);
    }

    internal async Task<HrReportSelection> RequireExportAsync(HrReportExportScope scope,
        AuthenticatedAccount actor, CancellationToken ct, bool includeSources = false)
    {
        HrReportExportRules.ValidateScope(scope);
        if (actor.AccountId != scope.Requester.AccountId || actor.EmployeeId != scope.Requester.EmployeeId)
            throw AttendanceOperation.Denied();
        var allowed = await CurrentAccessAsync(actor, RequiredPermissions(scope.ReportType, true), ct);
        // Retained groups preserve historical alternatives even after assignments or sources change.
        if (scope.Selection.IncludeUnscopedAudit && !allowed.HasAllScope ||
            scope.Selection.ScopeGroups.Any(group => !allowed.Allows([group])))
            throw AttendanceOperation.Denied();
        var selected = await reports.SelectRetainedScopeAsync(scope, allowed, includeSources, ct);
        // The repository resolves current resources using allowed.Now, not query completion time.
        RequireBusinessDate(allowed.Now);
        // Scheduled grants can expire during scope IO without a write or a business-date change.
        var completedAt = clock.GetUtcNow();
        if (allowed.Grants.Any(grant => !grant.IsEffectiveAt(completedAt)))
            throw AttendanceOperation.Denied();
        if (scope.Selection.EmployeeIds.Except(selected.EmployeeIds).Any() ||
            scope.Selection.IncludeUnscopedAudit && !selected.IncludeUnscopedAudit)
            throw AttendanceOperation.Denied();
        return new(scope.Selection.EmployeeIds, scope.Selection.IncludeUnscopedAudit)
        {
            ReadRoleAssignmentIds = selected.ReadRoleAssignmentIds,
            ScopeGroups = selected.ScopeGroups.Where(g => scope.Selection.EmployeeIds.Contains(g.Alternatives[0].EmployeeId!.Value)).ToArray(),
            Access = allowed,
            FullExport = true
        };
    }

    private static string GrantFingerprint(HrReportAccess access) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(access.Grants.OrderBy(x => x.Permission, StringComparer.Ordinal)
            .ThenBy(x => x.RoleAssignmentId).ThenBy(x => x.ScopeKind).ThenBy(x => x.TargetId)
            .Select(x => new { x.Permission, x.Sensitive, x.RoleAssignmentId, x.ScopeKind, x.TargetId,
                x.AuthorityTimingKnown, x.ValidFromUtc, x.ValidUntilUtc }).ToArray())));

    public static IReadOnlyList<string> RequiredPermissions(string reportType, bool export)
    {
        string[] sourcePermissions = reportType switch
        {
            HrReportTypes.DailyAttendance or HrReportTypes.MonthlyAttendanceRegister => [AttendanceReportPermissions.Read],
            HrReportTypes.PunchAudit => [AttendanceAuditPermissions.Read],
            HrReportTypes.OperationalZones => [ZonePermissions.Review],
            HrReportTypes.SiteReporting => [SiteReportPermissions.Read],
            HrReportTypes.LateKpi => [KpiPermissions.Read, AttendancePayrollInputPermissions.Read, HrReportPermissions.DisciplinaryRead],
            HrReportTypes.Overtime => [OvertimePermissions.Review, OvertimePermissions.FinancialRead],
            HrReportTypes.MealAllowance => [AllowancePermissions.Review, AllowancePermissions.Settlement],
            HrReportTypes.Leave => [LeavePermissions.Review],
            HrReportTypes.EmployeeMaster => [EmployeePermissions.Read, EmployeeUpdatePermissions.IdentityRead],
            HrReportTypes.DocumentExpiry => [EmployeePermissions.Read, EmployeeUpdatePermissions.IdentityRead, EmployeePermissions.DocumentsRead,
                EmployeePermissions.MedicalRead, EmployeeRecordPermissions.CertificationsRead],
            HrReportTypes.FuelMileage => [VehicleFuelPermissions.Fuel],
            HrReportTypes.PayrollHandoff => [PayrollHandoffPermissions.Read],
            HrReportTypes.Audit => [HrReportPermissions.AuditRead],
            _ => throw AttendanceOperation.Invalid("invalid_hr_report_type")
        };
        return export ? [HrReportPermissions.Read, HrReportPermissions.Export, .. sourcePermissions]
            : [HrReportPermissions.Read, .. sourcePermissions];
    }

    public static HrReportFilter Normalize(string reportType, HrReportFilter filter, DateTimeOffset now)
    {
        if (filter.Page < 1 || filter.PageSize is < 1 or > 1000 || (long)filter.Page * filter.PageSize > int.MaxValue ||
            filter.Year is < 1 or > 9998 || filter.Month is < 1 or > 12 ||
            new[] { filter.EmployeeId, filter.DepartmentId, filter.SiteId, filter.BranchId, filter.VehicleId,
                filter.ZoneId, filter.LeaveTypeId }.Any(x => x == Guid.Empty))
            throw AttendanceOperation.Invalid("invalid_hr_report_filter");
        var today = RosterSchedule.Today(now);
        var date = filter.Date ?? (reportType == HrReportTypes.DocumentExpiry ? filter.To ?? today.AddDays(90) : filter.From ?? today);
        var year = filter.Year ?? date.Year;
        var month = filter.Month ?? date.Month;
        if (year is < 1 or > 9998) throw AttendanceOperation.Invalid("invalid_hr_report_range");
        var from = filter.From ?? today.AddDays(reportType == HrReportTypes.PunchAudit ? -7 : -30);
        var to = filter.To ?? today;
        if (reportType is HrReportTypes.MonthlyAttendanceRegister or HrReportTypes.PayrollHandoff)
        { from = new(year, month, 1); to = from.AddMonths(1).AddDays(-1); }
        else if (reportType is HrReportTypes.Leave or HrReportTypes.LateKpi)
        { from = new(year, 1, 1); to = new(year, 12, 31); }
        else if (reportType == HrReportTypes.DailyAttendance) from = to = date;
        else if (reportType is HrReportTypes.EmployeeMaster or HrReportTypes.DocumentExpiry) from = to = today;
        if (from == default || to == DateOnly.MaxValue || date == default || date == DateOnly.MaxValue ||
            to < from || to.DayNumber - from.DayNumber + 1 > 366)
            throw AttendanceOperation.Invalid("invalid_hr_report_range");
        return filter with { Date = date, From = from, To = to, Year = year, Month = month };
    }
}
