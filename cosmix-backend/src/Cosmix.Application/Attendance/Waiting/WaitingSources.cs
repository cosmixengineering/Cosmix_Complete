using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.Attendance.Waiting;

/// <summary>Factual source reads only. Caller owns authorization/transaction; no OT valuation is called.</summary>
public sealed class WaitingSources(IOvertimeRepository duty, IIndividualRosterResolver rosters,
    IAttendanceSummaryRepository summaries, ILeaveAttendanceContextReader leave,
    IAdvancedAttendancePolicyReader policies, ICorrectionReviewRepository corrections,
    IStaffTaxonomyResolver taxonomy, AttendanceOperation operation)
{
    public async Task<WaitingSource> ReadAsync(Guid waitingEmployee, Guid causingEmployee, DateOnly date, CancellationToken ct)
    {
        if (waitingEmployee == Guid.Empty || causingEmployee == Guid.Empty || waitingEmployee == causingEmployee)
            throw AttendanceOperation.Invalid("invalid_waiting_pair");
        var waiting = await Worker(waitingEmployee, date, ct);
        var causing = await Worker(causingEmployee, date, ct);
        return new(waiting, causing, WaitingAttribution.Hash(new { waiting, causing }));
    }
    public async Task<IReadOnlyList<WaitingSource>> PairAsync(WaitingPairInput input, CancellationToken ct)
    {
        ValidatePair(input);
        var result = new List<WaitingSource>();
        for (var date = input.EffectiveFrom; date < input.EffectiveTo; date = date.AddDays(1))
            result.Add(await ReadAsync(input.WaitingEmployeeId, input.CausingEmployeeId, date, ct));
        return result;
    }
    public async Task<IReadOnlyList<AttendanceResourceScope>> ScopesAsync(Guid waiting, Guid causing, DateOnly from, DateOnly to, CancellationToken ct)
    {
        ValidateRange(from, to, 31);
        return (await summaries.ResourceScopesAsync(waiting, from, to.AddDays(-1), ct))
            .Concat(await summaries.ResourceScopesAsync(causing, from, to.AddDays(-1), ct)).ToArray();
    }
    private async Task<WaitingWorkerSource> Worker(Guid employee, DateOnly date, CancellationToken ct)
    {
        var roster = await rosters.ResolveAsync(employee, date, ct);
        var employment = await duty.EmploymentAsync(employee, date, ct);
        var sessions = await duty.SessionSourcesAsync(employee, date, ct);
        if (sessions.Count > 512 || sessions.Select(x => x.SessionKey).Distinct().Count() != sessions.Count)
            throw AttendanceOperation.Conflict("waiting_duty_source_ambiguous");
        foreach (var session in sessions.Where(x => x.ValidDutyEvidence && x.CorrectionId.HasValue))
        {
            var row = await corrections.FindAsync(employee, session.CorrectionId!.Value, ct);
            if (row is null || !Guid.TryParse(row.Approval.RequesterUserId, out var requester) ||
                row.Correction.AttendanceDate != date || row.AppliedSessionRevisionId != session.Id || row.AppliedSummaryRevisionId is null)
                throw AttendanceOperation.Conflict("waiting_correction_source_unavailable");
            var retained = await operation.ReadPrivateAsync<CorrectionEvidenceSnapshot>(row.Correction.PrivateEvidenceReference,
                AttendanceCorrectionService.Purpose(employee), ct);
            if (!AttendanceCorrectionService.Matches(row, retained, requester) || retained.SchemaVersion < 3)
                throw AttendanceOperation.Conflict("waiting_correction_source_unavailable");
        }
        var scopes = await summaries.ResourceScopesAsync(employee, date, date, ct);
        if (!AttendanceScopeEvaluation.IsValid(scopes) || scopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId != employee))
            throw AttendanceOperation.Conflict("waiting_historical_scope_unavailable");
        if (employment is not null)
        {
            var endDate = roster.EndsAt.HasValue ? DateOnly.FromDateTime(roster.EndsAt.Value.UtcDateTime).AddDays(1) : date.AddDays(1);
            if (endDate <= date) endDate = date.AddDays(1);
            var mapping = await RosterStaffTaxonomy.ResolveAsync(taxonomy, employment.StaffCategory, date, endDate, ct);
            if (!RosterStaffTaxonomy.Matches(employment.StaffCategory, mapping, roster.Taxonomy))
                throw AttendanceOperation.Conflict("waiting_taxonomy_source_changed");
        }
        return new(employee, date, employment, roster, sessions.OrderBy(x => x.SessionKey).ToArray(),
            await leave.ReadAsync(employee, date, ct), await policies.ReadAsync(employee, AdvancedAttendanceFamily.Waiting, date, ct), scopes);
    }
    public static WaitingRules RequirePolicy(WaitingSource source)
    {
        foreach (var worker in new[] { source.Waiting, source.Causing })
        {
            var p = worker.Policy;
            if (p.State != "configured" || p.EmployeeId != worker.EmployeeId || p.BusinessDate != worker.Date ||
                p.Family != AdvancedAttendanceFamily.Waiting || p.PolicyVersionId is null || p.PolicyVersionId == Guid.Empty ||
                p.Rules?.Waiting is null || !AdvancedAttendanceRulesCodec.Validate(p.Rules).IsValid ||
                p.Rules.ApplicableStaffCategoryCodes?.Contains(worker.Employment?.StaffCategory ?? "", StringComparer.Ordinal) != true)
                throw AttendanceOperation.Conflict("waiting_policy_unavailable");
        }
        if (source.Waiting.Policy.PolicyVersionId != source.Causing.Policy.PolicyVersionId ||
            WaitingAttribution.Hash(source.Waiting.Policy.Rules!) != WaitingAttribution.Hash(source.Causing.Policy.Rules!))
            throw AttendanceOperation.Conflict("waiting_pair_policy_conflict");
        return source.Waiting.Policy.Rules!.Waiting!;
    }
    public async Task RequireHistoricalAsync(WaitingSource source, Guid? site, bool actualDuty, CancellationToken ct)
    {
        RequirePolicy(source);
        foreach (var worker in new[] { source.Waiting, source.Causing })
        {
            var r = worker.Roster; var e = worker.Employment;
            if (e is null || r.EmployeeId != worker.EmployeeId || r.BusinessDate != worker.Date || r.ShiftVersionId is null ||
                r.StartsAt is null || r.EndsAt is null || r.EndsAt <= r.StartsAt || r.SiteId != site || e.SiteId != site ||
                r.BranchId != e.BranchId || worker.Leave.Status != LeaveContextStatus.Complete)
                throw AttendanceOperation.Conflict("waiting_historical_duty_unavailable");
            if (!await corrections.HistoricallyEligibleAsync(worker.EmployeeId, r.StartsAt.Value, r.EndsAt.Value, ct) ||
                !await corrections.PreservesAssignmentAsync(worker.EmployeeId, e.AssignmentId, site, r.StartsAt.Value, r.EndsAt.Value, ct))
                throw AttendanceOperation.Conflict("waiting_historical_assignment_changed");
            if (actualDuty && !worker.Sessions.Any(x => x.ValidDutyEvidence && x.EndsAtUtc.HasValue && x.SiteId == site &&
                x.BranchId == r.BranchId && x.ShiftVersionId == r.ShiftVersionId))
                throw AttendanceOperation.Conflict("waiting_actual_duty_unverified");
        }
        if (source.Waiting.Roster.BranchId != source.Causing.Roster.BranchId)
            throw AttendanceOperation.Conflict("waiting_pair_location_conflict");
    }
    public static IReadOnlyList<WaitingInterval> DutyIntervals(WaitingWorkerSource worker) => worker.Sessions
        .Where(x => x.ValidDutyEvidence && x.EndsAtUtc.HasValue && x.SiteId == worker.Roster.SiteId &&
            x.BranchId == worker.Roster.BranchId && x.ShiftVersionId == worker.Roster.ShiftVersionId)
        .Select(x => new WaitingInterval(x.StartsAtUtc, x.EndsAtUtc!.Value)).ToArray();
    public static IReadOnlyList<WaitingInterval> DelayIntervals(WaitingWorkerSource causer)
    {
        var duty = DutyIntervals(causer);
        if (duty.Count == 0 || causer.Roster.StartsAt is null || causer.Roster.EndsAt is null)
            throw AttendanceOperation.Conflict("waiting_actual_duty_unverified");
        var arrival = duty.Min(x => x.StartsAtUtc);
        var end = arrival < causer.Roster.EndsAt.Value ? arrival : causer.Roster.EndsAt.Value;
        return end <= causer.Roster.StartsAt.Value ? [] : [new(causer.Roster.StartsAt.Value.ToUniversalTime(), end.ToUniversalTime())];
    }
    public static IReadOnlyList<WaitingInterval> Excused(WaitingSource source) => source.Waiting.Leave.Effects
        .Concat(source.Causing.Leave.Effects).Select(x => new WaitingInterval(x.StartsAtUtc, x.EndsAtUtc)).ToArray();
    public static void ValidateRange(DateOnly from, DateOnly to, int max)
    {
        if (from.Year < 2000 || to.Year > 2100 || to <= from || to.DayNumber - from.DayNumber > max)
            throw AttendanceOperation.Invalid("invalid_waiting_date_range");
    }
    public static void Text(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > max || text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw AttendanceOperation.Invalid("invalid_waiting_evidence");
    }
    public static void ValidatePair(WaitingPairInput input)
    {
        if (input is null || input.WaitingEmployeeId == Guid.Empty || input.CausingEmployeeId == Guid.Empty ||
            input.WaitingEmployeeId == input.CausingEmployeeId || input.SiteId == Guid.Empty || input.PreviousPairId == Guid.Empty)
            throw AttendanceOperation.Invalid("invalid_waiting_pair");
        ValidateRange(input.EffectiveFrom, input.EffectiveTo, 31);
        Text(input.TaskDependencyReference, 150); Text(input.WrittenInstruction, 4000); Text(input.Reason, 1000);
    }
    public static void ValidateRequest(SubmitWaitingRequest input)
    {
        if (input is null || input.PairId == Guid.Empty || input.PreviousRequestId == Guid.Empty ||
            input.ProductiveAlternateWork is null || input.ProductiveAlternateWork.Count > 64 || input.SupportingEvidence is null ||
            input.SupportingEvidence.Count > 16 || input.SupportingEvidence.Any(x => x is null || x.SourceId == Guid.Empty || !Enum.IsDefined(x.Kind)) ||
            input.SupportingEvidence.Distinct().Count() != input.SupportingEvidence.Count)
            throw AttendanceOperation.Invalid("invalid_waiting_request");
        ValidateRange(input.BusinessDate, input.BusinessDate.AddDays(1), 1);
        WaitingAttribution.Union([input.ClaimedInterval]); WaitingAttribution.Union(input.ProductiveAlternateWork);
        if ((input.ClaimedInterval.EndsAtUtc - input.ClaimedInterval.StartsAtUtc).Ticks > TimeSpan.TicksPerDay * 2)
            throw AttendanceOperation.Invalid("waiting_interval_limit");
        Text(input.WrittenEvidence, 4000); Text(input.Reason, 1000);
    }
}
