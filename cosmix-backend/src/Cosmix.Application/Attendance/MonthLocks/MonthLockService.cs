using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.MonthLocks;

namespace Cosmix.Application.Attendance.MonthLocks;

/// <summary>Global direct finalization, not an approval chain. Every operation requires All scope.</summary>
public sealed class MonthLockService(AttendanceOperation operation, IMonthLockRepository repository,
    AttendanceSummaryService summaries, IMonthLockCarryForwardReader carryForwards, IPrivateFileStore files,
    IMonthLockFinancialReader? financial = null, IMonthLockOvertimeReader? overtime = null,
    IAttendancePayrollFinancialMonthReader? adjustments = null) : IMonthLockSnapshotReader
{
    public Task<MonthLockPreview> PreviewAsync(int year, int month, AuthRequestContext context, CancellationToken ct)
    {
        var bounds = MonthLockCoverage.Month(year, month);
        return operation.ReadAsync(MonthLockPermissions.Read, "attendance.month-lock.preview", null, context,
            async (actor, token) => (await Inspect(bounds.Start, bounds.End, actor, token)).Preview, ct);
    }
    public Task<MonthLockDetail> DetailAsync(int year, int month, AuthRequestContext context, CancellationToken ct)
    {
        var bounds = MonthLockCoverage.Month(year, month);
        return operation.ReadAsync(MonthLockPermissions.Read, "attendance.month-lock.detail", null, context, async (actor, token) =>
        {
            var existing = await repository.FindAsync(bounds.Start, bounds.End, token) ?? throw AttendanceOperation.Missing();
            var snapshot = await ReadSnapshot(existing, actor, token);
            return new MonthLockDetail(Map(existing.Period, existing.Event!, snapshot), snapshot.Summaries, snapshot.CarryForwards, snapshot.PayrollInputs);
        }, ct);
    }
    public Task<MonthLockResponse> FinalizeAsync(int year, int month, FinalizeMonthLockRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        var bounds = MonthLockCoverage.Month(year, month);
        if (!MonthLockEvidence.Hash(request.ExpectedPreviewFingerprint)) throw AttendanceOperation.Invalid("invalid_month_lock_fingerprint");
        var version = request.ExpectedVersion is null ? null : SchedulingOperation.Version(request.ExpectedVersion);
        return operation.WriteAsync(MonthLockPermissions.Finalize, "attendance.month-lock.finalize", null, key,
            new { year, month, request }, request.Reason, context, async (actor, token) =>
            {
                var state = await Inspect(bounds.Start, bounds.End, actor, token);
                if (state.Existing?.Period.IsLocked == true) throw AttendanceOperation.Conflict("attendance_month_already_locked");
                if (state.Existing is null ? version is not null : version is null || !state.Existing.Period.RowVersion.SequenceEqual(version))
                    throw AttendanceOperation.Conflict("month_lock_version_conflict");
                if (!string.Equals(request.ExpectedPreviewFingerprint, state.Preview.PreviewFingerprint, StringComparison.OrdinalIgnoreCase))
                    throw AttendanceOperation.Conflict("month_lock_preview_changed");
                if (!state.Preview.Ready) throw AttendanceOperation.Conflict("attendance_month_not_ready");
                var period = state.Existing?.Period ?? new AttendancePeriodLock(bounds.Start, bounds.End);
                var eventId = Guid.NewGuid();
                var snapshot = new MonthLockSnapshot(4, eventId, period.Id, checked(period.Revision + 1), bounds.Start, bounds.End,
                    actor.Identity.AccountId.ToString(), request.Reason.Trim(), actor.Now, state.Preview.PreviewFingerprint,
                    state.Rows.Select(Entry).OrderBy(x => x.EmployeeId).ThenBy(x => x.BusinessDate).ToArray(), state.Carry,
                    state.Financial.PayrollInputs, state.Overtime, state.Adjustments);
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), MonthLockEvidence.Purpose(period.Id, eventId), token));
                // Storage IO must not widen the source-race window before the final lock is staged.
                var rechecked = await Inspect(bounds.Start, bounds.End, actor, token);
                if (!rechecked.Preview.Ready || rechecked.Preview.PreviewFingerprint != state.Preview.PreviewFingerprint)
                    throw AttendanceOperation.Conflict("month_lock_preview_changed");
                if (version is not null) repository.ExpectVersion(period, version);
                var item = period.RecordDirectLock(eventId, actor.Identity.AccountId.ToString(), request.Reason.Trim(), actor.Now, reference);
                var links = state.Rows.Select(row => new AttendancePeriodSummary(period, item, row)).ToArray();
                repository.Add(period, item, links, state.Existing is null);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<MonthLockResponse>(period.Id, state.Preview, Map(period, item, snapshot));
            }, ct, beforeReplay: async (actor, token) =>
            {
                var existing = await repository.FindAsync(bounds.Start, bounds.End, token);
                if (existing?.Period.IsLocked == true)
                    _ = await ReadSnapshot(existing, actor, token);
            });
    }

    private async Task<Inspection> Inspect(DateOnly start, DateOnly end, AttendanceActor actor, CancellationToken ct)
    {
        var existing = await repository.FindAsync(start, end, ct);
        if (existing?.Period.IsLocked == true)
        {
            var frozen = await ReadSnapshot(existing, actor, ct);
            return new(new(start, end, existing.Period.Id, Convert.ToBase64String(existing.Period.RowVersion), "locked", false,
                frozen.PreviewFingerprint, frozen.Summaries.Select(x => x.EmployeeId).Distinct().Count(), frozen.Summaries.Count,
                frozen.Summaries.Count, frozen.CarryForwards.Count, 1, [new("attendance_month_already_locked")]), existing, existing.Summaries, frozen.CarryForwards,
                new(frozen.PreviewFingerprint, frozen.PayrollInputs ?? [], []), frozen.Overtime ?? [], frozen.FinancialAdjustments ?? []);
        }
        var population = await repository.PopulationAsync(start, end, ct);
        var source = await repository.SourcesAsync(start, end, ct);
        var carry = (await carryForwards.ReadForLockAsync(start, end, ct)).OrderBy(x => x.AdjustmentId).ToArray();
        if (carry.Length > MonthLockCoverage.MaximumCells || carry.Select(x => x.AdjustmentId).Distinct().Count() != carry.Length ||
            carry.Any(x => x.TargetMonthStart != start || x.TargetMonthEndExclusive != end)) throw AttendanceOperation.Conflict("month_lock_carry_source_conflict");
        var issues = new List<MonthLockIssue>(population.Issues); issues.AddRange(source.Issues);
        if (end > RosterSchedule.Today(actor.Now)) issues.Add(new("attendance_month_not_finished"));
        if (await repository.OverlapsAsync(start, end, existing?.Period.Id, ct)) issues.Add(new("attendance_period_overlap"));
        var rows = new List<DailyAttendanceSummary>(); var days = new List<DayEvidence>();
        foreach (var cell in population.Cells)
        {
            var saved = await repository.SummaryAsync(cell.EmployeeId, cell.BusinessDate, ct);
            AttendanceDayResponse day;
            try { day = await summaries.LoadAuthorizedDayAsync(cell.EmployeeId, cell.BusinessDate, ct); }
            catch (AuthenticationFailure failure) when (MonthLockSetup.IsMissingConfiguration(failure))
            {
                // A known missing setup requirement belongs in this global readiness preview.
                // Authorization, corrupt evidence and unexpected failures must still propagate.
                issues.Add(new(failure.Code, cell.EmployeeId, cell.BusinessDate));
                days.Add(new(cell, saved?.Id, MonthLockEvidence.Fingerprint(new { unavailable = failure.Code })));
                continue;
            }
            days.Add(new(cell, saved?.Id, MonthLockEvidence.Fingerprint(day)));
            if (day.EmployeeId != cell.EmployeeId || day.BusinessDate != cell.BusinessDate ||
                day.LatestSavedRevision?.Id != saved?.Id) throw AttendanceOperation.Conflict("month_lock_summary_identity_conflict");
            if (day.Facts.OpenSessions != 0) issues.Add(new("attendance_session_still_open", cell.EmployeeId, cell.BusinessDate));
            if (day.Policy.Status != "calculated" || day.Policy.PolicyVersionId is null || day.Policy.DayStatus is null or AttendanceDayStatus.Unverified or AttendanceDayStatus.Incomplete)
                issues.Add(new("policy_calculation_not_ready", cell.EmployeeId, cell.BusinessDate));
            if (saved is null)
            { issues.Add(new("attendance_summary_missing", cell.EmployeeId, cell.BusinessDate)); continue; }
            if (saved.EmployeeId != cell.EmployeeId || saved.AttendanceDate != cell.BusinessDate) throw AttendanceOperation.Conflict("month_lock_summary_identity_conflict");
            if (!AttendanceSummaryService.IsSavedRevisionCurrent(day)) issues.Add(new("attendance_summary_stale", cell.EmployeeId, cell.BusinessDate));
            if (saved.PolicyVersionId != day.Policy.PolicyVersionId || saved.CalculationVersion != day.Policy.CalculationVersion || saved.Status != day.Policy.DayStatus ||
                saved.PayableMinutes != day.Policy.PayableMinutes || saved.LateMinutes != day.Policy.LateMinutes || saved.OvertimeMinutes != day.Policy.OvertimeMinutes ||
                saved.WorkedMinutes != day.Facts.WorkedMinutes || saved.ShiftVersionId != day.Roster.ShiftVersionId)
                issues.Add(new("attendance_summary_policy_mismatch", cell.EmployeeId, cell.BusinessDate));
            if (saved.InputEvidenceReference is null) issues.Add(new("attendance_summary_evidence_missing", cell.EmployeeId, cell.BusinessDate));
            else
            {
                var frozen = await operation.ReadPrivateAsync<AttendanceDayResponse>(saved.InputEvidenceReference,
                    $"attendance:summary:{cell.EmployeeId:N}:{cell.BusinessDate:yyyy-MM-dd}", ct);
                // Reuse the calculation fingerprint contract, including roster/policy/facts/exceptions.
                if (frozen.EmployeeId != cell.EmployeeId || frozen.BusinessDate != cell.BusinessDate ||
                    !AttendanceSummaryService.IsSavedRevisionCurrent(frozen with { LatestSavedRevision = AttendanceSummaryService.Map(saved) }))
                    throw AttendanceOperation.Conflict("attendance_summary_evidence_conflict");
                if (!await repository.SummaryLinksMatchAsync(saved, frozen.Facts.SessionIds, ct))
                    issues.Add(new("attendance_summary_session_links_unavailable", cell.EmployeeId, cell.BusinessDate));
            }
            // Unclassified saved evidence is a readiness blocker, not a lockable row.
            // Entry requires a policy identity; projecting it here would abort the
            // preview instead of returning the policy_calculation_not_ready issue.
            if (saved.PolicyVersionId.HasValue) rows.Add(saved);
        }
        var employees = population.Cells.Select(x => x.EmployeeId).Distinct().Order().ToArray();
        var money = financial is null
            ? new MonthLockFinancialInspection(MonthLockEvidence.Fingerprint(new { dependency = "financial_reader_missing" }), [],
                employees.Select(x => new MonthLockIssue("attendance_financial_calculation_required", x)).ToArray())
            : await financial.InspectAsync(start, end, employees, actor, ct);
        MonthLockEvidence.ValidateFinancialInspection(money, start, employees);
        issues.AddRange(money.Issues);
        IReadOnlyList<AttendancePayrollFinancialMonthEntry> adjustmentEntries = [];
        if (adjustments is null) issues.Add(new("month_lock_financial_adjustment_reader_unavailable"));
        else
        {
            adjustmentEntries = await adjustments.ReadAsync(start, employees, actor, ct);
            MonthLockFinancialAdjustments.Validate(start, employees, money.PayrollInputs, adjustmentEntries);
        }
        issues.AddRange(MonthLockFinancialAdjustments.UnvaluedCarryForwards(carry, adjustmentEntries));
        IReadOnlyList<MonthLockOvertimeEntry> awards = [];
        if (source.Overtime is null) issues.Add(new("month_lock_overtime_sources_unavailable"));
        else if (!MonthLockOvertimeReadiness.Inspect(start, end, source.Overtime).Issues
            .Any(x => x.Code != "overtime_locked_handoff_unavailable"))
        {
            if (source.Overtime.Count == 0 || overtime is not null)
            {
                awards = source.Overtime.Count == 0 ? [] : await overtime!.ReadAsync(start, end, source.Overtime, actor, ct);
                MonthLockEvidence.ValidateOvertimeMembership(start, end, rows.Select(Entry).ToArray(), awards);
                MonthLockEvidence.RequireOvertimeDependencies(awards, source.Overtime);
                issues.RemoveAll(x => x.Code == "overtime_locked_handoff_unavailable" &&
                    awards.Any(a => a.Award.EmployeeId == x.EmployeeId && a.Award.SourceBusinessDate == x.BusinessDate));
            }
            else issues.Add(new("month_lock_overtime_reader_unavailable"));
        }
        var unique = issues.Distinct().OrderBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.EmployeeId).ThenBy(x => x.BusinessDate).ToArray();
        var fingerprint = MonthLockEvidence.Fingerprint(new { schema = "month-lock-preview-v1", start, end,
            periodId = existing?.Period.Id, revision = existing?.Period.Revision, version = existing?.Period.RowVersion,
            population.EvidenceFingerprint, source.Fingerprint, days, carry, money, awards, adjustmentEntries, issues = unique });
        var preview = new MonthLockPreview(start, end, existing?.Period.Id, existing is null ? null : Convert.ToBase64String(existing.Period.RowVersion),
            unique.Length == 0 ? "ready" : "not_ready", unique.Length == 0 && rows.Count == population.Cells.Count && rows.Count > 0,
            fingerprint, population.Cells.Select(x => x.EmployeeId).Distinct().Count(), population.Cells.Count,
            rows.Count(row => !unique.Any(x => x.EmployeeId == row.EmployeeId && x.BusinessDate == row.AttendanceDate)), carry.Length, unique.Length, unique.Take(200).ToArray());
        return new(preview, existing, rows, carry, money, awards, adjustmentEntries);
    }

    Task<MonthLockSnapshot> IMonthLockSnapshotReader.ReadRetainedAsync(MonthLockExisting existing,
        AttendanceActor actor, CancellationToken ct) => ReadSnapshot(existing, actor, ct);

    private async Task<MonthLockSnapshot> ReadSnapshot(MonthLockExisting existing, AttendanceActor actor, CancellationToken ct)
    {
        if (existing.Event?.ProtectedEvidenceReference is not string reference)
            throw AttendanceOperation.Conflict(existing.LegacyApprovalValid ? "month_lock_legacy_manifest_unavailable" : "month_lock_evidence_unavailable");
        var snapshot = await operation.ReadPrivateAsync<MonthLockSnapshot>(reference, MonthLockEvidence.Purpose(existing.Period.Id, existing.Event.Id), ct);
        MonthLockEvidence.Validate(existing, snapshot);
        if (snapshot.SchemaVersion >= 2)
        {
            if (financial is null) throw AttendanceOperation.Conflict("month_lock_financial_reader_unavailable");
            await financial.VerifyRetainedAsync(snapshot.PayrollInputs!, actor, ct);
        }
        if (snapshot.SchemaVersion >= 3)
        {
            var source = await repository.SourcesAsync(snapshot.MonthStart, snapshot.MonthEndExclusive, ct);
            if (source.Overtime is null || MonthLockOvertimeReadiness.Inspect(snapshot.MonthStart, snapshot.MonthEndExclusive,
                source.Overtime).Issues.Any(x => x.Code != "overtime_locked_handoff_unavailable"))
                throw AttendanceOperation.Conflict("month_lock_overtime_dependencies_unavailable");
            IReadOnlyList<MonthLockOvertimeEntry> current = [];
            if (source.Overtime.Count != 0)
            {
                if (overtime is null) throw AttendanceOperation.Conflict("month_lock_overtime_reader_unavailable");
                current = await overtime.ReadAsync(snapshot.MonthStart, snapshot.MonthEndExclusive, source.Overtime, actor, ct);
            }
            MonthLockEvidence.RequireOvertimeDependencies(current, source.Overtime);
            MonthLockEvidence.RequireOvertimeMatch(snapshot.Overtime!, current);
        }
        if (snapshot.SchemaVersion == 4)
        {
            if (adjustments is null) throw AttendanceOperation.Conflict("month_lock_financial_adjustment_reader_unavailable");
            var employees = snapshot.Summaries.Select(x => x.EmployeeId).Distinct().Order().ToArray();
            var current = await adjustments.ReadAsync(snapshot.MonthStart, employees, actor, ct);
            MonthLockFinancialAdjustments.Validate(snapshot.MonthStart, employees, snapshot.PayrollInputs!, current);
            MonthLockFinancialAdjustments.RequireRetainedMatch(snapshot.FinancialAdjustments!, current);
        }
        var carry = (await carryForwards.ReadForLockAsync(existing.Period.PeriodStart, existing.Period.PeriodEndExclusive, ct)).OrderBy(x => x.AdjustmentId).ToArray();
        if (!carry.SequenceEqual(snapshot.CarryForwards)) throw AttendanceOperation.Conflict("month_lock_carry_membership_conflict");
        return snapshot;
    }
    private static MonthLockSummaryEntry Entry(DailyAttendanceSummary row) => new(row.EmployeeId, row.AttendanceDate, row.Id,
        row.InputFingerprint, row.PolicyVersionId ?? throw AttendanceOperation.Conflict("policy_calculation_not_ready"), row.CalculationVersion);
    private static MonthLockResponse Map(AttendancePeriodLock period, AttendancePeriodLockEvent item, MonthLockSnapshot snapshot) =>
        new(period.Id, item.Id, item.Revision, period.PeriodStart, period.PeriodEndExclusive, "locked", Convert.ToBase64String(period.RowVersion),
            item.AtUtc, item.Actor, item.Reason, "direct_protected_manifest", snapshot.Summaries.Count, snapshot.CarryForwards.Count);
    private sealed record DayEvidence(MonthLockCell Cell, Guid? SummaryId, string CurrentDayFingerprint);
    private sealed record Inspection(MonthLockPreview Preview, MonthLockExisting? Existing, IReadOnlyList<DailyAttendanceSummary> Rows,
        IReadOnlyList<MonthLockCarryForwardEntry> Carry, MonthLockFinancialInspection Financial,
        IReadOnlyList<MonthLockOvertimeEntry> Overtime, IReadOnlyList<AttendancePayrollFinancialMonthEntry> Adjustments);
}
