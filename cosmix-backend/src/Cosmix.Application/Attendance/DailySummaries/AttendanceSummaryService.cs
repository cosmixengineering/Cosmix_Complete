using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.DailySummaries;

namespace Cosmix.Application.Attendance.DailySummaries;

public sealed class AttendanceSummaryService(AttendanceOperation operation, IAttendanceSummaryRepository repository,
    IIndividualRosterResolver rosters, IAttendancePolicyCalculator policies, IPrivateFileStore files)
{
    public Task<AttendanceDayReadResponse> ReadDayAvailabilityAsync(Guid employeeId, DateOnly date,
        AuthRequestContext context, CancellationToken ct)
    {
        Date(date);
        if (employeeId == Guid.Empty) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(AttendanceSummaryPermissions.Read, "attendance.summary.read", employeeId,
            context, (_, token) => LoadReadDay(employeeId, date, token), ct,
            resourceScopes: (_, token) => repository.ResourceScopesAsync(employeeId, date, date, token));
    }

    public Task<IReadOnlyList<AttendanceDayReadResponse>> ReadCalendarAvailabilityAsync(Guid employeeId,
        AttendanceDateRange range, AuthRequestContext context, CancellationToken ct)
    {
        Date(range.From); Date(range.To);
        if (employeeId == Guid.Empty) throw AttendanceOperation.Invalid();
        if (range.To < range.From || range.To.DayNumber - range.From.DayNumber > 30)
            throw AttendanceOperation.Invalid("attendance_range_limit");
        return operation.ReadAsync<IReadOnlyList<AttendanceDayReadResponse>>(AttendanceSummaryPermissions.Read,
            "attendance.calendar.read", employeeId, context, async (_, token) =>
            {
                var days = new List<AttendanceDayReadResponse>();
                for (var day = range.From.DayNumber; day <= range.To.DayNumber; day++)
                {
                    token.ThrowIfCancellationRequested();
                    days.Add(await LoadReadDay(employeeId, DateOnly.FromDayNumber(day), token));
                }
                return days;
            }, ct, resourceScopes: (_, token) => repository.ResourceScopesAsync(employeeId, range.From, range.To, token));
    }

    private async Task<AttendanceDayReadResponse> LoadReadDay(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var snapshot = await repository.LookupAsync(employeeId, date, ct);
        if (snapshot.IsLocked && snapshot.Summary?.InputEvidenceReference is null)
            return AttendanceDayReadResponse.MissingLockedSummary(employeeId, date);
        // Valid frozen records still undergo identity and fingerprint verification. Do not catch
        // corruption, storage failures or authorization errors as ordinary missing coverage.
        // HR must be able to inspect factual evidence and fix missing employment setup.
        // This narrow read-only fallback does not enable calculation, self-service or payroll.
        return AttendanceDayReadResponse.Available(await LoadDay(employeeId, date, ct,
            allowMissingAssignment: true, snapshot: snapshot));
    }

    public Task<AttendanceDayResponse> MyDayAsync(DateOnly date, AuthRequestContext context, CancellationToken ct) =>
        ReadDay(null, date, true, context, ct);
    public Task<AttendanceDayResponse> DayAsync(Guid employeeId, DateOnly date, AuthRequestContext context, CancellationToken ct) =>
        ReadDay(employeeId, date, false, context, ct);
    private Task<AttendanceDayResponse> ReadDay(Guid? employeeId, DateOnly date, bool self, AuthRequestContext context, CancellationToken ct)
    {
        Date(date);
        return operation.ReadAsync(self ? AttendanceSummaryPermissions.Self : AttendanceSummaryPermissions.Read,
            "attendance.summary.read", employeeId, context,
            (actor, token) => LoadDay(employeeId ?? actor.Identity.EmployeeId, date, token), ct,
            resourceScopes: self ? null : (_, token) => repository.ResourceScopesAsync(employeeId!.Value, date, date, token), self: self);
    }
    public Task<IReadOnlyList<AttendanceDayResponse>> CalendarAsync(Guid? employeeId, AttendanceDateRange range,
        bool self, AuthRequestContext context, CancellationToken ct)
    {
        Date(range.From); Date(range.To);
        if (range.To < range.From || range.To.DayNumber - range.From.DayNumber > 30) throw AttendanceOperation.Invalid("attendance_range_limit");
        if (!self && !employeeId.HasValue) throw AttendanceOperation.Invalid();
        return operation.ReadAsync<IReadOnlyList<AttendanceDayResponse>>(self ? AttendanceSummaryPermissions.Self : AttendanceSummaryPermissions.Read,
            "attendance.calendar.read", employeeId, context, async (actor, token) =>
            {
                var result = new List<AttendanceDayResponse>();
                for (var day = range.From.DayNumber; day <= range.To.DayNumber; day++)
                    result.Add(await LoadDay(employeeId ?? actor.Identity.EmployeeId, DateOnly.FromDayNumber(day), token));
                return result;
            }, ct, resourceScopes: self ? null : (_, token) => repository.ResourceScopesAsync(employeeId!.Value, range.From, range.To, token), self: self);
    }
    public Task<PagedResult<AttendanceSummaryRevision>> HistoryAsync(Guid? employeeId, DateOnly date, int page, int pageSize,
        bool self, AuthRequestContext context, CancellationToken ct)
    {
        Date(date);
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100 || !self && !employeeId.HasValue) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(self ? AttendanceSummaryPermissions.Self : AttendanceSummaryPermissions.Read,
            "attendance.summary.history", employeeId, context, async (actor, token) =>
            {
                var history = await repository.HistoryAsync(employeeId ?? actor.Identity.EmployeeId, date, page, pageSize, token);
                return new PagedResult<AttendanceSummaryRevision>(history.Items.Select(Map).ToArray(), history.TotalCount, page, pageSize);
            }, ct, resourceScopes: self ? null : (_, token) => repository.ResourceScopesAsync(employeeId!.Value, date, date, token), self: self);
    }
    public Task<AttendanceSummaryRevision> RecalculateAsync(Guid employeeId, RecalculateAttendanceRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        Date(request.BusinessDate);
        if (request.ExpectedRevision < 0) throw AttendanceOperation.Invalid();
        return operation.WriteAsync(AttendanceSummaryPermissions.Calculate, "attendance.summary.calculate", employeeId, key,
            request, request.Reason, context, (actor, token) => AppendAuthorizedRevisionAsync(employeeId, request, actor, token),
            ct, resourceScopes: (_, token) => repository.ResourceScopesAsync(employeeId, request.BusinessDate, request.BusinessDate, token));
    }

    /// <summary>Assembly-internal calculation step for a caller that ALREADY owns the live authorized
    /// transaction and all employee/date/source scopes. It opens no transaction, authorizes no actor,
    /// saves no changes and commits nothing. Any new source rows must be flushed inside that same
    /// transaction before calling; the outer operation owns audit, receipt and final atomic commit.</summary>
    internal async Task<AttendanceChange<AttendanceSummaryRevision>> AppendAuthorizedRevisionAsync(Guid employeeId,
        RecalculateAttendanceRequest request, AttendanceActor actor, CancellationToken token)
    {
        Date(request.BusinessDate);
        if (employeeId == Guid.Empty || request.ExpectedRevision < 0) throw AttendanceOperation.Invalid();
        if (await repository.IsLockedAsync(request.BusinessDate, token)) throw AttendanceOperation.Conflict("attendance_period_locked");
        var latest = await repository.LatestAsync(employeeId, request.BusinessDate, token);
        if ((latest?.Revision ?? 0) != request.ExpectedRevision) throw AttendanceOperation.Conflict("attendance_summary_revision_conflict");
        var day = await LoadDay(employeeId, request.BusinessDate, token);
        if (day.Facts.OpenSessions != 0) throw AttendanceOperation.Conflict("attendance_session_still_open");
        var fingerprint = Fingerprint(day);
        if (latest?.InputFingerprint == fingerprint)
            return new AttendanceChange<AttendanceSummaryRevision>(latest.Id, Map(latest), Map(latest));
        var evidence = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(day), Purpose(employeeId, request.BusinessDate), token);
        var summary = new DailyAttendanceSummary(employeeId, request.BusinessDate, checked(request.ExpectedRevision + 1),
            day.Policy.DayStatus ?? AttendanceDayStatus.Unverified, day.Policy.PolicyVersionId, day.Roster.ShiftVersionId,
            day.Policy.CalculationVersion ?? AttendanceFacts.CalculationVersion, fingerprint,
            day.Roster.ConfiguredScheduledMinutes ?? 0, day.Facts.WorkedMinutes, day.Policy.PayableMinutes,
            day.Policy.LateMinutes, day.Policy.OvertimeMinutes, day.ExceptionCount, actor.Now, latest?.Id,
            AttendanceOperation.Reference(evidence));
        var sessions = (await repository.SessionsAsync(employeeId, request.BusinessDate, token))
            .Where(x => day.Facts.SessionIds.Contains(x.Id)).ToArray();
        // Per-session integer allocation is explanatory only. Total minutes are rounded once
        // after summing exact elapsed time; the protected inputs preserve sub-minute evidence.
        var links = sessions.Select(x => new AttendanceSummarySession(summary, x,
            x.EndsAtUtc.HasValue ? (int)(x.EndsAtUtc.Value - x.StartsAtUtc).TotalMinutes : 0)).ToArray();
        repository.Add(summary, links);
        return new AttendanceChange<AttendanceSummaryRevision>(summary.Id, latest is null ? null : Map(latest), Map(summary));
    }
    /// <summary>For trusted internal use cases only. The caller must already own the shared authorization
    /// transaction and authorize the employee/date plus every historical source scope. Does not authorize by itself.</summary>
    public Task<AttendanceDayResponse> LoadAuthorizedDayAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    { Date(date); return LoadDay(employeeId, date, ct); }

    /// <summary>Trusted factual dashboard read under the caller's existing authorization transaction.
    /// An absent employment assignment leaves classification unconfigured while retaining accepted
    /// session facts. Locked evidence, invalid assignment history and other failures still fail closed.</summary>
    public async Task<AttendanceDayResponse?> LoadAuthorizedDashboardDayAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        Date(date);
        var snapshot = await repository.LookupAsync(employeeId, date, ct);
        // Expected incomplete coverage is a result on this dashboard-only path, not an exception
        // that pauses the debugger while its authorization transaction holds locks.
        if (snapshot.IsLocked && snapshot.Summary?.InputEvidenceReference is null) return null;
        return await LoadDay(employeeId, date, ct, allowMissingAssignment: true, snapshot);
    }

    private async Task<AttendanceDayResponse> LoadDay(Guid employeeId, DateOnly date, CancellationToken ct,
        bool allowMissingAssignment = false, AttendanceSummaryLookup? snapshot = null)
    {
        var locked = snapshot?.IsLocked ?? await repository.IsLockedAsync(date, ct);
        var saved = snapshot is null ? await repository.LatestAsync(employeeId, date, ct) : snapshot.Summary;
        if (locked)
        {
            if (saved?.InputEvidenceReference is null) throw AttendanceOperation.Conflict("locked_attendance_summary_unavailable");
            var frozen = await operation.ReadPrivateAsync<AttendanceDayResponse>(saved.InputEvidenceReference, Purpose(employeeId, date), ct);
            if (frozen.EmployeeId != employeeId || frozen.BusinessDate != date) throw AttendanceOperation.Conflict("attendance_summary_identity_conflict");
            if (Fingerprint(frozen) != saved.InputFingerprint) throw AttendanceOperation.Conflict("attendance_summary_evidence_conflict");
            return frozen with { IsLocked = true, LatestSavedRevision = Map(saved) };
        }
        var facts = AttendanceFacts.Calculate(employeeId, date, await repository.SessionsAsync(employeeId, date, ct));
        EffectiveRosterResponse roster;
        AttendancePolicyResult? policy = null;
        try { roster = await rosters.ResolveAsync(employeeId, date, ct); }
        catch (AuthenticationFailure error) when (allowMissingAssignment && error.StatusCode == 409 &&
            error.Code == "employee_assignment_unavailable")
        {
            roster = new(employeeId, date, "employee_assignment_unavailable", RosterSchedule.TimeZoneId);
            policy = new("schedule_configuration_required", null, null, null, null, null, null,
                ["No effective employment assignment exists for this business date. Accepted session evidence is factual only; attendance classification requires configuration."]);
        }
        policy ??= await policies.CalculateAsync(employeeId, date, facts, roster, ct);
        ValidatePolicy(policy);
        return new(employeeId, date, roster.TimeZone,
            facts.OpenSessions != 0 ? "open_session" : facts.SessionIds.Count != 0 ? "closed_session_evidence" : "no_accepted_session_evidence",
            facts, roster, policy, await repository.ExceptionCountAsync(employeeId, date, ct), false, saved is null ? null : Map(saved));
    }
    private static void ValidatePolicy(AttendancePolicyResult policy)
    {
        var complete = policy.Status == "calculated";
        if (complete ? policy.PolicyVersionId is null || policy.PolicyVersionId == Guid.Empty || string.IsNullOrWhiteSpace(policy.CalculationVersion) ||
            policy.CalculationVersion.Length > 100 || policy.CalculationVersion.Any(char.IsControl) ||
            policy.PayableMinutes is null or < 0 || policy.LateMinutes is null or < 0 || policy.OvertimeMinutes is null or < 0 ||
            policy.DayStatus is null || !Enum.IsDefined(policy.DayStatus.Value) :
            policy.PolicyVersionId.HasValue || policy.PayableMinutes.HasValue || policy.LateMinutes.HasValue || policy.OvertimeMinutes.HasValue || policy.DayStatus.HasValue)
            throw AttendanceOperation.Conflict("invalid_policy_calculation_contract");
    }
    private static string Fingerprint(AttendanceDayResponse day) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { day.Facts, day.Roster, day.Policy, day.ExceptionCount })));
    public static bool IsSavedRevisionCurrent(AttendanceDayResponse day) =>
        day.LatestSavedRevision is not null && day.LatestSavedRevision.InputFingerprint == Fingerprint(day);
    private static void Date(DateOnly date) { if (date == default || date == DateOnly.MaxValue) throw AttendanceOperation.Invalid("invalid_business_date"); }
    private static string Purpose(Guid id, DateOnly date) => $"attendance:summary:{id:N}:{date:yyyy-MM-dd}";
    public static AttendanceSummaryRevision Map(DailyAttendanceSummary s) => new(s.Id, s.Revision,
        s.PolicyVersionId.HasValue ? s.Status.ToString() : "factual_only", s.PolicyVersionId, s.ShiftVersionId,
        s.WorkedMinutes, s.PayableMinutes, s.LateMinutes, s.OvertimeMinutes, s.CalculationVersion, s.InputFingerprint, s.CalculatedAtUtc);
}
