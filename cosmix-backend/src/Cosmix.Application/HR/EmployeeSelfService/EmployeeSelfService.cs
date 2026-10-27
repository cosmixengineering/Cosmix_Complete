using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.HR.EmployeeSelfService;

public sealed partial class EmployeeSelfService(
    IEmployeeSelfServiceRepository repository,
    ICurrentAuthentication current,
    IAccountAccessReader accessReader,
    TimeProvider clock,
    IAccessManagementRepository authorization,
    ISchedulingJournalRepository journal,
    LeaveService? leaves = null,
    AttendanceSummaryService? attendance = null,
    IEmployeePayrollHistoryReader? payrollHistory = null,
    Cosmix.Application.HR.Payslips.PayslipService? payslips = null)
{
    public Task<EssProfileResponse> GetProfileAsync(AuthRequestContext context, CancellationToken ct) =>
        ReadAuditedAsync(EmployeeSelfServicePermissions.ProfileRead, context,
            async (employeeId, token) => await repository.GetProfileAsync(employeeId, token)
                ?? throw new AuthenticationFailure("employee_not_found", 404), ct);

    public async Task<EssAttendanceCalendarResponse> GetAttendanceCalendarAsync(int? year, int? month,
        AuthRequestContext context, CancellationToken ct)
    {
        var identity = current.Require();
        await EnsurePermissionAsync(identity, EmployeeSelfServicePermissions.AttendanceRead, ct);

        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            throw AttendanceOperation.Invalid("invalid_ess_calendar_month");
        var today = RosterSchedule.Today(clock.GetUtcNow());
        var targetYear = year ?? today.Year;
        var targetMonth = month ?? today.Month;
        var first = new DateOnly(targetYear, targetMonth, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var source = attendance ?? throw AttendanceOperation.Conflict("ess_attendance_reader_unavailable");
        var days = await source.CalendarAsync(null, new(first, last), true, context, ct);
        await EnsurePermissionAsync(identity, EmployeeSelfServicePermissions.AttendanceRead, ct);

        // Keep the authoritative day payload, including locked evidence and per-date roster pins.
        // Incomplete classification cannot turn into zero absences, rest days or overtime.
        await EnsurePermissionAsync(identity, AttendanceSummaryPermissions.Self, ct, allowGenericSelf: false);
        var complete = days.All(x => x.Policy.Status == "calculated" && x.Policy.DayStatus.HasValue &&
            x.Policy.DayStatus is not (AttendanceDayStatus.Unverified or AttendanceDayStatus.Incomplete) &&
            x.Policy.LateMinutes.HasValue && x.Policy.OvertimeMinutes.HasValue);
        int? Count(params AttendanceDayStatus[] statuses) => complete
            ? days.Count(x => statuses.Contains(x.Policy.DayStatus!.Value)) : null;
        return new(targetYear, targetMonth, complete ? "available" : "incomplete", days.Count,
            Count(AttendanceDayStatus.Present, AttendanceDayStatus.HalfDay, AttendanceDayStatus.Hourly),
            Count(AttendanceDayStatus.Absent), Count(AttendanceDayStatus.Leave), Count(AttendanceDayStatus.Holiday), Count(AttendanceDayStatus.RestDay),
            complete ? days.Count(x => x.Policy.LateMinutes > 0) : null,
            days.Sum(x => x.Facts.WorkedMinutes), complete ? days.Sum(x => x.Policy.OvertimeMinutes!.Value) : null, days);
    }

    public async Task<EssLeaveOverviewResponse> GetLeaveOverviewAsync(AuthRequestContext context, CancellationToken ct)
    {
        var identity = current.Require();
        await EnsurePermissionAsync(identity, EmployeeSelfServicePermissions.LeavesRead, ct);

        var source = leaves ?? throw AttendanceOperation.Conflict("ess_leave_reader_unavailable");
        var today = RosterSchedule.Today(clock.GetUtcNow());
        var types = await source.TypesAsync(true, context, ct);
        var balances = new List<EssLeaveBalanceSummary>();
        foreach (var type in types.Where(x => x.IsActive))
        {
            var policies = await source.SelfPoliciesAsync(new(type.Id, today, today), context, ct);
            LeaveSelfBalanceResponse? balance = null;
            var status = policies.Status;
            var reasons = policies.Reasons;
            if (policies.Status == "available" && policies.Policies.Count == 1)
            {
                balance = await source.BalanceAsync(new(type.Id, policies.Policies[0].VersionId, today), context, ct);
                status = balance.Status == "available" && balance.Reasons.Count > 0 ? "conditional" : balance.Status;
                reasons = balance.Reasons;
            }
            else if (policies.Policies.Count > 1)
            {
                status = "selection_required";
                reasons = ["leave_policy_selection_required"];
            }
            balances.Add(new(type.Id, type.Code, type.Name, status, balance, policies.Policies, reasons));
        }
        var selection = await repository.GetLeaveRequestIdsAsync(identity.EmployeeId, today, ct);
        var details = new Dictionary<Guid, LeaveResponse>();
        foreach (var id in selection.Recent.Concat(selection.Upcoming).Distinct())
            details.Add(id, await source.DetailAsync(id, true, context, ct));
        var recent = selection.Recent.Select(id => details[id]).ToArray();
        var upcoming = selection.Upcoming.Select(id => details[id]).Where(x =>
            x.State == ApprovalState.Approved && x.SupersededByRequestId is null &&
            x.FinancialStatus != "cancelled_no_pay_effect").ToArray();
        await EnsurePermissionAsync(identity, EmployeeSelfServicePermissions.LeavesRead, ct);
        await EnsurePermissionAsync(identity, LeavePermissions.Self, ct, allowGenericSelf: false);
        return new(today, balances.Count == 0 ? "unavailable" : balances.All(x => x.Status == "available") ? "available" : "incomplete",
            balances, recent, upcoming);
    }

    public async Task<LeaveSelfBalanceResponse> GetLeaveBalanceAsync(LeaveSelfBalanceRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        var identity = current.Require();
        await EnsurePermissionAsync(identity, EmployeeSelfServicePermissions.LeavesRead, ct);
        var source = leaves ?? throw AttendanceOperation.Conflict("ess_leave_reader_unavailable");
        var result = await source.BalanceAsync(request, context, ct);
        await EnsurePermissionAsync(identity, EmployeeSelfServicePermissions.LeavesRead, ct);
        await EnsurePermissionAsync(identity, LeavePermissions.Self, ct, allowGenericSelf: false);
        return result;
    }

    public Task<EssFinancialOverviewResponse> GetFinancialOverviewAsync(AuthRequestContext context, CancellationToken ct) =>
        ReadFinancialAsync(repository.GetFinancialOverviewAsync, context, ct);

    public Task<PagedResult<EssPayrollHandoffSummary>> GetPayrollHistoryAsync(int page, int pageSize,
        AuthRequestContext context, CancellationToken ct)
    {
        if (page is < 1 or > 4096 || pageSize is < 1 or > 100) throw AttendanceOperation.Invalid("invalid_page");
        return ReadFinancialAsync((employeeId, token) =>
            (payrollHistory ?? throw AttendanceOperation.Conflict("ess_payroll_reader_unavailable"))
                .GetPayrollHistoryAsync(employeeId, page, pageSize, token), context, ct);
    }

    private async Task<T> ReadFinancialAsync<T>(Func<Guid, CancellationToken, Task<T>> read,
        AuthRequestContext context, CancellationToken ct)
    {
        var identity = current.Require();
        if (identity.EmployeeId == Guid.Empty)
            throw new AuthenticationFailure("unlinked_employee_account", 401);

        return await authorization.InTransactionAsync(async token =>
        {
            await RequireFinancialAuthorityAsync(identity, token);
            var result = await read(identity.EmployeeId, token);
            var role = await RequireFinancialAuthorityAsync(identity, token);
            journal.AddAudit(new AuditEvent(EmployeeSelfServicePermissions.FinancesRead,
                identity.AccountId.ToString(), role, "EmployeeSelfService", identity.EmployeeId.ToString(),
                clock.GetUtcNow(), context.CorrelationId, employeeScopeId: identity.EmployeeId,
                ipAddress: context.IpAddress, deviceReference: identity.SessionId.ToString()));
            await journal.SaveChangesAsync(token);
            await RequireFinancialAuthorityAsync(identity, token);
            return result;
        }, ct);
    }

    private async Task<string> RequireFinancialAuthorityAsync(AuthenticatedAccount identity, CancellationToken ct)
    {
        if (current.Require() != identity || !await journal.IsCurrentSessionAsync(identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        var grants = await accessReader.GetGrantsAsync(identity.AccountId, clock.GetUtcNow(), ct);
        var own = new AccessResource(EmployeeId: identity.EmployeeId);
        var checkedAt = clock.GetUtcNow();
        var basis = grants.FirstOrDefault(grant => grant.IsEffectiveAt(checkedAt) && AccessPermissionEvaluator.Allows(identity, [grant],
            EmployeeSelfServicePermissions.FinancesRead, own));
        if (basis is null) throw new AuthenticationFailure("forbidden", 403);
        return await authorization.GetAssignmentRoleCodeAsync(basis.RoleAssignmentId, ct)
            ?? throw new AuthenticationFailure("forbidden", 403);
    }

    public Task<EssRequestsSummaryResponse> GetRequestsSummaryAsync(AuthRequestContext context, CancellationToken ct) =>
        ReadAuditedAsync(EmployeeSelfServicePermissions.RequestsRead, context, repository.GetRequestsSummaryAsync, ct);

    private Task<T> ReadAuditedAsync<T>(string permission, AuthRequestContext context,
        Func<Guid, CancellationToken, Task<T>> read, CancellationToken ct)
    {
        var identity = current.Require();
        return authorization.InTransactionAsync(async token =>
        {
            await EnsurePermissionAsync(identity, permission, token);
            var result = await read(identity.EmployeeId, token);
            var basis = await EnsurePermissionAsync(identity, permission, token);
            var role = await authorization.GetAssignmentRoleCodeAsync(basis.RoleAssignmentId, token)
                ?? throw new AuthenticationFailure("forbidden", 403);
            journal.AddAudit(new AuditEvent(permission, identity.AccountId.ToString(), role,
                "EmployeeSelfService", identity.EmployeeId.ToString(), clock.GetUtcNow(), context.CorrelationId,
                employeeScopeId: identity.EmployeeId, ipAddress: context.IpAddress,
                deviceReference: identity.SessionId.ToString()));
            await journal.SaveChangesAsync(token);
            await EnsurePermissionAsync(identity, permission, token);
            return result;
        }, ct);
    }

    private async Task<PermissionGrant> EnsurePermissionAsync(AuthenticatedAccount identity, string permission, CancellationToken ct,
        bool allowGenericSelf = true)
    {
        if (identity.EmployeeId == Guid.Empty)
            throw new AuthenticationFailure("unlinked_employee_account", 401);

        if (current.Require() != identity || !await journal.IsCurrentSessionAsync(identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);

        var grants = await accessReader.GetGrantsAsync(identity.AccountId, clock.GetUtcNow(), ct);
        var resource = new AccessResource(EmployeeId: identity.EmployeeId);

        var basis = grants.FirstOrDefault(grant => AccessPermissionEvaluator.Allows(identity, [grant], permission, resource))
            ?? (allowGenericSelf ? grants.FirstOrDefault(grant => AccessPermissionEvaluator.Allows(identity, [grant],
                AccessPermissionCatalog.SelfRead, resource)) : null);
        return basis ?? throw new AuthenticationFailure("forbidden", 403);
    }
}
