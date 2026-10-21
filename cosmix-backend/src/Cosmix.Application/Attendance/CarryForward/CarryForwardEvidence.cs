using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.Calculation;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Attendance.CarryForward;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.CarryForward;

/// <summary>Validates retained evidence, never grants access. Every public call owns a fresh bounded
/// validation graph, so an earlier request cannot cache authority or mutable source state.</summary>
public sealed class CarryForwardEvidence(AttendanceOperation operation, ICarryForwardRepository repository,
    IOfficeAttendanceSourceReader offices, IIndividualRosterResolver rosters, IAttendanceCalculationSnapshotProducer calculations)
{
    public static string ProposalPurpose(Guid employee) => $"attendance:carry-forward:proposal:{employee:N}";
    public static string InterpretationPurpose(Guid employee) => $"attendance:carry-forward:interpretation:{employee:N}";
    public static string Fingerprint<T>(T value) => MonthLockEvidence.Fingerprint(value);
    public static string SourceFingerprint(CarryForwardSourceResponse source) => Fingerprint(source with { SourceFingerprint = "" });
    public static string InterpretationFingerprint(IReadOnlyList<CarryForwardSession> sessions) => Fingerprint(sessions.OrderBy(x => x.StartsAtUtc).ThenBy(x => x.SessionKey).ToArray());

    public async Task<(CarryForwardFrozenRows Rows, AttendanceDayResponse Day)> FrozenAsync(Guid employee, DateOnly date, CancellationToken ct) =>
        await Frozen(employee, date, new(), ct);
    public Task<CarryForwardInterpretationSnapshot> AdjustmentAsync(AttendanceCarryForwardAdjustment row, CancellationToken ct) => Adjustment(row, new(), ct);
    public async Task<IReadOnlyList<MonthLockCarryForwardEntry>> MonthAsync(DateOnly start, DateOnly end, CancellationToken ct) => await Month(start, end, new(), ct);

    public async Task<(CarryForwardRequestRow Row, CarryForwardProposalSnapshot Snapshot)> RequestAsync(Guid employee, Guid id, CancellationToken ct)
    {
        var row = await repository.RequestAsync(employee, id, ct) ?? throw AttendanceOperation.Missing();
        var snapshot = await operation.ReadPrivateAsync<CarryForwardProposalSnapshot>(row.Correction.PrivateEvidenceReference, ProposalPurpose(employee), ct);
        var c = row.Correction; var a = row.Approval; var p = snapshot.Proposal; var s = snapshot.Source;
        if ((snapshot.AlternativeEvidence is not null ? snapshot.SchemaVersion != 3 : s.OfficeSource is null ? snapshot.SchemaVersion != 1 : snapshot.SchemaVersion != 2) ||
            !ValidAlternative(snapshot.AlternativeEvidence, p, employee) ||
            !ValidLocation(s.EmployeeId, s.SourceBusinessDate, s.OfficeSource, s.Sessions) ||
            !ValidLocation(s.EmployeeId, s.SourceBusinessDate, s.OfficeSource, snapshot.ProposedInterpretation) ||
            snapshot.SubmissionId == Guid.Empty || a.SubjectId != snapshot.SubmissionId ||
            snapshot.EmployeeId != employee || c.EmployeeId != employee || snapshot.RequesterAccountId.ToString() != a.RequesterUserId ||
            a.RequestType != AttendanceCarryForwardAdjustment.RequestType || a.Stage != 1 || a.RouteRuleId.HasValue ||
            c.ApprovalRequestId != a.Id || c.PrivateEvidenceReference != a.SnapshotPrivateReference || c.RequestedBy != a.RequesterUserId || c.RequestedAtUtc != a.RequestedAt ||
            c.AttendanceDate != p.SourceBusinessDate || c.Kind != p.Kind || c.RawPunchEventId != p.RawPunchId || c.OriginalSummaryId != p.ExpectedSourceSummaryId ||
            c.RequestedTimeUtc != p.RequestedTimeUtc || c.RequestedEndUtc != p.RequestedEndUtc || c.Reason != p.Reason.Trim() ||
            c.RequestedStatus.HasValue || c.AdjustmentPeriodLockId.HasValue || s.EmployeeId != employee || s.SourceBusinessDate != c.AttendanceDate ||
            s.SourceLockEventId != p.ExpectedSourceLockEventId || s.SourceSummaryId != p.ExpectedSourceSummaryId || s.PriorAdjustmentId != p.ExpectedPriorAdjustmentId ||
            s.TargetMonthStart != p.ExpectedTargetMonthStart || !string.Equals(s.SourceFingerprint, p.ExpectedSourceFingerprint, StringComparison.OrdinalIgnoreCase) ||
            SourceFingerprint(s) != s.SourceFingerprint || !AttendanceScopeEvaluation.IsValid(snapshot.OriginalScopes) ||
            snapshot.OriginalScopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId != employee) || snapshot.ProposedInterpretation is not { Count: > 0 })
            throw Unavailable();
        _ = CarryForwardRules.TotalTicks(snapshot.ProposedInterpretation);
        return (row, snapshot);
    }

    private async Task<(CarryForwardFrozenRows Rows, AttendanceDayResponse Day)> Frozen(Guid employee, DateOnly date, ValidationGraph graph, CancellationToken ct)
    {
        if (graph.Frozen.TryGetValue((employee, date), out var cached)) return cached;
        graph.Enter();
        try
        {
            var rows = await repository.FrozenAsync(employee, date, ct); var item = rows.Lock.Event;
            if (item?.ProtectedEvidenceReference is null || item.ApprovalRequestId.HasValue) throw AttendanceOperation.Conflict("carry_forward_legacy_lock_evidence_unavailable");
            var manifest = await operation.ReadPrivateAsync<MonthLockSnapshot>(item.ProtectedEvidenceReference, MonthLockEvidence.Purpose(rows.Lock.Period.Id, item.Id), ct);
            MonthLockEvidence.Validate(rows.Lock, manifest);
            // A lock must freeze every posted adjustment, not only the latest interpretation.
            var posted = await Month(manifest.MonthStart, manifest.MonthEndExclusive, graph, ct);
            if (!posted.SequenceEqual(manifest.CarryForwards)) throw Unavailable();
            var summary = rows.Summary;
            if (rows.Membership.AttendancePeriodLockEventId != item.Id || rows.Membership.DailyAttendanceSummaryId != summary.Id ||
                rows.Membership.EmployeeId != employee || rows.Membership.AttendanceDate != date || summary.CalculatedAtUtc > item.AtUtc)
                throw Unavailable();
            var inputReference = summary.InputEvidenceReference;
            if (string.IsNullOrWhiteSpace(inputReference)) throw Unavailable();
            var day = await operation.ReadPrivateAsync<AttendanceDayResponse>(inputReference,
                $"attendance:summary:{employee:N}:{date:yyyy-MM-dd}", ct);
            var facts = AttendanceFacts.Calculate(employee, date, rows.Sessions);
            if (summary.EmployeeId != employee || summary.AttendanceDate != date || day.EmployeeId != employee || day.BusinessDate != date ||
                !AttendanceSummaryService.IsSavedRevisionCurrent(day with { LatestSavedRevision = AttendanceSummaryService.Map(summary) }) ||
                day.Policy.Status != "calculated" || summary.PolicyVersionId is null || summary.PolicyVersionId != day.Policy.PolicyVersionId ||
                summary.CalculationVersion != day.Policy.CalculationVersion || summary.Status != day.Policy.DayStatus ||
                summary.PayableMinutes != day.Policy.PayableMinutes || summary.LateMinutes != day.Policy.LateMinutes || summary.OvertimeMinutes != day.Policy.OvertimeMinutes ||
                summary.ShiftVersionId != day.Roster.ShiftVersionId || summary.ScheduledMinutes != (day.Roster.ConfiguredScheduledMinutes ?? 0) ||
                facts.OpenSessions != 0 || facts.InputFingerprint != day.Facts.InputFingerprint || facts.WorkedMinutes != summary.WorkedMinutes ||
                facts.WorkedSeconds != day.Facts.WorkedSeconds || !facts.SessionIds.SequenceEqual(day.Facts.SessionIds) ||
                rows.Links.Count != rows.Sessions.Count || rows.Links.Select(x => x.WorkSessionId).Distinct().Count() != rows.Links.Count ||
                rows.Links.Any(x => x.EmployeeId != employee || x.DailyAttendanceSummaryId != summary.Id) ||
                !rows.Links.Select(x => x.WorkSessionId).Order().SequenceEqual(rows.Sessions.Select(x => x.Id).Order()) ||
                rows.Sessions.Select(x => x.SessionKey).Distinct().Count() != rows.Sessions.Count ||
                rows.Sessions.Any(x => x.EmployeeId != employee || x.AttendanceDate != date || x.EndsAtUtc is null || x.CalculatedAtUtc > item.AtUtc))
                throw Unavailable();
            _ = await OfficeSourceAsync(employee, date, day.Roster, rows.Sessions, ct, day);
            graph.Frozen[(employee, date)] = (rows, day); return (rows, day);
        }
        finally { graph.Exit(); }
    }

    private async Task<CarryForwardInterpretationSnapshot> Adjustment(AttendanceCarryForwardAdjustment row, ValidationGraph graph, CancellationToken ct)
    {
        if (graph.Adjustments.TryGetValue(row.Id, out var cached)) return cached;
        if (!graph.Active.Add(row.Id)) throw Unavailable(); graph.Enter();
        try
        {
            var snapshot = await operation.ReadPrivateAsync<CarryForwardInterpretationSnapshot>(row.ProtectedEvidenceReference, InterpretationPurpose(row.EmployeeId), ct);
            var office = snapshot.Sessions?.FirstOrDefault()?.OfficeSource;
            if ((snapshot.AlternativeEvidence is not null ? snapshot.SchemaVersion != 3 : office is null ? snapshot.SchemaVersion != 1 : snapshot.SchemaVersion != 2) ||
                !ValidLocation(row.EmployeeId, row.SourceBusinessDate, office, snapshot.Sessions) ||
                snapshot.EmployeeId != row.EmployeeId || snapshot.SourceBusinessDate != row.SourceBusinessDate ||
                snapshot.SourceLockEventId != row.SourcePeriodLockEventId || snapshot.SourceSummaryId != row.SourceSummaryId || snapshot.CorrectionId != row.CorrectionId ||
                snapshot.ApprovalDecisionId != row.ApprovalDecisionId || snapshot.Revision != row.Revision || snapshot.PriorAdjustmentId != row.SupersedesAdjustmentId ||
                snapshot.TargetMonthStart != row.TargetMonthStart || snapshot.TargetMonthEndExclusive != row.TargetMonthEndExclusive ||
                snapshot.BeforeWorkedTicks != row.BeforeWorkedTicks || snapshot.AfterWorkedTicks != row.AfterWorkedTicks || snapshot.AppliedAtUtc != row.AppliedAtUtc ||
                snapshot.InputFingerprint != row.InputFingerprint || snapshot.InterpretationFingerprint != row.InterpretationFingerprint ||
                snapshot.OriginalScopes is not { Count: > 0 } || snapshot.Sessions is not { Count: > 0 } ||
                InterpretationFingerprint(snapshot.Sessions) != row.InterpretationFingerprint || CarryForwardRules.TotalTicks(snapshot.Sessions) != row.AfterWorkedTicks ||
                snapshot.Sessions.Min(x => x.StartsAtUtc) != row.FirstStartsAtUtc || snapshot.Sessions.Max(x => x.EndsAtUtc) != row.LastEndsAtUtc)
                throw Unavailable();
            var (source, day) = await Frozen(row.EmployeeId, row.SourceBusinessDate, graph, ct);
            var frozenOffice = await OfficeSourceAsync(row.EmployeeId, row.SourceBusinessDate, day.Roster, source.Sessions, ct, day);
            if (office != frozenOffice) throw Unavailable();
            if (!snapshot.FrozenSessionIds.Order().SequenceEqual(source.Sessions.Select(x => x.Id).Order()) ||
                !snapshot.FrozenSessionKeys.Order().SequenceEqual(source.Sessions.Select(x => x.SessionKey).Order())) throw Unavailable();
            var (request, proposal) = await RequestAsync(row.EmployeeId, row.CorrectionId, ct);
            if (System.Text.Json.JsonSerializer.Serialize(snapshot.AlternativeEvidence) != System.Text.Json.JsonSerializer.Serialize(proposal.AlternativeEvidence)) throw Unavailable();
            var decision = await repository.DecisionAsync(row.ApprovalDecisionId, ct) ?? throw Unavailable();
            if (proposal.AlternativeEvidence is not null && decision.ActorRoleCode != "ADMIN") throw Unavailable();
            AttendanceCarryForwardAdjustment? prior = null;
            if (row.SupersedesAdjustmentId.HasValue)
            {
                prior = await repository.AdjustmentAsync(row.SupersedesAdjustmentId.Value, ct) ?? throw Unavailable();
                _ = await Adjustment(prior, graph, ct);
            }
            var expected = proposal.ProposedInterpretation.Select(x => x.IsManual && x.OriginCorrectionId is null ? x with { OriginCorrectionId = row.CorrectionId } : x).ToArray();
            if (!expected.SequenceEqual(snapshot.Sessions) || Fingerprint(proposal.OriginalScopes) != Fingerprint(snapshot.OriginalScopes) ||
                proposal.Source.SourceFingerprint != row.InputFingerprint || proposal.Source.PriorAdjustmentId != row.SupersedesAdjustmentId ||
                proposal.Source.TargetMonthStart != row.TargetMonthStart || proposal.Source.CurrentWorkedTicks != row.BeforeWorkedTicks) throw Unavailable();
            // Reconstruct all immutable domain invariants from exact retained source and decision.
            AttendanceCarryForwardAdjustment rebuilt;
            try { rebuilt = new(request.Correction, request.Approval, decision, source.Lock.Period, source.Lock.Event!, source.Membership,
                source.Summary, source.Sessions, prior, row.TargetMonthStart, snapshot.Sessions.Select(x => new AttendanceCarryForwardWindow(x.SessionKey, x.StartsAtUtc, x.EndsAtUtc)).ToArray(),
                row.InputFingerprint, row.InterpretationFingerprint, row.ProtectedEvidenceReference, row.AppliedAtUtc); }
            catch (ArgumentException) { throw Unavailable(); }
            if (rebuilt.Revision != row.Revision || rebuilt.BeforeWorkedTicks != row.BeforeWorkedTicks || rebuilt.AfterWorkedTicks != row.AfterWorkedTicks || rebuilt.DeltaWorkedTicks != row.DeltaWorkedTicks)
                throw Unavailable();
            graph.Adjustments[row.Id] = snapshot; return snapshot;
        }
        finally { graph.Active.Remove(row.Id); graph.Exit(); }
    }
    private async Task<IReadOnlyList<MonthLockCarryForwardEntry>> Month(DateOnly start, DateOnly end, ValidationGraph graph, CancellationToken ct)
    {
        if (graph.Months.TryGetValue((start, end), out var cached)) return cached;
        var rows = await repository.MonthAsync(null, start, end, ct);
        if (rows.Count > 4096) throw AttendanceOperation.Conflict("carry_forward_month_limit");
        var result = new List<MonthLockCarryForwardEntry>();
        foreach (var row in rows)
        {
            _ = await Adjustment(row, graph, ct);
            result.Add(new(row.Id, row.EmployeeId, row.SourceBusinessDate, row.Revision, row.TargetMonthStart, row.TargetMonthEndExclusive,
                row.DeltaWorkedTicks, CarryForwardRules.MinuteDelta(row.BeforeWorkedTicks, row.AfterWorkedTicks), row.InputFingerprint, row.InterpretationFingerprint));
        }
        var validated = result.OrderBy(x => x.AdjustmentId).ToArray();
        graph.Months[(start, end)] = validated;
        return validated;
    }
    private static Exception Unavailable() => AttendanceOperation.Conflict("carry_forward_evidence_unavailable");
    private static bool ValidAlternative(CorrectionAlternativeEvidence? evidence, SubmitCarryForwardRequest proposal, Guid employee) =>
        proposal.AlternativeKind.HasValue ? proposal.Kind == Domain.Attendance.Corrections.AttendanceCorrectionKind.ManualSession &&
            CorrectionAlternativePolicyService.Valid(evidence, employee) && evidence!.Policy.Kind == proposal.AlternativeKind &&
            evidence.Policy.Id == proposal.ExpectedAlternativePolicyId && evidence.Documents.Select(x => x.Id).Order().SequenceEqual((proposal.AlternativeEvidenceDocumentIds ?? []).Order())
        : evidence is null && proposal.ExpectedAlternativePolicyId is null && proposal.AlternativeEvidenceDocumentIds is null;
    internal async Task<OfficeAttendanceSource?> OfficeSourceAsync(Guid employee, DateOnly date,
        EffectiveRosterResponse retainedRoster, IReadOnlyList<WorkSession> sessions, CancellationToken ct,
        AttendanceDayResponse? retainedDay = null)
    {
        if (retainedRoster.BranchId is null)
        {
            if (retainedRoster.Explanation?.Source == "office" || sessions.Any(x => x.SiteId is null || x.BranchId.HasValue ||
                x.OfficeRosterAssignmentId.HasValue || x.OfficeGeofenceVersionId.HasValue)) throw Unavailable();
            return null;
        }
        if (sessions.Count > 4096) throw Unavailable();
        Guid boundary;
        OfficeAttendanceSource? calculatedOffice = null;
        if (sessions.Count == 0)
        {
            // Empty means an exact, saved calculated source, never missing data. Rebuild only to prove
            // byte-identical frozen provenance; a newer policy/boundary cannot substitute for it.
            if (retainedDay is null || retainedDay.Policy.Status != "calculated" || retainedDay.Policy.InputProvenance is not { Length: 64 } hash ||
                retainedDay.Facts.SessionIds.Count != 0) throw AttendanceOperation.Conflict("carry_forward_office_calculation_provenance_required");
            var result = await calculations.ReadAsync(employee, date, retainedDay.Facts, retainedRoster, ct);
            if (result.State != "complete" || result.Snapshot is not { SchemaVersion: 1 or 2, OfficeSource: not null } calculation ||
                result.Fingerprint != hash || Fingerprint(calculation) != hash || calculation.EmployeeId != employee || calculation.BusinessDate != date ||
                calculation.FactualFingerprint != retainedDay.Facts.InputFingerprint || calculation.Sessions.Count != 0 ||
                calculation.Policy.State != "effective" || calculation.Policy.PolicyVersionId != retainedDay.Policy.PolicyVersionId ||
                calculation.CalculationVersion != retainedDay.Policy.CalculationVersion)
                throw AttendanceOperation.Conflict("carry_forward_office_calculation_source_changed");
            calculatedOffice = calculation.OfficeSource; boundary = calculatedOffice.OfficeGeofenceVersionId;
        }
        else if (sessions[0].OfficeGeofenceVersionId is Guid pinned) boundary = pinned;
        else throw Unavailable();
        var retained = await CorrectionOfficeEvidence.ReadAsync(employee, date, retainedRoster, offices, boundary, ct);
        if (retained is null || calculatedOffice is not null && calculatedOffice != retained ||
            sessions.Any(x => !CorrectionOfficeEvidence.Matches(x, retained))) throw Unavailable();
        var currentRoster = await rosters.ResolveAsync(employee, date, ct);
        var current = await CorrectionOfficeEvidence.ReadAsync(employee, date, currentRoster, offices, boundary, ct);
        if (current != retained || currentRoster.StartsAt != retainedRoster.StartsAt || currentRoster.EndsAt != retainedRoster.EndsAt ||
            currentRoster.Status != retainedRoster.Status) throw AttendanceOperation.Conflict("carry_forward_office_source_changed");
        return retained;
    }

    private static bool ValidLocation(Guid employee, DateOnly date, OfficeAttendanceSource? office, IReadOnlyList<CarryForwardSession>? sessions) =>
        sessions is { Count: <= 4096 } && (office is null
            ? sessions.All(x => x.OfficeSource is null && x.SiteId.HasValue && x.SiteId != Guid.Empty)
            : office.EmployeeId == employee && office.BusinessDate == date && sessions.All(x => x.SiteId is null && x.OfficeSource == office));
    private sealed class ValidationGraph
    {
        public Dictionary<(Guid, DateOnly), (CarryForwardFrozenRows, AttendanceDayResponse)> Frozen { get; } = [];
        public Dictionary<Guid, CarryForwardInterpretationSnapshot> Adjustments { get; } = [];
        public Dictionary<(DateOnly, DateOnly), IReadOnlyList<MonthLockCarryForwardEntry>> Months { get; } = [];
        public HashSet<Guid> Active { get; } = [];
        private int depth; private int nodes;
        public void Enter() { if (++depth > 128 || ++nodes > 8192) throw AttendanceOperation.Conflict("carry_forward_evidence_limit"); }
        public void Exit() => depth--;
    }
}

public sealed class CarryForwardLockReader(CarryForwardEvidence evidence) : IMonthLockCarryForwardReader
{
    public Task<IReadOnlyList<MonthLockCarryForwardEntry>> ReadForLockAsync(DateOnly start, DateOnly end, CancellationToken ct) => evidence.MonthAsync(start, end, ct);
}

public sealed class CarryForwardDutyOverlap(ICarryForwardRepository repository, CarryForwardEvidence evidence) : ICarryForwardDutyOverlap
{
    public async Task<bool> HasOverlapAsync(Guid employeeId, IReadOnlyCollection<Guid> excludedSessionKeys, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || end <= start) throw AttendanceOperation.Invalid();
        var sessions = await repository.OverlappingSessionsAsync(employeeId, excludedSessionKeys, start, end, ct);
        var adjustments = await repository.OverlapAdjustmentsAsync(employeeId, sessions.Select(x => x.AttendanceDate).Distinct().ToArray(), start, end, ct);
        if (sessions.Count > 4096 || adjustments.Count > 4096) throw AttendanceOperation.Conflict("carry_forward_overlap_limit");
        var replaced = new HashSet<Guid>();
        foreach (var row in adjustments)
        {
            var retained = await evidence.AdjustmentAsync(row, ct);
            replaced.UnionWith(retained.FrozenSessionKeys);
            if (retained.Sessions.Any(x => !excludedSessionKeys.Contains(x.SessionKey) && x.StartsAtUtc < end && x.EndsAtUtc > start)) return true;
        }
        return sessions.Any(x => !replaced.Contains(x.SessionKey));
    }
}
