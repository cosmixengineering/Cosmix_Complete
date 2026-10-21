using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.Reports;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Scopes;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.SiteReports;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed partial class HrReportRepository
{
    private const int MaximumCandidates = 256;
    private const int MaximumSourceRows = 8192;
    private const int MaximumReportRows = 16384;
    private const int MaximumRosterReads = 1024;
    private const int MaximumScopeAlternatives = 65536;
    private const int MaximumFootprintDays = 3660;

    // The service owns the serializable transaction and revalidates the session/grants afterwards.
    public Task<HrReportSelection> SelectScopeAsync(string reportType, HrReportFilter filter,
        HrReportAccess access, CancellationToken ct) => SelectScopeCoreAsync(reportType, filter, access, null, true, ct);

    public Task<HrReportSelection> SelectRetainedScopeAsync(HrReportExportScope scope, HrReportAccess access,
        bool includeSources, CancellationToken ct)
    {
        HrReportExportRules.ValidateScope(scope);
        return SelectScopeCoreAsync(scope.ReportType, scope.Filter, access, scope.Selection, includeSources, ct);
    }

    private async Task<HrReportSelection> SelectScopeCoreAsync(string reportType, HrReportFilter filter,
        HrReportAccess access, HrReportSelection? retained, bool includeSources, CancellationToken ct)
    {
        if (!HrReportTypes.IsValid(reportType) || filter.From is null || filter.To is null ||
            filter.Date is null || filter.Year is null || filter.Month is null)
            throw AttendanceOperation.Invalid("invalid_hr_report_filter");
        reportType = reportType.ToLowerInvariant();
        // EmployeeScopeId cannot prove the scope of arbitrary event target/actor metadata.
        if (reportType == HrReportTypes.Audit && !access.HasAllScope) throw AttendanceOperation.Denied();
        var today = RosterSchedule.Today(access.Now);
        // Retained authority is independent of mutable row eligibility and unrelated population growth.
        var candidatesQuery = retained is null
            ? FilterEmployees(reportType, filter, today, CandidateEmployees(filter, access, today))
            : db.Employees.AsNoTracking().Where(x => retained.EmployeeIds.Contains(x.Id));
        var candidates = await candidatesQuery.OrderBy(x => x.Id)
            .Select(x => x.Id).Take(MaximumCandidates + 1).ToArrayAsync(ct);
        if (candidates.Length > MaximumCandidates) throw AttendanceOperation.Conflict("hr_report_candidate_limit");

        var reads = 0;
        var alternatives = 0;
        var cache = new Dictionary<(Guid, DateOnly, DateOnly), IReadOnlyList<AttendanceResourceScope>>();
        async Task<IReadOnlyList<AttendanceResourceScope>> Scopes(Guid employee, DateOnly from, DateOnly to)
        {
            ValidateFootprint(from, to);
            if (cache.TryGetValue((employee, from, to), out var found)) return found;
            if (++reads > MaximumRosterReads) throw AttendanceOperation.Conflict("hr_report_scope_work_limit");
            var resolved = (await rosters.ResourcesAsync(employee, from, to.AddDays(1), ct))
                .Select(x => new AttendanceResourceScope(x.Alternatives)).ToArray();
            if (resolved.Length > AttendanceScopeEvaluation.MaximumScopes ||
                resolved.Sum(x => x.Alternatives.Count) > AttendanceScopeEvaluation.MaximumTotalAlternatives)
                throw AttendanceOperation.Conflict("hr_report_scope_limit");
            if (!AttendanceScopeEvaluation.IsValid(resolved) ||
                resolved.Any(x => x.Alternatives.Any(r => r.EmployeeId != employee)))
                throw AttendanceOperation.Conflict("hr_report_scope_invalid");
            alternatives += resolved.Sum(x => x.Alternatives.Count);
            if (alternatives > MaximumScopeAlternatives) throw AttendanceOperation.Conflict("hr_report_scope_work_limit");
            cache.Add((employee, from, to), resolved);
            return resolved;
        }

        var current = new List<Guid>();
        foreach (var employee in candidates)
            if (access.Allows(await Scopes(employee, today, today))) current.Add(employee);

        var sources = new List<ReportSource>();
        if (includeSources && current.Count > 0)
            foreach (var query in SourceQueries(reportType, filter, current.ToArray()))
            {
                var rows = await query.Take(MaximumSourceRows + 1).ToArrayAsync(ct);
                if (rows.Length > MaximumSourceRows || sources.Count + rows.Length > MaximumSourceRows)
                    throw AttendanceOperation.Conflict("hr_report_source_limit");
                sources.AddRange(rows);
            }

        var selected = new List<Guid>();
        var byEmployee = sources.ToLookup(x => x.EmployeeId);
        foreach (var employee in current)
        {
            var footprint = byEmployee[employee].Distinct().ToArray();
            var from = filter.From.Value;
            var to = filter.To.Value;
            foreach (var source in footprint)
            {
                ValidateFootprint(source.From, source.To);
                if (source.From < from) from = source.From;
                if (source.To > to) to = source.To;
            }
            // A contiguous superset also protects intermediate transfers between source periods.
            if (!access.Allows(await Scopes(employee, from, to))) continue;
            var allowed = true;
            foreach (var source in footprint.Where(x => x.HasSite))
            {
                var historical = await Scopes(employee, source.From, source.To);
                if (!AllowsOriginalSite(access, employee, source.SiteId, historical)) { allowed = false; break; }
            }
            if (allowed) selected.Add(employee);
        }
        var includeUnscoped = reportType == HrReportTypes.Audit && access.HasAllScope &&
            (retained?.IncludeUnscopedAudit ?? !HasEmployeeFilters(filter));
        var selectedIds = selected.ToHashSet();
        var protectedResources = cache.Where(x => selectedIds.Contains(x.Key.Item1))
            .SelectMany(x => x.Value).SelectMany(x => x.Alternatives)
            .Concat(sources.Where(x => selectedIds.Contains(x.EmployeeId) && x.HasSite)
                .Select(x => new AccessResource(EmployeeId: x.EmployeeId, SiteId: x.SiteId))).Distinct().ToArray();
        var readRoles = access.Grants.Where(g => g.Permission == HrReportPermissions.Read &&
                (includeUnscoped && AccessPermissionEvaluator.HasAllScope(access.Identity, [g], HrReportPermissions.Read) ||
                 protectedResources.Any(r => AccessPermissionEvaluator.Allows(access.Identity, [g], HrReportPermissions.Read, r))))
            .Select(g => g.RoleAssignmentId).Distinct().ToArray();
        var protectedGroups = cache.Where(x => selectedIds.Contains(x.Key.Item1)).SelectMany(x => x.Value)
            .Concat(sources.Where(x => selectedIds.Contains(x.EmployeeId) && x.HasSite)
                .Where(x => !cache[(x.EmployeeId, x.From, x.To)].All(g => g.Alternatives.All(r => r.SiteId == x.SiteId)))
                .Select(x => new AttendanceResourceScope([new AccessResource(EmployeeId: x.EmployeeId, SiteId: x.SiteId)])))
            .ToArray();
        return new(selected.ToArray(), includeUnscoped) { ReadRoleAssignmentIds = readRoles, ScopeGroups = protectedGroups };
    }

    private IQueryable<Employee> CandidateEmployees(HrReportFilter filter, HrReportAccess access, DateOnly today)
    {
        var query = db.Employees.AsNoTracking();
        if (filter.EmployeeId.HasValue) query = query.Where(x => x.Id == filter.EmployeeId.Value);
        if (access.RequiredPermissions.Count == 0) return query.Where(_ => false);
        foreach (var permission in access.RequiredPermissions.Distinct())
        {
            var grants = access.Grants.Where(g => g.Permission == permission).ToArray();
            if (grants.Any(g => g.ScopeKind == AccessScopeKind.All)) continue;
            var employees = grants.Where(g => g.ScopeKind is AccessScopeKind.Employee or AccessScopeKind.Self).Select(g => g.TargetId).ToArray();
            var departments = grants.Where(g => g.ScopeKind == AccessScopeKind.Department).Select(g => g.TargetId).ToArray();
            var sites = grants.Where(g => g.ScopeKind == AccessScopeKind.Site).Select(g => g.TargetId).ToArray();
            var teams = grants.Where(g => g.ScopeKind == AccessScopeKind.Team).Select(g => g.TargetId).ToArray();
            var projects = grants.Where(g => g.ScopeKind == AccessScopeKind.Project).Select(g => g.TargetId).ToArray();
            // Team/project existence is only a SQL superset. ResourcesAsync owns timing, termination and activity.
            query = query.Where(e => employees.Contains(e.Id) ||
                db.EmployeeAssignments.Any(a => a.EmployeeId == e.Id && a.EffectiveFrom <= today &&
                    (a.EffectiveTo == null || a.EffectiveTo > today) &&
                    (departments.Contains(a.DepartmentId) || a.SiteId.HasValue && sites.Contains(a.SiteId))) ||
                db.Set<TeamMember>().Any(m => m.EmployeeId == e.Id && teams.Contains(m.TeamId)) ||
                db.Set<ProjectMember>().Any(m => m.EmployeeId == e.Id && projects.Contains(m.ProjectId)));
        }
        return query;
    }

    private IQueryable<Employee> FilterEmployees(string type, HrReportFilter filter, DateOnly today, IQueryable<Employee> query)
    {
        // Caller filters describe the reported rows; only CandidateEmployees describes current authority.
        var sourceSite = type is HrReportTypes.PunchAudit or HrReportTypes.SiteReporting or HrReportTypes.Overtime;
        var assignmentDate = type == HrReportTypes.DailyAttendance ? filter.Date!.Value :
            type == HrReportTypes.MonthlyAttendanceRegister ? filter.To!.Value :
            type == HrReportTypes.EmployeeMaster ? filter.From!.Value : today;
        var monthStart = filter.From!.Value;
        var assignments = db.EmployeeAssignments.AsNoTracking();
        if (type == HrReportTypes.LateKpi)
        {
            if (filter.DepartmentId.HasValue || filter.BranchId.HasValue || filter.SiteId.HasValue)
                query = query.Where(e => db.PolicyImpactAssessments.Any(p => p.EmployeeId == e.Id &&
                    p.PeriodStart <= filter.To && p.PeriodEnd >= filter.From && assignments.Any(a => a.EmployeeId == e.Id &&
                        a.EffectiveFrom <= p.PeriodStart && (a.EffectiveTo == null || a.EffectiveTo > p.PeriodStart) &&
                        (!filter.DepartmentId.HasValue || a.DepartmentId == filter.DepartmentId) &&
                        (!filter.BranchId.HasValue || a.BranchId == filter.BranchId) &&
                        (!filter.SiteId.HasValue || a.SiteId == filter.SiteId))));
            return query;
        }
        assignments = assignments.Where(a => a.EffectiveFrom <= assignmentDate &&
                (a.EffectiveTo == null || (type == HrReportTypes.MonthlyAttendanceRegister ? a.EffectiveTo >= monthStart : a.EffectiveTo > assignmentDate)));
        if (filter.DepartmentId.HasValue || filter.BranchId.HasValue || filter.SiteId.HasValue && !sourceSite)
            query = from employee in query
                    let assignment = assignments.Where(a => a.EmployeeId == employee.Id).OrderByDescending(a => a.EffectiveFrom).FirstOrDefault()
                    where assignment != null &&
                        (!filter.DepartmentId.HasValue || assignment.DepartmentId == filter.DepartmentId.Value) &&
                        (!filter.BranchId.HasValue || assignment.BranchId == filter.BranchId.Value) &&
                        (sourceSite || !filter.SiteId.HasValue || assignment.SiteId == filter.SiteId.Value)
                    select employee;
        if (type is HrReportTypes.DailyAttendance or HrReportTypes.EmployeeMaster or HrReportTypes.MonthlyAttendanceRegister)
            query = query.Where(e => !e.IsArchived);
        if (type == HrReportTypes.MonthlyAttendanceRegister) query = query.Where(e => e.Status == EmployeeStatus.Active);
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var employeeStates = MatchingValues<EmployeeStatus>(filter.Status);
            if (type == HrReportTypes.EmployeeMaster) query = query.Where(e => employeeStates.Contains(e.Status));
            // Daily status is filtered after authoritative retained/latest summary selection.
            // A missing calculation is not absence, and a newer revision cannot replace a locked one here.
        }
        return query;
    }

    private static T[] MatchingValues<T>(string value) where T : struct, Enum => Enum.GetValues<T>()
        .Where(x => string.Equals(x.ToString(), value, StringComparison.OrdinalIgnoreCase)).ToArray();

    // Source instants use the same historical timezone rules as retained roster versions.
    // Intervals are [start,end): midnight belongs only to the preceding occupied day.
    private static DateOnly SourceDate(DateTimeOffset instant) => RosterSchedule.Today(instant);
    private static DateOnly LastSourceDate(DateTimeOffset start, DateTimeOffset end) =>
        end < start ? throw AttendanceOperation.Conflict("hr_report_source_interval_invalid") :
        SourceDate(end > start ? end.AddTicks(-1) : end);

    private static bool AllowsOriginalSite(HrReportAccess access, Guid employee, Guid? site,
        IReadOnlyList<AttendanceResourceScope> historical) => access.Allows(historical) &&
        (historical.All(s => s.Alternatives.All(r => r.SiteId == site)) ||
         access.Allows([new AttendanceResourceScope([new AccessResource(EmployeeId: employee, SiteId: site)])]));

    private static bool HasEmployeeFilters(HrReportFilter filter) => filter.EmployeeId.HasValue ||
        filter.DepartmentId.HasValue || filter.SiteId.HasValue || filter.BranchId.HasValue;

    private static void ValidateFootprint(DateOnly from, DateOnly to)
    {
        if (from == default || to == DateOnly.MaxValue || to < from)
            throw AttendanceOperation.Conflict("hr_report_source_interval_invalid");
        if (to.DayNumber - from.DayNumber > MaximumFootprintDays)
            throw AttendanceOperation.Conflict("hr_report_source_interval_limit");
    }

    private static void RequireReportBound(int count)
    {
        if (count > MaximumReportRows) throw AttendanceOperation.Conflict("hr_report_row_limit");
    }

    private sealed record ReportSource(Guid EmployeeId, DateOnly From, DateOnly To, Guid? SiteId, bool HasSite);

    private IEnumerable<IQueryable<ReportSource>> SourceQueries(string type, HrReportFilter filter, Guid[] employees)
    {
        var rangeStart = filter.From!.Value;
        var to = filter.To!.Value;
        var start = new DateTimeOffset(rangeStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var end = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
        var year = filter.Year!.Value;
        if (type is HrReportTypes.DailyAttendance or HrReportTypes.MonthlyAttendanceRegister)
        {
            yield return db.WorkSessions.AsNoTracking().Where(x => employees.Contains(x.EmployeeId) && x.AttendanceDate >= rangeStart && x.AttendanceDate <= to)
                .Select(x => new ReportSource(x.EmployeeId, x.AttendanceDate, x.AttendanceDate, x.SiteId, true));
            yield return db.WorkSessions.AsNoTracking().Where(x => employees.Contains(x.EmployeeId) && x.AttendanceDate >= rangeStart && x.AttendanceDate <= to)
                .Select(x => new ReportSource(x.EmployeeId, SourceDate(x.StartsAtUtc),
                    LastSourceDate(x.StartsAtUtc, x.EndsAtUtc ?? x.StartsAtUtc), x.SiteId, true));
            yield return from link in db.AttendanceSummarySessions.AsNoTracking()
                         where employees.Contains(link.EmployeeId) && link.Summary.EmployeeId == link.EmployeeId &&
                             link.Summary.AttendanceDate >= rangeStart && link.Summary.AttendanceDate <= to
                         select new ReportSource(link.EmployeeId, link.WorkSession.AttendanceDate,
                             link.WorkSession.AttendanceDate, link.WorkSession.SiteId, true);
        }
        else if (type == HrReportTypes.PunchAudit)
        {
            var punches = db.RawPunchEvents.AsNoTracking().Where(x => employees.Contains(x.EmployeeId) && x.ReceivedAtUtc >= start && x.ReceivedAtUtc <= end &&
                (!filter.SiteId.HasValue || x.SiteId == filter.SiteId));
            yield return punches.Select(x => new ReportSource(x.EmployeeId,
                x.BusinessDate ?? SourceDate(x.DeviceCapturedAtUtc ?? x.ReceivedAtUtc),
                x.BusinessDate ?? SourceDate(x.DeviceCapturedAtUtc ?? x.ReceivedAtUtc), x.SiteId, true));
            yield return punches.Select(x => new ReportSource(x.EmployeeId, SourceDate(x.DeviceCapturedAtUtc ?? x.ReceivedAtUtc),
                SourceDate(x.DeviceCapturedAtUtc ?? x.ReceivedAtUtc), x.SiteId, true));
        }
        else if (type == HrReportTypes.SiteReporting)
        {
            var reports = db.SiteReports.AsNoTracking().Where(x => employees.Contains(x.EmployeeId) && x.ReceivedAtUtc >= start && x.ReceivedAtUtc <= end &&
                (!filter.SiteId.HasValue || x.SiteId == filter.SiteId));
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var outcomes = MatchingValues<SiteReportReviewOutcome>(filter.Status);
                var pending = string.Equals(filter.Status, "PendingReview", StringComparison.OrdinalIgnoreCase);
                reports = from report in reports
                          let review = db.SiteReportReviews.Where(r => r.SiteReportId == report.Id).OrderByDescending(r => r.Sequence).ThenByDescending(r => r.Id).FirstOrDefault()
                          where review == null ? pending : outcomes.Contains(review.Outcome)
                          select report;
            }
            yield return reports.Select(x => new ReportSource(x.EmployeeId, SourceDate(x.DeviceCapturedAtUtc ?? x.ReceivedAtUtc),
                    SourceDate(x.DeviceCapturedAtUtc ?? x.ReceivedAtUtc), x.SiteId, true));
        }
        else if (type == HrReportTypes.OperationalZones)
        {
            // Use the same assignment/version path as the report projection, plus its retained event version.
            var events = db.ZoneEvents.AsNoTracking().Where(x => employees.Contains(x.EmployeeId) && x.ReceivedAtUtc >= start && x.ReceivedAtUtc <= end);
            if (filter.ZoneId.HasValue)
                events = events.Where(e => e.ZoneVersion.OperationalZoneId == filter.ZoneId.Value);
            yield return from e in events
                         join a in db.ZoneAssignments on e.ZoneAssignmentId equals a.Id
                         join z in db.OperationalZones on a.ZoneVersion.OperationalZoneId equals z.Id
                         select new ReportSource(e.EmployeeId, SourceDate(e.DeviceCapturedAtUtc ?? e.ReceivedAtUtc),
                             SourceDate(e.DeviceCapturedAtUtc ?? e.ReceivedAtUtc), z.SiteId, true);
            yield return from e in events
                         join z in db.OperationalZones on e.ZoneVersion.OperationalZoneId equals z.Id
                         select new ReportSource(e.EmployeeId, SourceDate(e.DeviceCapturedAtUtc ?? e.ReceivedAtUtc),
                             SourceDate(e.DeviceCapturedAtUtc ?? e.ReceivedAtUtc), z.SiteId, true);
        }
        else if (type == HrReportTypes.LateKpi)
        {
            var assessments = db.PolicyImpactAssessments.AsNoTracking().Where(x => employees.Contains(x.EmployeeId) && x.PeriodStart <= to && x.PeriodEnd >= rangeStart);
            yield return assessments
                .Select(x => new ReportSource(x.EmployeeId, x.PeriodStart, x.PeriodEnd, null, false));
            yield return from p in assessments
                         from a in db.EmployeeAssignments.Where(a => a.EmployeeId == p.EmployeeId &&
                             a.EffectiveFrom <= p.PeriodStart && (a.EffectiveTo == null || a.EffectiveTo > p.PeriodStart))
                             .OrderByDescending(a => a.EffectiveFrom).ThenByDescending(a => a.Id).Take(1)
                         select new ReportSource(p.EmployeeId, p.PeriodStart, p.PeriodStart, a.SiteId, true);
        }
        else if (type == HrReportTypes.Overtime)
        {
            var requests = db.OvertimeRequests.AsNoTracking().Where(x => employees.Contains(x.EmployeeId) && x.RequestedAtUtc >= start && x.RequestedAtUtc <= end &&
                (!filter.SiteId.HasValue || x.SiteId == filter.SiteId));
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var states = MatchingValues<ApprovalState>(filter.Status);
                var pending = string.Equals(filter.Status, "Pending", StringComparison.OrdinalIgnoreCase);
                requests = from request in requests
                           let approval = db.ApprovalRequests.FirstOrDefault(a => a.Id == request.ApprovalRequestId)
                           where approval == null ? pending : states.Contains(approval.State)
                           select request;
            }
            foreach (var query in OvertimeSources(requests)) yield return query;
        }
        else if (type == HrReportTypes.MealAllowance)
        {
            var claims = db.AllowanceClaims.AsNoTracking().Where(x => employees.Contains(x.EmployeeId) && x.CreatedAt >= start && x.CreatedAt <= end);
            if (!string.IsNullOrWhiteSpace(filter.Category))
            {
                var kinds = MatchingValues<AllowanceKind>(filter.Category);
                claims = claims.Where(c => kinds.Contains(c.Kind));
            }
            foreach (var query in AllowanceSources(claims))
                yield return query;
        }
        else if (type == HrReportTypes.Leave)
        {
            yield return db.LeaveLedgerEntries.AsNoTracking().Where(x => employees.Contains(x.EmployeeId) && x.LeaveYearStart <= to && x.LeaveYearEnd > rangeStart &&
                (!filter.LeaveTypeId.HasValue || x.LeaveTypeId == filter.LeaveTypeId))
                .Select(x => new ReportSource(x.EmployeeId, x.LeaveYearStart, x.LeaveYearEnd, null, false));
        }
        else if (type == HrReportTypes.FuelMileage)
        {
            var claims = db.FuelClaims.AsNoTracking().Where(x => employees.Contains(x.EmployeeId) && x.CreatedAt >= start && x.CreatedAt <= end);
            if (filter.VehicleId.HasValue)
                claims = claims.Where(c => db.VehicleTrips.Any(t => t.Id == c.VehicleTripId && t.EmployeeId == c.EmployeeId && t.VehicleId == filter.VehicleId.Value));
            foreach (var query in FuelSources(claims)) yield return query;
        }
        else if (type == HrReportTypes.PayrollHandoff)
        {
            var lines = from l in db.PayrollHandoffLines.AsNoTracking()
                        join b in db.PayrollHandoffBatches on l.PayrollHandoffBatchId equals b.Id
                        where employees.Contains(l.EmployeeId) && b.PeriodStart <= to && b.PeriodEnd > rangeStart
                        select l;
            yield return lines.Select(x => new ReportSource(x.EmployeeId, x.PayrollHandoffBatch.PeriodStart, x.PayrollHandoffBatch.PeriodEnd.AddDays(-1), null, false));
            foreach (var query in OvertimeSources(db.OvertimeRequests.AsNoTracking().Where(x => lines.Any(l => l.EmployeeId == x.EmployeeId && l.SourceType == "attendance.overtime" && l.SourceId == x.Id))))
                yield return query;
            foreach (var query in AllowanceSources(db.AllowanceClaims.AsNoTracking().Where(x => lines.Any(l => l.EmployeeId == x.EmployeeId && l.SourceType == "attendance.allowance" && l.SourceId == x.Id))))
                yield return query;
            foreach (var query in FuelSources(db.FuelClaims.AsNoTracking().Where(x => lines.Any(l => l.EmployeeId == x.EmployeeId && l.SourceType == "hr.vehicle-fuel.claim" && l.SourceId == x.Id))))
                yield return query;
            yield return db.AttendancePayrollInputRevisions.AsNoTracking().Where(x => lines.Any(l => l.EmployeeId == x.EmployeeId && l.SourceType == "attendance.payroll-input" && l.SourceId == x.Id))
                .Select(x => new ReportSource(x.EmployeeId, x.PeriodStart, x.PeriodStart.AddMonths(1).AddDays(-1), null, false));
            yield return db.RepaymentScheduleEntries.AsNoTracking().Where(x => lines.Any(l =>
                    l.EmployeeId == x.AdvanceLoanAgreement.AdvanceLoanRequest.EmployeeId && l.SourceType == "hr.advance-loan.schedule" && l.SourceId == x.Id))
                .Select(x => new ReportSource(x.AdvanceLoanAgreement.AdvanceLoanRequest.EmployeeId,
                    x.AdvanceLoanAgreement.AdvanceLoanRequest.RequestedOn, x.DueDate, null, false));
            // Unknown, missing or cross-employee provenance cannot establish a scoped financial footprint.
            yield return lines.Where(l =>
                    !(l.SourceType == "attendance.overtime" && db.OvertimeRequests.Any(x => x.Id == l.SourceId && x.EmployeeId == l.EmployeeId)) &&
                    !(l.SourceType == "attendance.allowance" && db.AllowanceClaims.Any(x => x.Id == l.SourceId && x.EmployeeId == l.EmployeeId)) &&
                    !(l.SourceType == "hr.vehicle-fuel.claim" && db.FuelClaims.Any(x => x.Id == l.SourceId && x.EmployeeId == l.EmployeeId && x.VehicleTrip.EmployeeId == l.EmployeeId)) &&
                    !(l.SourceType == "attendance.payroll-input" && db.AttendancePayrollInputRevisions.Any(x => x.Id == l.SourceId && x.EmployeeId == l.EmployeeId)) &&
                    !(l.SourceType == "hr.advance-loan.schedule" && db.RepaymentScheduleEntries.Any(x => x.Id == l.SourceId && x.AdvanceLoanAgreement.AdvanceLoanRequest.EmployeeId == l.EmployeeId)))
                .Select(x => new ReportSource(x.EmployeeId, DateOnly.MinValue, DateOnly.MinValue, null, false));
        }
    }

    private IEnumerable<IQueryable<ReportSource>> OvertimeSources(IQueryable<Cosmix.Domain.Attendance.Overtime.OvertimeRequest> requests)
    {
        yield return requests.Select(x => new ReportSource(x.EmployeeId, SourceDate(x.StartsAtUtc),
            LastSourceDate(x.StartsAtUtc, x.EndsAtUtc), x.SiteId, true));
        yield return from x in requests join w in db.WorkSessions on x.WorkSessionId equals w.Id
                     where w.EmployeeId == x.EmployeeId
                     select new ReportSource(x.EmployeeId, w.AttendanceDate, w.AttendanceDate, w.SiteId, true);
    }

    private IEnumerable<IQueryable<ReportSource>> AllowanceSources(IQueryable<Cosmix.Domain.HR.Allowances.AllowanceClaim> claims)
    {
        yield return claims.Select(x => new ReportSource(x.EmployeeId, SourceDate(x.EligibilityPeriodStart),
            LastSourceDate(x.EligibilityPeriodStart, x.EligibilityPeriodEnd), x.EmployeeAssignment.SiteId, true));
        yield return from x in claims join w in db.WorkSessions on x.WorkSessionId equals w.Id
                     where w.EmployeeId == x.EmployeeId
                     select new ReportSource(x.EmployeeId, w.AttendanceDate, w.AttendanceDate, w.SiteId, true);
    }

    private IQueryable<ReportSource>[] FuelSources(IQueryable<Cosmix.Domain.HR.VehicleFuel.FuelClaim> claims) =>
    [from x in claims join t in db.VehicleTrips on x.VehicleTripId equals t.Id where t.EmployeeId == x.EmployeeId
     select new ReportSource(x.EmployeeId, SourceDate(t.StartedAt), LastSourceDate(t.StartedAt, t.EndedAt), null, false)];
}
