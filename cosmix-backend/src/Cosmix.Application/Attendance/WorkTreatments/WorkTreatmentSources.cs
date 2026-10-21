using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Policies.Advanced;

namespace Cosmix.Application.Attendance.WorkTreatments;

public sealed class WorkTreatmentSources(IWorkTreatmentRepository repository, IIndividualRosterResolver rosters,
    IAdvancedAttendancePolicyReader policies, IWorkTreatmentProposalProtector protector,
    IWorkTreatmentAnnualLateReader? annualLate = null)
{
    public async Task<WorkTreatmentSource> LoadAsync(Guid employeeId, WorkTreatmentKind kind, DateOnly from, DateOnly to,
        CancellationToken ct, Guid? annualSourceId = null) {
        repository.RequireTransaction(); WorkTreatmentRules.Dates(from, to);
        if (employeeId == Guid.Empty) throw AttendanceOperation.Invalid();
        var sessions = await repository.SessionsAsync(employeeId, from, to, ct);
        var days = new List<WorkTreatmentDay>();
        for (var date = from; date <= to; date = date.AddDays(1)) {
            var roster = await rosters.ResolveAsync(employeeId, date, ct);
            if (roster.EmployeeId != employeeId || roster.BusinessDate != date) throw AttendanceOperation.Conflict("work_treatment_roster_identity_conflict");
            days.Add(new(date, await repository.EmploymentAsync(employeeId, date, ct), roster,
                await policies.ReadAsync(employeeId, WorkTreatmentRules.Family(kind), date, ct),
                sessions.Where(x => x.BusinessDate == date).OrderBy(x => x.Key).ThenBy(x => x.Id).ToArray(),
                await repository.LockedAsync(date, ct)));
        }
        var scopes = Canonical(employeeId, (await repository.ScopesAsync(employeeId, from, to, ct))
            .Concat(await TemporalScopesAsync(employeeId, days, ct, kind == WorkTreatmentKind.ContinuousDuty)).ToArray());
        var annual = kind == WorkTreatmentKind.AnnualLate && annualSourceId.HasValue && annualLate is not null
            ? await annualLate.ReadAsync(employeeId, annualSourceId.Value, ct) : null;
        if (annual is not null && (annual.EmployeeId != employeeId || annual.SourceId != annualSourceId || annual.From == default || annual.To < annual.From ||
            annual.ExactLateCount <= 120 || annual.PolicyVersionId == Guid.Empty ||
            !AttendanceScopeEvaluation.IsValid(annual.OriginalScopes) || annual.OriginalScopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId != employeeId) ||
            !WorkTreatmentRules.HashValid(annual.SourceFingerprint) || !WorkTreatmentRules.HashValid(annual.PolicyFingerprint)))
            throw AttendanceOperation.Conflict("annual_late_verified_source_invalid");
        if (annual is not null) scopes = Canonical(employeeId, scopes.Concat(annual.OriginalScopes).Concat(await repository.ScopesAsync(employeeId, annual.From, annual.To, ct)).ToArray());
        var state = days.Any(x => x.Employment is null) ? "historical_employment_unavailable" :
            days.Any(x => x.Policy.State != "configured") ? "policy_not_effective" :
            kind == WorkTreatmentKind.AnnualLate && annual is null ? "annual_evidence_unavailable" :
            days.Any(x => x.Roster.ShiftVersionId is null) ? "roster_unavailable" :
            sessions.Count == 0 ? "no_actual_sessions" : sessions.Any(x => !x.Verified || x.EndUtc is null) ? "unresolved_actual_sessions" : "source_available";
        var explanation = state switch {
            "no_actual_sessions" => "No real work-session source exists in this range. Dated written instructions may be reviewed; actual-work credit cannot be inferred.",
            "annual_evidence_unavailable" => "A verified pending C04 annual KPI request has not been supplied. Allegations remain separate from retained late counts and reviewed outcomes.",
            "policy_not_effective" => "An explicit approved typed policy is missing, conflicting or inapplicable on one or more source dates.",
            "historical_employment_unavailable" => "Historical employee membership is unavailable on one or more source dates.",
            "roster_unavailable" => "Exact dated roster context is unavailable; no time treatment is inferred.",
            "unresolved_actual_sessions" => "One or more source sessions are open or unverified. Only independently verified closed intervals may be proposed.",
            _ => "Real dated sources are available for a signed factual proposal and independent review. No pay or punishment is implied."
        };
        var source = new WorkTreatmentSource(employeeId, kind, from, to, state, explanation, days, scopes, "") { AnnualLate = annual };
        return source with { Fingerprint = WorkTreatmentRules.Fingerprint(source) };
    }
    public WorkTreatmentSourceResponse Sign(WorkTreatmentSource source, AttendanceActor actor) {
        // Ticket lifetime is a protocol validity bound, not a work/policy threshold.
        var expiry = actor.Now.AddMinutes(15);
        return new(source, protector.Protect(new(actor.Identity.AccountId, actor.Identity.SessionId, source.EmployeeId,
            source.Kind, source.From, source.To, source.Fingerprint, expiry)), expiry);
    }
    public void VerifyTicket(WorkTreatmentRequest p, Guid employeeId, AttendanceActor actor, bool allowExpired) {
        var t = protector.Unprotect(p.SourceTicket);
        if (t.AccountId != actor.Identity.AccountId || t.SessionId != actor.Identity.SessionId || t.EmployeeId != employeeId ||
            t.Kind != p.Kind || t.From != p.From || t.To != p.To || t.Fingerprint != p.ExpectedSourceFingerprint ||
            !allowExpired && t.ExpiresAtUtc <= actor.Now) throw AttendanceOperation.Conflict("work_treatment_signed_source_mismatch");
    }
    public async Task<IReadOnlyList<AttendanceResourceScope>> CurrentScopesAsync(Guid employeeId, DateOnly from, DateOnly to,
        DateTimeOffset now, CancellationToken ct) {
        var roster = await rosters.ResolveAsync(employeeId, from, ct);
        DateOnly currentDate;
        try { currentDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(roster.TimeZone)).DateTime); }
        catch (TimeZoneNotFoundException) { throw AttendanceOperation.Conflict("work_treatment_business_zone_unavailable"); }
        catch (InvalidTimeZoneException) { throw AttendanceOperation.Conflict("work_treatment_business_zone_unavailable"); }
        var physical = new List<AttendanceResourceScope>();
        foreach (var s in await repository.SessionsAsync(employeeId, from, to, ct))
            physical.AddRange(await WindowScopesAsync(employeeId, s.StartUtc, s.EndUtc ?? s.StartUtc, roster.TimeZone, ct));
        if (roster.StartsAt is { } start && roster.EndsAt is { } end)
            physical.AddRange(await WindowScopesAsync(employeeId, start, end, roster.TimeZone, ct));
        return Canonical(employeeId, (await repository.ScopesAsync(employeeId, from, to, ct)).Concat(physical)
            .Concat(await repository.ScopesAsync(employeeId, currentDate, currentDate, ct)).ToArray());
    }
    public Task<IReadOnlyList<AttendanceResourceScope>> RetainedDateScopesAsync(WorkTreatmentSource source, CancellationToken ct) =>
        TemporalScopesAsync(source.EmployeeId, source.Days, ct, source.Kind == WorkTreatmentKind.ContinuousDuty);
    public async Task<IReadOnlyList<AttendanceResourceScope>> ProposalWindowScopesAsync(Guid employeeId, WorkTreatmentRequest p, CancellationToken ct) {
        var roster = await rosters.ResolveAsync(employeeId, p.From, ct);
        return await WindowScopesAsync(employeeId, p.StartUtc, p.EndUtc, roster.TimeZone, ct);
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> TemporalScopesAsync(Guid employeeId, IEnumerable<WorkTreatmentDay> days, CancellationToken ct, bool includeFollowingDay) {
        var result = new List<AttendanceResourceScope>();
        foreach (var day in days) {
            if (includeFollowingDay) result.AddRange(await repository.ScopesAsync(employeeId, day.BusinessDate.AddDays(1), day.BusinessDate.AddDays(1), ct));
            if (day.Roster.StartsAt is { } start && day.Roster.EndsAt is { } end) {
                var earlyTicks = day.Policy.Rules?.EarlyCredit?.MaximumPreStartSeconds is { } seconds ? WorkTreatmentRules.Seconds(seconds) : 0;
                result.AddRange(await WindowScopesAsync(employeeId, start.AddTicks(-earlyTicks), end, day.Roster.TimeZone, ct));
            }
            foreach (var s in day.Sessions)
                result.AddRange(await WindowScopesAsync(employeeId, s.StartUtc, s.EndUtc ?? s.StartUtc, day.Roster.TimeZone, ct));
        }
        return result;
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> WindowScopesAsync(Guid employeeId, DateTimeOffset start, DateTimeOffset end,
        string zoneId, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(zoneId)) throw AttendanceOperation.Conflict("work_treatment_business_zone_unavailable");
        try {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
            var from = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, zone).DateTime);
            var to = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(end > start ? end.AddTicks(-1) : start, zone).DateTime);
            return await repository.ScopesAsync(employeeId, from, to, ct);
        } catch (TimeZoneNotFoundException) { throw AttendanceOperation.Conflict("work_treatment_business_zone_unavailable"); }
        catch (InvalidTimeZoneException) { throw AttendanceOperation.Conflict("work_treatment_business_zone_unavailable"); }
    }
    public static IReadOnlyList<AttendanceResourceScope> Canonical(Guid employeeId, IReadOnlyList<AttendanceResourceScope> scopes) {
        if (!AttendanceScopeEvaluation.IsValid(scopes) || scopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId != employeeId))
            throw AttendanceOperation.Conflict("work_treatment_scope_limit");
        return scopes.Select(x => new AttendanceResourceScope(x.Alternatives.Distinct().OrderBy(r => JsonSerializer.Serialize(r), StringComparer.Ordinal).ToArray()))
            .DistinctBy(x => JsonSerializer.Serialize(x)).OrderBy(x => JsonSerializer.Serialize(x), StringComparer.Ordinal).ToArray();
    }
}
