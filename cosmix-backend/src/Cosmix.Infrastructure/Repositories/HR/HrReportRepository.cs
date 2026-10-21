using Cosmix.Application.HR.Reports;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.SiteReports;
using Cosmix.Domain.Attendance.Zones;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Domain.HR.PolicyEffects;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed partial class HrReportRepository(CosmixDbContext db, IRosterRepository rosters) : IHrReportRepository
{
    public async Task<IReadOnlyList<DailyAttendanceReportRow>> GetDailyAttendanceAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        var date = filter.Date ?? filter.From ?? RosterSchedule.Today(DateTimeOffset.UtcNow);
        var employees = await AttendanceReportEmployeesAsync(filter, selection, date, date, false, ct);
        var retained = await AttendanceReportSummariesAsync(employees.Select(x => x.Id).ToArray(), selection, date, date, ct);
        var matching = employees.Select(employee =>
        {
            retained.Days.TryGetValue((employee.Id, date), out var summary);
            if (summary is null && retained.IsLocked(date))
                throw Cosmix.Application.Attendance.Common.AttendanceOperation.Conflict("locked_attendance_summary_unavailable");
            return (Employee: employee, Summary: summary, Status: summary?.Status.ToString() ?? "Missing");
        }).Where(x => string.IsNullOrWhiteSpace(filter.Status) || string.Equals(x.Status, filter.Status, StringComparison.OrdinalIgnoreCase)).ToArray();
        var rows = matching.Skip(checked((filter.Page - 1) * filter.PageSize)).Take(Math.Min(filter.PageSize, 1000)).ToArray();
        var sessions = await AttendanceReportSessionsAsync(rows.Where(x => x.Summary is not null).Select(x => x.Summary!).ToArray(), selection, ct);
        var result = rows.Select(x =>
        {
            var summary = x.Summary;
            var times = summary is null ? [] : sessions.Where(s => s.DailyAttendanceSummaryId == summary.Id).ToArray();
            var first = times.Length == 0 ? (DateTimeOffset?)null : times.Min(s => s.StartsAtUtc);
            var last = times.Length == 0 || times.Any(s => s.EndsAtUtc is null) ? null : times.Max(s => s.EndsAtUtc);
            return new DailyAttendanceReportRow(x.Employee.Id, x.Employee.EmployeeNumber, x.Employee.FullName,
                x.Employee.DepartmentName, x.Employee.DesignationName, x.Employee.SiteName, date, x.Status,
                first?.UtcDateTime.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                last?.UtcDateTime.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                summary?.ShiftVersionId is not null ? summary.ScheduledMinutes : null, summary?.WorkedMinutes, summary?.LateMinutes, null,
                summary?.OvertimeMinutes, summary?.ExceptionCount,
                summary is null ? "summary_missing;quantities_unavailable" : AttendanceReportSource(summary, retained.IsLocked(date)));
        }).ToArray();
        return new HrReportRows<DailyAttendanceReportRow>(result, matching.Length);
    }

    public async Task<IReadOnlyList<MonthlyAttendanceRegisterReportRow>> GetMonthlyAttendanceRegisterAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        var today = RosterSchedule.Today(DateTimeOffset.UtcNow);
        var year = filter.Year ?? (filter.Date?.Year ?? today.Year);
        var month = filter.Month ?? (filter.Date?.Month ?? today.Month);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var startDate = new DateOnly(year, month, 1);
        var endDate = new DateOnly(year, month, daysInMonth);
        var monthYearStr = $"{year:D4}-{month:D2}";

        var matching = await AttendanceReportEmployeesAsync(filter, selection, startDate, endDate, true, ct);
        var employees = matching.Skip(checked((filter.Page - 1) * filter.PageSize)).Take(Math.Min(filter.PageSize, 1000)).ToArray();
        var retained = await AttendanceReportSummariesAsync(employees.Select(e => e.Id).ToArray(), selection, startDate, endDate, ct);

        var result = new List<MonthlyAttendanceRegisterReportRow>();
        foreach (var emp in employees)
        {
            var empSummaries = retained.Days.Values.Where(s => s.EmployeeId == emp.Id).OrderBy(s => s.AttendanceDate).ToArray();

            var presentDays = empSummaries.Count(s => s.Status is AttendanceDayStatus.Present or AttendanceDayStatus.HalfDay or AttendanceDayStatus.Hourly);
            var lateDays = empSummaries.Count(s => (s.LateMinutes ?? 0) > 0);
            var leaveDays = empSummaries.Count(s => s.Status == AttendanceDayStatus.Leave);
            var restHolidayDays = empSummaries.Count(s => s.Status is AttendanceDayStatus.Holiday or AttendanceDayStatus.RestDay);
            var absentDays = empSummaries.Count(s => s.Status == AttendanceDayStatus.Absent);

            var workedMinutes = empSummaries.Sum(s => (decimal)s.WorkedMinutes);
            var payableMinutes = empSummaries.Where(s => s.PayableMinutes.HasValue).Sum(s => (decimal)s.PayableMinutes!.Value);
            var overtimeMinutes = empSummaries.Sum(s => (decimal)(s.OvertimeMinutes ?? 0));

            result.Add(new MonthlyAttendanceRegisterReportRow(
                emp.Id,
                emp.EmployeeNumber,
                emp.FullName,
                emp.DepartmentName,
                emp.DesignationName,
                emp.SiteName,
                monthYearStr,
                daysInMonth,
                presentDays,
                lateDays,
                absentDays,
                leaveDays,
                restHolidayDays,
                Math.Round(workedMinutes / 60m, 2),
                Math.Round(payableMinutes / 60m, 2),
                Math.Round(overtimeMinutes / 60m, 2),
                empSummaries.Length,
                daysInMonth - empSummaries.Length,
                $"known_retained_quantities_only;calendar_summary_days={empSummaries.Length}/{daysInMonth};" +
                $"policy_quantity_days={empSummaries.Count(s => s.PayableMinutes.HasValue)}/{empSummaries.Length};" +
                string.Join("|", empSummaries.Select(s => AttendanceReportSource(s, retained.IsLocked(s.AttendanceDate))))));
        }

        return new HrReportRows<MonthlyAttendanceRegisterReportRow>(result, matching.Length);
    }

    public async Task<IReadOnlyList<PunchAuditReportRow>> GetPunchAuditAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        var fromUtc = filter.From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) ?? DateTime.UtcNow.AddDays(-7);
        var toUtc = filter.To?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc) ?? DateTime.UtcNow;

        var query = from p in db.RawPunchEvents.AsNoTracking()
                    where selection.EmployeeIds.Contains(p.EmployeeId) && p.ReceivedAtUtc >= fromUtc && p.ReceivedAtUtc <= toUtc
                    join emp in db.Employees.AsNoTracking() on p.EmployeeId equals emp.Id
                    let site = p.SiteId.HasValue ? db.Sites.FirstOrDefault(s => s.Id == p.SiteId.Value) : null
                    let assessment = db.PunchAssessments.Where(a => a.RawPunchEventId == p.Id && a.EmployeeId == p.EmployeeId).OrderByDescending(a => a.Revision).ThenByDescending(a => a.Id).FirstOrDefault()
                    select new
                    {
                        PunchId = p.Id,
                        p.ReceivedAtUtc,
                        p.DeviceCapturedAtUtc,
                        emp.EmployeeNumber,
                        FullName = emp.FullLegalName ?? $"Employee #{emp.EmployeeNumber}",
                        Action = p.Action.ToString(),
                        Source = p.OfficeGeofenceVersionId.HasValue ? "OfficeCapture" : p.SiteId.HasValue ? "SiteCapture" : "Unspecified",
                        p.RegisteredDeviceId,
                        SiteName = site != null ? site.Name : "Unassigned",
                        p.IsOffline,
                        p.Latitude,
                        p.Longitude,
                        p.AccuracyMeters,
                        IsWithinGeofence = assessment != null ? assessment.InsideGeofence : null,
                        AssessmentStatus = assessment != null ? assessment.Outcome.ToString() : "Unverified",
                        ExceptionReason = assessment != null ? assessment.ReasonCode : null,
                        p.EmployeeId,
                        p.SiteId
                    };

        if (filter.EmployeeId.HasValue)
            query = query.Where(x => x.EmployeeId == filter.EmployeeId.Value);

        if (filter.SiteId.HasValue)
            query = query.Where(x => x.SiteId == filter.SiteId.Value);

        var list = await query
            .OrderByDescending(x => x.ReceivedAtUtc).ThenBy(x => x.PunchId)
            .Take(MaximumReportRows + 1)
            .ToListAsync(ct);

        return PageReport(list.Select(x => new PunchAuditReportRow(
            x.PunchId,
            x.ReceivedAtUtc,
            x.DeviceCapturedAtUtc,
            x.EmployeeNumber,
            x.FullName,
            x.Action,
            x.Source,
            x.RegisteredDeviceId,
            x.SiteName,
            x.IsOffline,
            x.Latitude,
            x.Longitude,
            x.AccuracyMeters,
            x.IsWithinGeofence,
            x.AssessmentStatus,
            x.ExceptionReason)), filter);
    }

    public async Task<IReadOnlyList<OperationalZoneReportRow>> GetOperationalZonesAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        var fromUtc = filter.From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) ?? DateTime.UtcNow.AddDays(-30);
        var toUtc = filter.To?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc) ?? DateTime.UtcNow;

        var query = from ze in db.ZoneEvents.AsNoTracking()
                    where selection.EmployeeIds.Contains(ze.EmployeeId) && ze.ReceivedAtUtc >= fromUtc && ze.ReceivedAtUtc <= toUtc
                    join emp in db.Employees.AsNoTracking() on ze.EmployeeId equals emp.Id
                    let version = db.ZoneVersions.FirstOrDefault(v => v.Id == ze.ZoneVersionId)
                    let zone = version != null ? db.OperationalZones.FirstOrDefault(z => z.Id == version.OperationalZoneId) : null
                    let zoneType = version != null ? db.ZoneTypes.FirstOrDefault(t => t.Id == version.ZoneTypeId) : null
                    let response = db.ZoneEventResponses.Where(r => r.ZoneEventId == ze.Id)
                        .OrderByDescending(r => r.AtUtc).ThenByDescending(r => r.Id).FirstOrDefault()
                    select new
                    {
                        EventId = ze.Id,
                        ze.ReceivedAtUtc,
                        emp.EmployeeNumber,
                        FullName = emp.FullLegalName ?? $"Employee #{emp.EmployeeNumber}",
                        ZoneCode = zone != null ? zone.Code : "",
                        ZoneName = zone != null ? zone.Name : "",
                        ZoneColor = zoneType != null ? zoneType.DisplayColor : "",
                        EventKind = ze.Kind.ToString(),
                        Source = ze.Source.ToString(),
                        ze.ShownInstruction,
                        ze.IsOffline,
                        ReviewStatus = response != null ? response.Kind.ToString() : "Unreviewed",
                        ze.EmployeeId,
                        OperationalZoneId = zone != null ? zone.Id : (Guid?)null
                    };

        if (filter.EmployeeId.HasValue)
            query = query.Where(x => x.EmployeeId == filter.EmployeeId.Value);

        if (filter.ZoneId.HasValue)
            query = query.Where(x => x.OperationalZoneId == filter.ZoneId.Value);

        var list = await query
            .OrderByDescending(x => x.ReceivedAtUtc).ThenBy(x => x.EventId)
            .Take(MaximumReportRows + 1)
            .ToListAsync(ct);

        return PageReport(list.Select(x => new OperationalZoneReportRow(
            x.EventId,
            x.ReceivedAtUtc,
            x.EmployeeNumber,
            x.FullName,
            x.ZoneCode,
            x.ZoneName,
            x.ZoneColor,
            x.EventKind,
            x.Source,
            x.ShownInstruction,
            x.IsOffline,
            x.ReviewStatus)), filter);
    }

    public async Task<IReadOnlyList<SiteReportingReportRow>> GetSiteReportingAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        var fromUtc = filter.From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) ?? DateTime.UtcNow.AddDays(-30);
        var toUtc = filter.To?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc) ?? DateTime.UtcNow;

        var query = from sr in db.SiteReports.AsNoTracking()
                    where selection.EmployeeIds.Contains(sr.EmployeeId) && sr.ReceivedAtUtc >= fromUtc && sr.ReceivedAtUtc <= toUtc
                    join emp in db.Employees.AsNoTracking() on sr.EmployeeId equals emp.Id
                    join site in db.Sites.AsNoTracking() on sr.SiteId equals site.Id
                    let latestReview = db.SiteReportReviews.Where(r => r.SiteReportId == sr.Id).OrderByDescending(r => r.Sequence).ThenByDescending(r => r.Id).FirstOrDefault()
                    let response = db.Set<SiteReportResponse>().Where(r => r.SiteReportId == sr.Id && r.EmployeeId == sr.EmployeeId)
                        .OrderByDescending(r => r.Sequence).ThenByDescending(r => r.Id).FirstOrDefault()
                    select new
                    {
                        LatestResponseId = response != null ? (Guid?)response.Id : null,
                        LatestResponseAtUtc = response != null ? (DateTimeOffset?)response.SubmittedAtUtc : null,
                        ReportId = sr.Id,
                        sr.ReceivedAtUtc,
                        emp.EmployeeNumber,
                        ReporterName = emp.FullLegalName ?? $"Employee #{emp.EmployeeNumber}",
                        SiteName = site.Name,
                        ReportKind = sr.Kind.ToString(),
                        sr.TaskReference,
                        sr.Notes,
                        sr.IsOffline,
                        LatestReviewDecision = latestReview != null ? latestReview.Outcome.ToString() : "PendingReview",
                        ReviewOutcome = latestReview != null ? (SiteReportReviewOutcome?)latestReview.Outcome : null,
                        ReviewedByUserId = latestReview != null ? latestReview.Reviewer : null,
                        ReviewedAtUtc = latestReview != null ? latestReview.ReviewedAtUtc : (DateTimeOffset?)null,
                        ReviewerNotes = latestReview != null ? latestReview.Reason : null,
                        sr.SiteId,
                        sr.EmployeeId
                    };

        if (filter.SiteId.HasValue)
            query = query.Where(x => x.SiteId == filter.SiteId.Value);

        if (filter.EmployeeId.HasValue)
            query = query.Where(x => x.EmployeeId == filter.EmployeeId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var outcomes = MatchingValues<SiteReportReviewOutcome>(filter.Status);
            var pending = string.Equals(filter.Status, "PendingReview", StringComparison.OrdinalIgnoreCase);
            query = query.Where(x => x.ReviewOutcome.HasValue ? outcomes.Contains(x.ReviewOutcome.Value) : pending);
        }

        var list = await query
            .OrderByDescending(x => x.ReceivedAtUtc).ThenBy(x => x.ReportId)
            .Take(MaximumReportRows + 1)
            .ToListAsync(ct);

        return PageReport(list.Select(x => new SiteReportingReportRow(
            x.ReportId,
            x.ReceivedAtUtc,
            x.EmployeeNumber,
            x.ReporterName,
            x.SiteName,
            x.ReportKind,
            x.TaskReference,
            x.Notes,
            x.IsOffline,
            x.LatestReviewDecision,
            x.ReviewedByUserId,
            x.ReviewedAtUtc,
            x.ReviewerNotes) { LatestResponseId = x.LatestResponseId, LatestResponseAtUtc = x.LatestResponseAtUtc }), filter);
    }

    public async Task<IReadOnlyList<LateKpiReportRow>> GetLateKpiAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        var year = filter.Year ?? (filter.Date?.Year ?? DateTime.UtcNow.Year);

        var query = from pia in db.PolicyImpactAssessments.AsNoTracking()
                    where selection.EmployeeIds.Contains(pia.EmployeeId) && (pia.PeriodStart <= new DateOnly(year, 12, 31) && pia.PeriodEnd >= new DateOnly(year, 1, 1))
                    join emp in db.Employees.AsNoTracking() on pia.EmployeeId equals emp.Id
                    let assignment = db.EmployeeAssignments
                        .Where(a => a.EmployeeId == emp.Id && a.EffectiveFrom <= pia.PeriodStart && (a.EffectiveTo == null || a.EffectiveTo > pia.PeriodStart))
                        .OrderByDescending(a => a.EffectiveFrom).ThenByDescending(a => a.Id)
                        .FirstOrDefault()
                    let disciplinary = db.DisciplinaryCases.Where(d => d.EmployeeId == emp.Id && d.PolicyImpactAssessmentId == pia.Id)
                        .OrderByDescending(d => d.OpenedAt).ThenByDescending(d => d.Id).FirstOrDefault()
                    select new
                    {
                        AssessmentId = pia.Id,
                        emp.EmployeeNumber,
                        FullName = emp.FullLegalName ?? $"Employee #{emp.EmployeeNumber}",
                        DepartmentId = assignment != null ? assignment.DepartmentId : (Guid?)null,
                        BranchId = assignment != null ? assignment.BranchId : null,
                        SiteId = assignment != null ? assignment.SiteId : null,
                        DepartmentName = assignment != null && assignment.Department != null ? assignment.Department.Name : "Unassigned",
                        PolicyYear = year,
                        pia.PeriodStart,
                        pia.PeriodEnd,
                        ImpactKind = pia.Kind.ToString(),
                        pia.AffectedMinutes,
                        pia.ProposedMoneyImpact,
                        pia.Currency,
                        pia.ProposedKpiPercent,
                        DisciplinaryAction = disciplinary != null ? disciplinary.State.ToString() : "None",
                        pia.CalculationVersion,
                        AssessedAtUtc = pia.RecordedAt,
                        pia.EmployeeId
                    };

        if (filter.EmployeeId.HasValue)
            query = query.Where(x => x.EmployeeId == filter.EmployeeId.Value);

        if (filter.DepartmentId.HasValue)
            query = query.Where(x => x.DepartmentId == filter.DepartmentId.Value);

        if (filter.BranchId.HasValue) query = query.Where(x => x.BranchId == filter.BranchId.Value);
        if (filter.SiteId.HasValue) query = query.Where(x => x.SiteId == filter.SiteId.Value);

        var list = await query
            .OrderByDescending(x => x.AssessedAtUtc).ThenBy(x => x.AssessmentId)
            .Take(MaximumReportRows + 1)
            .ToListAsync(ct);

        return PageReport(list.Select(x => new LateKpiReportRow(
            x.AssessmentId,
            x.EmployeeNumber,
            x.FullName,
            x.DepartmentName,
            x.PolicyYear,
            x.PeriodStart,
            x.PeriodEnd,
            x.ImpactKind,
            x.AffectedMinutes,
            x.ProposedMoneyImpact,
            x.Currency,
            x.ProposedKpiPercent,
            x.DisciplinaryAction,
            x.CalculationVersion,
            x.AssessedAtUtc)), filter);
    }

    public async Task<IReadOnlyList<OvertimeReportRow>> GetOvertimeAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        var fromUtc = filter.From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) ?? DateTime.UtcNow.AddDays(-30);
        var toUtc = filter.To?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc) ?? DateTime.UtcNow;

        var query = from ot in db.OvertimeRequests.AsNoTracking()
                    where selection.EmployeeIds.Contains(ot.EmployeeId) && ot.RequestedAtUtc >= fromUtc && ot.RequestedAtUtc <= toUtc
                    join emp in db.Employees.AsNoTracking() on ot.EmployeeId equals emp.Id
                    let site = ot.SiteId.HasValue ? db.Sites.FirstOrDefault(s => s.Id == ot.SiteId.Value) : null
                    let approval = db.ApprovalRequests.FirstOrDefault(a => a.Id == ot.ApprovalRequestId)
                    select new
                    {
                        RequestId = ot.Id,
                        ot.RequestedAtUtc,
                        emp.EmployeeNumber,
                        FullName = emp.FullLegalName ?? $"Employee #{emp.EmployeeNumber}",
                        SiteName = site != null ? site.Name : "Unassigned",
                        ot.WorkSessionId,
                        ot.StartsAtUtc,
                        ot.EndsAtUtc,
                        ot.RequestedMinutes,
                        ot.RateRuleCode,
                        ot.JobReference,
                        ot.Reason,
                        ot.RequestedBy,
                        ApprovalStatus = approval != null ? approval.State.ToString() : "Pending",
                        ApprovalState = approval != null ? (ApprovalState?)approval.State : null,
                        ot.EmployeeId,
                        ot.SiteId
                    };

        if (filter.EmployeeId.HasValue)
            query = query.Where(x => x.EmployeeId == filter.EmployeeId.Value);

        if (filter.SiteId.HasValue)
            query = query.Where(x => x.SiteId == filter.SiteId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var states = MatchingValues<ApprovalState>(filter.Status);
            var pending = string.Equals(filter.Status, "Pending", StringComparison.OrdinalIgnoreCase);
            query = query.Where(x => x.ApprovalState.HasValue ? states.Contains(x.ApprovalState.Value) : pending);
        }

        var list = await query
            .OrderByDescending(x => x.RequestedAtUtc).ThenBy(x => x.RequestId)
            .Take(MaximumReportRows + 1)
            .ToListAsync(ct);

        return PageReport(list.Select(x => new OvertimeReportRow(
            x.RequestId, x.RequestedAtUtc, x.EmployeeNumber, x.FullName, x.SiteName, x.WorkSessionId,
            x.StartsAtUtc, x.EndsAtUtc, x.RequestedMinutes, x.RateRuleCode, x.JobReference, x.Reason,
            x.RequestedBy, x.ApprovalStatus, null, null) { EmployeeId = x.EmployeeId }), filter);
    }

    public async Task<IReadOnlyList<MealAllowanceReportRow>> GetMealAllowanceAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        var fromUtc = filter.From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) ?? DateTime.UtcNow.AddDays(-30);
        var toUtc = filter.To?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc) ?? DateTime.UtcNow;

        var query = from claim in db.AllowanceClaims.AsNoTracking()
                    where selection.EmployeeIds.Contains(claim.EmployeeId) && claim.CreatedAt >= fromUtc && claim.CreatedAt <= toUtc
                    join emp in db.Employees.AsNoTracking() on claim.EmployeeId equals emp.Id
                    let submission = db.AllowanceClaimSubmissions.Where(s => s.AllowanceClaimId == claim.Id).OrderByDescending(s => s.Version).ThenByDescending(s => s.Id).FirstOrDefault()
                    let approval = submission != null ? db.ApprovalRequests.FirstOrDefault(a => a.Id == submission.ApprovalRequestId) : null
                    let settlement = db.AllowanceSettlementRecords.Where(r => r.AllowanceClaimId == claim.Id && submission != null && r.SubmissionVersion == submission.Version)
                        .OrderByDescending(r => r.RecordedAt).ThenByDescending(r => r.Id).FirstOrDefault()
                    select new
                    {
                        ClaimId = claim.Id,
                        CreatedAtUtc = claim.CreatedAt,
                        emp.EmployeeNumber,
                        FullName = emp.FullLegalName ?? $"Employee #{emp.EmployeeNumber}",
                        AllowanceKind = claim.Kind.ToString(),
                        Kind = claim.Kind,
                        claim.DeploymentCategoryCode,
                        claim.EligibilityPeriodStart,
                        claim.EligibilityPeriodEnd,
                        ClaimedAmount = submission != null ? submission.ClaimedAmount : claim.ClaimedAmount,
                        Currency = submission != null ? submission.Currency : claim.Currency,
                        PayableDate = submission != null ? submission.PayableDate : claim.PayableDate,
                        PayrollPeriodStart = submission != null ? submission.PayrollPeriodStart : claim.PayrollPeriodStart,
                        ApprovalStatus = approval != null ? approval.State.ToString() : "Pending",
                        SettlementStatus = settlement != null ? settlement.State.ToString() : "NotScheduled",
                        SettlementBatchReference = settlement != null ? settlement.ExternalPaymentReference : null,
                        claim.EmployeeId
                    };

        if (filter.EmployeeId.HasValue)
            query = query.Where(x => x.EmployeeId == filter.EmployeeId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            var kinds = MatchingValues<AllowanceKind>(filter.Category);
            query = query.Where(x => kinds.Contains(x.Kind));
        }

        var list = await query
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.ClaimId)
            .Take(MaximumReportRows + 1)
            .ToListAsync(ct);

        return PageReport(list.Select(x => new MealAllowanceReportRow(
            x.ClaimId,
            x.CreatedAtUtc,
            x.EmployeeNumber,
            x.FullName,
            x.AllowanceKind,
            x.DeploymentCategoryCode,
            x.EligibilityPeriodStart,
            x.EligibilityPeriodEnd,
            x.ClaimedAmount,
            x.Currency,
            x.PayableDate,
            x.PayrollPeriodStart,
            x.ApprovalStatus,
            x.SettlementStatus,
            x.SettlementBatchReference)), filter);
    }

    public async Task<IReadOnlyList<LeaveReportRow>> GetLeaveAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        var year = filter.Year ?? (filter.Date?.Year ?? DateTime.UtcNow.Year);
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        var query = from ledger in db.LeaveLedgerEntries.AsNoTracking()
                    where selection.EmployeeIds.Contains(ledger.EmployeeId) &&
                        ledger.LeaveYearStart <= yearEnd && ledger.LeaveYearEnd > yearStart &&
                        (!filter.EmployeeId.HasValue || ledger.EmployeeId == filter.EmployeeId.Value) &&
                        (!filter.LeaveTypeId.HasValue || ledger.LeaveTypeId == filter.LeaveTypeId.Value)
                    join emp in db.Employees.AsNoTracking() on ledger.EmployeeId equals emp.Id
                    join lt in db.LeaveTypes.AsNoTracking() on ledger.LeaveTypeId equals lt.Id
                    select new { Ledger = ledger, emp.EmployeeNumber,
                        FullName = emp.FullLegalName ?? $"Employee #{emp.EmployeeNumber}", LeaveTypeName = lt.Name };
        var entries = await query.Take(MaximumReportRows + 1).ToListAsync(ct);
        RequireReportBound(entries.Count);
        var rows = entries.GroupBy(x => new { x.Ledger.EmployeeId, x.Ledger.LeaveTypeId,
            x.Ledger.LeaveYearStart, x.Ledger.LeaveYearEnd, x.EmployeeNumber, x.FullName, x.LeaveTypeName })
            .OrderBy(g => g.Key.EmployeeNumber).ThenBy(g => g.Key.EmployeeId)
            .ThenBy(g => g.Key.LeaveYearStart).ThenBy(g => g.Key.LeaveTypeId).Select(group =>
            {
                var ledger = group.Select(x => x.Ledger).ToArray();
                var byId = ledger.ToDictionary(x => x.Id);
                // A release/reversal belongs to the original category; its sign is preserved.
                LeaveLedgerEntryType Category(LeaveLedgerEntry entry)
                {
                    var seen = new HashSet<Guid>();
                    while (entry.ReversesEntryId is Guid original)
                    {
                        if (!seen.Add(entry.Id) || !byId.TryGetValue(original, out var source))
                            throw Cosmix.Application.Attendance.Common.AttendanceOperation.Conflict("hr_report_leave_reversal_unavailable");
                        entry = source;
                    }
                    return entry.EntryType;
                }
                long Sum(LeaveLedgerEntryType category) => ledger.Where(e => Category(e) == category).Sum(e => (long)e.MinutesDelta);
                return new LeaveReportRow(group.Key.EmployeeId, group.Key.EmployeeNumber, group.Key.FullName,
                    group.Key.LeaveTypeName, group.Key.LeaveYearStart.Year, null, null, null, null, null, null, null)
                {
                    LeaveTypeId = group.Key.LeaveTypeId, LeaveYearStart = group.Key.LeaveYearStart,
                    LeaveYearEndExclusive = group.Key.LeaveYearEnd,
                    EntitledMinutes = Sum(LeaveLedgerEntryType.Entitlement),
                    UsedMinutes = -Sum(LeaveLedgerEntryType.Utilization),
                    ReservedMinutes = -Sum(LeaveLedgerEntryType.Reservation),
                    EncashmentMinutes = -Sum(LeaveLedgerEntryType.Encashment),
                    BalanceMinutes = ledger.Sum(e => (long)e.MinutesDelta)
                };
            }).ToArray();
        return PageReport(rows, filter);
    }

    public async Task<IReadOnlyList<EmployeeMasterReportRow>> GetEmployeeMasterAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        var today = filter.From!.Value;

        var query = from emp in db.Employees.AsNoTracking()
                    where selection.EmployeeIds.Contains(emp.Id) && !emp.IsArchived
                    let assignment = db.EmployeeAssignments
                        .Where(a => a.EmployeeId == emp.Id && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo > today))
                        .OrderByDescending(a => a.EffectiveFrom)
                        .FirstOrDefault()
                    select new
                    {
                        EmployeeId = emp.Id,
                        emp.EmployeeNumber,
                        FullName = emp.FullLegalName ?? $"Employee #{emp.EmployeeNumber}",
                        FatherOrSpouseName = emp.FatherOrSpouseName ?? "",
                        emp.DateOfBirth,
                        Gender = emp.Gender ?? "Unspecified",
                        DepartmentId = assignment != null ? assignment.DepartmentId : (Guid?)null,
                        DepartmentName = assignment != null && assignment.Department != null ? assignment.Department.Name : "Unassigned",
                        DesignationName = assignment != null && assignment.Designation != null ? assignment.Designation.Name : "Unassigned",
                        BranchId = assignment != null ? assignment.BranchId : (Guid?)null,
                        BranchName = assignment != null && assignment.Branch != null ? assignment.Branch.Name : "Unassigned",
                        StaffCategoryId = assignment != null ? assignment.StaffCategoryId : (Guid?)null,
                        StaffCategoryName = assignment != null && assignment.StaffCategory != null ? assignment.StaffCategory.Name : "Unassigned",
                        EmploymentTypeName = assignment != null && assignment.EmploymentType != null ? assignment.EmploymentType.Name : "Unassigned",
                        emp.JoinedOn,
                        Status = emp.Status.ToString(),
                        EmployeeStatus = emp.Status,
                        OfficialEmail = emp.OfficialEmail ?? "",
                        CompanyPhone = emp.CompanyPhone ?? ""
                    };

        if (filter.DepartmentId.HasValue)
            query = query.Where(x => x.DepartmentId == filter.DepartmentId.Value);

        if (filter.BranchId.HasValue)
            query = query.Where(x => x.BranchId == filter.BranchId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var states = MatchingValues<EmployeeStatus>(filter.Status);
            query = query.Where(x => states.Contains(x.EmployeeStatus));
        }

        if (filter.EmployeeId.HasValue)
            query = query.Where(x => x.EmployeeId == filter.EmployeeId.Value);

        var list = await query
            .OrderBy(x => x.EmployeeNumber).ThenBy(x => x.EmployeeId)
            .Take(MaximumReportRows + 1)
            .ToListAsync(ct);

        return PageReport(list.Select(x => new EmployeeMasterReportRow(
            x.EmployeeId,
            x.EmployeeNumber,
            x.FullName,
            x.FatherOrSpouseName,
            x.DateOfBirth,
            x.Gender,
            x.DepartmentName,
            x.DesignationName,
            x.BranchName,
            x.StaffCategoryName,
            x.EmploymentTypeName,
            x.JoinedOn,
            x.Status,
            x.OfficialEmail,
            x.CompanyPhone)), filter);
    }

    public async Task<IReadOnlyList<DocumentExpiryReportRow>> GetDocumentExpiryAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        var thresholdDate = filter.Date ?? filter.To ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90));
        var today = filter.From!.Value;
        var results = new List<DocumentExpiryReportRow>();

        // 1. National Identity / CNIC
        var cnicList = await db.Employees.AsNoTracking()
            .Where(e => selection.EmployeeIds.Contains(e.Id) && !e.IsArchived && e.IdentityDocumentExpiresOn.HasValue && e.IdentityDocumentExpiresOn.Value <= thresholdDate)
            .Select(e => new
            {
                e.Id,
                e.EmployeeNumber,
                FullName = e.FullLegalName ?? $"Employee #{e.EmployeeNumber}",
                ExpiresOn = e.IdentityDocumentExpiresOn,
                Number = e.IdentityDocumentNumber ?? ""
            })
            .Take(MaximumReportRows + 1).ToListAsync(ct);
        RequireReportBound(cnicList.Count);

        foreach (var cnic in cnicList)
        {
            var exp = cnic.ExpiresOn ?? today;
            var days = exp.DayNumber - today.DayNumber;
            results.Add(new DocumentExpiryReportRow(
                cnic.EmployeeNumber,
                cnic.FullName,
                "Identity",
                "CNIC / National ID",
                cnic.Number,
                "",
                null,
                cnic.ExpiresOn,
                days,
                days < 0 ? "Expired" : days <= 30 ? "ExpiringWithin30Days" : days <= 90 ? "ExpiringWithin90Days" : "Valid") { SourceId = cnic.Id });
        }

        // 2. Employee Documents (Contracts, Degrees, Licenses)
        var docs = await (from d in db.EmployeeDocuments.AsNoTracking()
                          where selection.EmployeeIds.Contains(d.EmployeeId) && d.ExpiresOn.HasValue && d.ExpiresOn.Value <= thresholdDate && d.LifecycleStatus == EmployeeDocumentLifecycleStatus.Current
                          join emp in db.Employees.AsNoTracking() on d.EmployeeId equals emp.Id
                          select new
                          {
                              d.Id,
                              emp.EmployeeNumber,
                              FullName = emp.FullLegalName ?? $"Employee #{emp.EmployeeNumber}",
                              DocType = d.DocumentType.ToString(),
                              ExpiresOn = d.ExpiresOn,
                              d.IssuedOn,
                              d.OriginalFileName
                          }).Take(MaximumReportRows + 1).ToListAsync(ct);
        RequireReportBound(docs.Count);

        foreach (var doc in docs)
        {
            var exp = doc.ExpiresOn ?? today;
            var days = exp.DayNumber - today.DayNumber;
            results.Add(new DocumentExpiryReportRow(
                doc.EmployeeNumber,
                doc.FullName,
                "Employee Document",
                doc.DocType,
                doc.OriginalFileName,
                "",
                doc.IssuedOn,
                doc.ExpiresOn,
                days,
                days < 0 ? "Expired" : days <= 30 ? "ExpiringWithin30Days" : days <= 90 ? "ExpiringWithin90Days" : "Valid") { SourceId = doc.Id });
        }

        // 3. Employee Certifications & Professional Licenses
        var certs = await (from c in db.EmployeeCertifications.AsNoTracking()
                           where selection.EmployeeIds.Contains(c.EmployeeId) && c.ExpiresOn.HasValue && c.ExpiresOn.Value <= thresholdDate && c.Status == EmployeeCertificationStatus.Active &&
                               c.EffectiveOn <= today && !db.EmployeeCertifications.Any(next => next.EmployeeId == c.EmployeeId &&
                                   next.SupersedesEmployeeCertificationId == c.Id && next.EffectiveOn <= today)
                           join emp in db.Employees.AsNoTracking() on c.EmployeeId equals emp.Id
                           select new
                           {
                               c.Id,
                               emp.EmployeeNumber,
                               FullName = emp.FullLegalName ?? $"Employee #{emp.EmployeeNumber}",
                               c.CertificateType,
                               c.CertificateNumber,
                               c.Issuer,
                               c.IssuedOn,
                               ExpiresOn = c.ExpiresOn
                           }).Take(MaximumReportRows + 1).ToListAsync(ct);
        RequireReportBound(certs.Count);

        foreach (var cert in certs)
        {
            var exp = cert.ExpiresOn ?? today;
            var days = exp.DayNumber - today.DayNumber;
            results.Add(new DocumentExpiryReportRow(
                cert.EmployeeNumber,
                cert.FullName,
                "Certification",
                cert.CertificateType,
                cert.CertificateNumber,
                cert.Issuer,
                cert.IssuedOn,
                cert.ExpiresOn,
                days,
                days < 0 ? "Expired" : days <= 30 ? "ExpiringWithin30Days" : days <= 90 ? "ExpiringWithin90Days" : "Valid") { SourceId = cert.Id });
        }

        // 4. Insurance Policies
        var policies = await (from p in db.InsurancePolicies.AsNoTracking()
                              where p.ExpiryDate <= thresholdDate && p.EmployeeId.HasValue && selection.EmployeeIds.Contains(p.EmployeeId.Value)
                              join emp in db.Employees.AsNoTracking() on p.EmployeeId equals (Guid?)emp.Id
                              select new
                              {
                                  p.Id,
                                  emp.EmployeeNumber,
                                  FullName = emp.FullLegalName ?? $"Employee #{emp.EmployeeNumber}",
                                  p.PolicyNumber,
                                  p.ProviderName,
                                  p.CoverageStartDate,
                                  ExpiresOn = (DateOnly?)p.ExpiryDate
                              }).Take(MaximumReportRows + 1).ToListAsync(ct);
        RequireReportBound(policies.Count);

        foreach (var pol in policies)
        {
            var exp = pol.ExpiresOn ?? today;
            var days = exp.DayNumber - today.DayNumber;
            results.Add(new DocumentExpiryReportRow(
                pol.EmployeeNumber,
                pol.FullName,
                "Insurance",
                "Employee Insurance",
                pol.PolicyNumber,
                pol.ProviderName,
                pol.CoverageStartDate,
                pol.ExpiresOn,
                days,
                days < 0 ? "Expired" : days <= 30 ? "ExpiringWithin30Days" : days <= 90 ? "ExpiringWithin90Days" : "Valid") { SourceId = pol.Id });
        }

        if (!string.IsNullOrWhiteSpace(filter.DocumentType))
            results = results.Where(r => r.Category.Contains(filter.DocumentType, StringComparison.OrdinalIgnoreCase) ||
                                         r.DocumentType.Contains(filter.DocumentType, StringComparison.OrdinalIgnoreCase)).ToList();

        RequireReportBound(results.Count);
        return PageReport(results.OrderBy(r => r.DaysUntilExpiry).ThenBy(r => r.EmployeeNumber)
            .ThenBy(r => r.Category, StringComparer.Ordinal).ThenBy(r => r.SourceId), filter);
    }

    public async Task<IReadOnlyList<FuelMileageReportRow>> GetFuelMileageAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        var fromUtc = filter.From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) ?? DateTime.UtcNow.AddDays(-30);
        var toUtc = filter.To?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc) ?? DateTime.UtcNow;

        var query = from fc in db.FuelClaims.AsNoTracking()
                    where selection.EmployeeIds.Contains(fc.EmployeeId) && fc.CreatedAt >= fromUtc && fc.CreatedAt <= toUtc
                    join emp in db.Employees.AsNoTracking() on fc.EmployeeId equals emp.Id
                    join trip in db.VehicleTrips.AsNoTracking() on fc.VehicleTripId equals trip.Id
                    where trip.EmployeeId == fc.EmployeeId
                    join veh in db.Vehicles.AsNoTracking() on trip.VehicleId equals veh.Id
                    let approval = fc.ApprovalRequestId.HasValue ? db.ApprovalRequests.FirstOrDefault(a => a.Id == fc.ApprovalRequestId.Value) : null
                    select new
                    {
                        RecordId = fc.Id,
                        trip.StartedAt,
                        RegistrationNumber = veh.RegistrationNumber,
                        veh.MakeModel,
                        emp.EmployeeNumber,
                        DriverName = emp.FullLegalName ?? $"Employee #{emp.EmployeeNumber}",
                        ApprovedRoute = $"{trip.OriginLabel} -> {trip.DestinationLabel}",
                        fc.EligibleDistanceKm,
                        fc.ExcludedCommuteKm,
                        fc.FuelLiters,
                        KmPerLiter = fc.FuelLiters > 0 ? (decimal?)Math.Round(fc.EligibleDistanceKm / fc.FuelLiters, 2) : null,
                        fc.ClaimedAmount,
                        fc.Currency,
                        ApprovalStatus = approval != null ? approval.State.ToString() : "Pending",
                        NextServiceDueKm = (decimal?)null,
                        fc.EmployeeId,
                        trip.VehicleId
                    };

        if (filter.EmployeeId.HasValue)
            query = query.Where(x => x.EmployeeId == filter.EmployeeId.Value);

        if (filter.VehicleId.HasValue)
            query = query.Where(x => x.VehicleId == filter.VehicleId.Value);

        var list = await query
            .OrderByDescending(x => x.StartedAt).ThenBy(x => x.RecordId)
            .Take(MaximumReportRows + 1)
            .ToListAsync(ct);

        return PageReport(list.Select(x => new FuelMileageReportRow(
            x.RecordId,
            SourceDate(x.StartedAt),
            x.RegistrationNumber,
            x.MakeModel,
            x.EmployeeNumber,
            x.DriverName,
            x.ApprovedRoute,
            x.EligibleDistanceKm,
            x.ExcludedCommuteKm,
            x.FuelLiters,
            x.KmPerLiter,
            x.ClaimedAmount,
            x.Currency,
            x.ApprovalStatus,
            x.NextServiceDueKm)), filter);
    }

    public async Task<IReadOnlyList<PayrollHandoffReportRow>> GetPayrollHandoffAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        var year = filter.Year ?? (filter.Date?.Year ?? DateTime.UtcNow.Year);
        var month = filter.Month ?? (filter.Date?.Month ?? DateTime.UtcNow.Month);
        var startDate = new DateOnly(year, month, 1);
        var endDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        var query = from line in db.PayrollHandoffLines.AsNoTracking()
                    where selection.EmployeeIds.Contains(line.EmployeeId)
                    join batch in db.PayrollHandoffBatches.AsNoTracking() on line.PayrollHandoffBatchId equals batch.Id
                    join emp in db.Employees.AsNoTracking() on line.EmployeeId equals emp.Id
                    where batch.PeriodStart <= endDate && batch.PeriodEnd > startDate
                    select new
                    {
                        BatchId = batch.Id,
                        batch.BatchReference,
                        LineId = line.Id,
                        emp.EmployeeNumber,
                        FullName = emp.FullLegalName ?? $"Employee #{emp.EmployeeNumber}",
                        batch.PeriodStart,
                        batch.PeriodEnd,
                        ComponentKind = line.Component.ToString(),
                        line.SourceType,
                        line.SourceId,
                        line.SourceVersion,
                        line.AttendancePeriodLockEventId,
                        line.Amount,
                        line.Currency,
                        line.TraceId,
                        IsBatchLocked = batch.LockedAt.HasValue,
                        LockedAtUtc = batch.LockedAt,
                        line.EmployeeId
                    };

        if (filter.EmployeeId.HasValue)
            query = query.Where(x => x.EmployeeId == filter.EmployeeId.Value);

        var list = await query
            .OrderBy(x => x.EmployeeNumber).ThenBy(x => x.LineId)
            .Take(MaximumReportRows + 1)
            .ToListAsync(ct);

        return PageReport(list.Select(x => new PayrollHandoffReportRow(
            x.BatchId,
            x.BatchReference,
            x.LineId,
            x.EmployeeNumber,
            x.FullName,
            x.PeriodStart,
            x.PeriodEnd,
            x.ComponentKind,
            x.SourceType,
            x.SourceId,
            x.Amount,
            x.Currency,
            x.TraceId,
            x.IsBatchLocked,
            x.LockedAtUtc) { SourceVersion = x.SourceVersion, AttendancePeriodLockEventId = x.AttendancePeriodLockEventId }), filter);
    }

    public async Task<IReadOnlyList<AuditReportRow>> GetAuditAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        var fromUtc = filter.From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) ?? DateTime.UtcNow.AddDays(-30);
        var toUtc = filter.To?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc) ?? DateTime.UtcNow;

        var includeUnscoped = selection.IncludeUnscopedAudit && !HasEmployeeFilters(filter);
        var query = from a in db.AuditEvents.AsNoTracking()
                    where (a.EmployeeScopeId.HasValue ? selection.EmployeeIds.Contains(a.EmployeeScopeId.Value) : includeUnscoped)
                        && a.OccurredAt >= fromUtc && a.OccurredAt <= toUtc
                    let emp = a.EmployeeScopeId.HasValue ? db.Employees.FirstOrDefault(e => e.Id == a.EmployeeScopeId.Value) : null
                    select new
                    {
                        EventId = a.Id,
                        OccurredAtUtc = a.OccurredAt,
                        a.ActorUserId,
                        a.ActorRoleCode,
                        a.EventType,
                        a.TargetType,
                        a.TargetId,
                        ScopeEmployeeNumber = emp != null ? (long?)emp.EmployeeNumber : null,
                        ScopeEmployeeName = emp != null ? emp.FullLegalName : null,
                        a.CorrelationId,
                        a.IpAddress,
                        a.DeviceReference
                    };

        if (!string.IsNullOrWhiteSpace(filter.Category))
            query = query.Where(x => x.EventType.ToLower().Contains(filter.Category.ToLower()) ||
                                     x.TargetType.ToLower().Contains(filter.Category.ToLower()));

        var list = await query
            .OrderByDescending(x => x.OccurredAtUtc).ThenBy(x => x.EventId)
            .Take(MaximumReportRows + 1)
            .ToListAsync(ct);

        return PageReport(list.Select(x => new AuditReportRow(
            x.EventId,
            x.OccurredAtUtc,
            x.ActorUserId,
            x.ActorRoleCode,
            x.EventType,
            x.TargetType,
            x.TargetId,
            x.ScopeEmployeeNumber,
            x.ScopeEmployeeName,
            x.CorrelationId,
            x.IpAddress,
            x.DeviceReference)), filter);
    }
    private static HrReportRows<T> PageReport<T>(IEnumerable<T> source, HrReportFilter filter)
    {
        var rows = source.ToArray();
        RequireReportBound(rows.Length);
        return new(rows.Skip(checked((filter.Page - 1) * filter.PageSize)).Take(filter.PageSize).ToArray(), rows.Length);
    }
}
