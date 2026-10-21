using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.Attendance.WorkTreatments;

public static class WorkTreatmentRules
{
    public static AdvancedAttendanceFamily Family(WorkTreatmentKind kind) => kind switch {
        WorkTreatmentKind.EarlyInstruction or WorkTreatmentKind.EarlyCredit => AdvancedAttendanceFamily.EarlyCredit,
        WorkTreatmentKind.StudyInstruction or WorkTreatmentKind.MakeUp => AdvancedAttendanceFamily.StudyMakeUp,
        WorkTreatmentKind.CompanyTrip or WorkTreatmentKind.PersonalHomeTravel => AdvancedAttendanceFamily.CompanyTravel,
        WorkTreatmentKind.ContinuousDuty or WorkTreatmentKind.AllegedFakePunch => AdvancedAttendanceFamily.ContinuousDuty,
        WorkTreatmentKind.AnnualLate => AdvancedAttendanceFamily.Kpi,
        _ => throw AttendanceOperation.Invalid("invalid_work_treatment_kind")
    };
    public static void Dates(DateOnly from, DateOnly to) {
        if (from == default || to < from || to == DateOnly.MaxValue || to.DayNumber - from.DayNumber > 30)
            throw AttendanceOperation.Invalid("work_treatment_range_limit");
    }
    public static void Window(DateTimeOffset start, DateTimeOffset end) {
        if (start.Offset != TimeSpan.Zero || end.Offset != TimeSpan.Zero || end <= start || end - start > TimeSpan.FromDays(31))
            throw AttendanceOperation.Invalid("invalid_work_treatment_interval");
    }
    public static void Validate(WorkTreatmentRequest p) {
        Dates(p.From, p.To); Window(p.StartUtc, p.EndUtc); _ = Family(p.Kind);
        if (!HashValid(p.ExpectedSourceFingerprint) || string.IsNullOrWhiteSpace(p.SourceTicket) || p.SourceTicket.Length > 16000 ||
            p.Intervals is null or { Count: > 256 } || p.CycleEvents is null or { Count: > 64 } || p.DocumentIds is null or { Count: > 32 } ||
            p.DocumentIds.Any(x => x == Guid.Empty) || p.DocumentIds.Distinct().Count() != p.DocumentIds.Count ||
            p.InstructionRequestId == Guid.Empty || p.ResubmissionOf == Guid.Empty || p.AnnualLateSourceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(p.Reason) || p.Reason.Length > 1000 || p.Reason.Any(char.IsControl))
            throw AttendanceOperation.Invalid("invalid_work_treatment_proposal");
        foreach (var r in p.Intervals) {
            Window(r.StartUtc, r.EndUtc);
            if (r.SessionId == Guid.Empty || r.BusinessDate < p.From || r.BusinessDate > p.To || r.StartUtc < p.StartUtc || r.EndUtc > p.EndUtc)
                throw AttendanceOperation.Invalid("invalid_work_treatment_session_range");
        }
        var sorted = p.Intervals.OrderBy(x => x.StartUtc).ToArray();
        if (sorted.Zip(sorted.Skip(1)).Any(x => x.First.EndUtc > x.Second.StartUtc))
            throw AttendanceOperation.Invalid("overlapping_work_treatment_intervals");
        var instruction = p.Kind is WorkTreatmentKind.EarlyInstruction or WorkTreatmentKind.StudyInstruction;
        var allocating = p.Kind is WorkTreatmentKind.EarlyCredit or WorkTreatmentKind.MakeUp or WorkTreatmentKind.CompanyTrip or WorkTreatmentKind.ContinuousDuty;
        if (instruction != (p.Instruction is not null) || instruction && (p.From != p.To || p.Intervals.Count != 0) ||
            allocating != (p.Intervals.Count != 0) ||
            (p.Kind is WorkTreatmentKind.EarlyCredit or WorkTreatmentKind.MakeUp) != p.InstructionRequestId.HasValue ||
            p.Kind != WorkTreatmentKind.ContinuousDuty && p.CycleEvents.Count != 0 ||
            p.Kind != WorkTreatmentKind.AnnualLate && p.AnnualLateSourceId.HasValue)
            throw AttendanceOperation.Invalid("work_treatment_fields_do_not_match_kind");
        if (p.Instruction is { } i && (i.WrittenDocumentId == Guid.Empty || !p.DocumentIds.Contains(i.WrittenDocumentId) ||
            string.IsNullOrWhiteSpace(i.NamedInstruction) || i.NamedInstruction.Length > 500 || i.NamedInstruction.Any(char.IsControl) ||
            (p.Kind == WorkTreatmentKind.StudyInstruction ? i.RequiredMakeUpTicks is null or <= 0 || i.FulfillmentDeadline is null :
                i.RequiredMakeUpTicks is not null || i.FulfillmentDeadline is not null)))
            throw AttendanceOperation.Invalid("explicit_named_written_instruction_required");
        foreach (var e in p.CycleEvents) {
            Window(e.StartUtc, e.EndUtc);
            if (!Enum.IsDefined(e.Kind) || !p.DocumentIds.Contains(e.DocumentId) || e.StartUtc < p.StartUtc || e.EndUtc > p.EndUtc)
                throw AttendanceOperation.Invalid("verified_cycle_event_required");
        }
        var events = p.CycleEvents.OrderBy(x => x.StartUtc).ToArray();
        if (events.Zip(events.Skip(1)).Any(x => x.First.EndUtc > x.Second.StartUtc))
            throw AttendanceOperation.Invalid("overlapping_cycle_events");
    }
    public static AdvancedAttendanceRules Policy(WorkTreatmentSource source) {
        if (source.EmployeeId == Guid.Empty || source.Days.Count != source.To.DayNumber - source.From.DayNumber + 1 ||
            source.Days.Select(x => x.BusinessDate).Distinct().Count() != source.Days.Count ||
            !AttendanceScopeEvaluation.IsValid(source.Scopes) || source.Scopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId != source.EmployeeId) ||
            Fingerprint(source) != source.Fingerprint) throw AttendanceOperation.Conflict("work_treatment_source_invalid");
        foreach (var day in source.Days) {
            var p = day.Policy;
            if (day.Employment is null || p.State != "configured" || p.EmployeeId != source.EmployeeId || p.BusinessDate != day.BusinessDate ||
                p.Family != Family(source.Kind) || p.PolicyVersionId is null || p.PolicyVersionId == Guid.Empty ||
                p.AssignmentIds.Count == 0 || !HashValid(p.Fingerprint) || !AdvancedAttendanceRulesCodec.Validate(p.Rules).IsValid ||
                p.Rules!.Family != Family(source.Kind) || !p.Rules.ApplicableStaffCategoryCodes!.Contains(day.Employment.CategoryCode, StringComparer.Ordinal))
                throw AttendanceOperation.Conflict("work_treatment_policy_unavailable");
        }
        // A bounded proposal cannot cross a configuration revision; split it explicitly instead.
        if (source.Days.Select(x => JsonSerializer.Serialize(x.Policy.Rules)).Distinct().Count() != 1 ||
            source.Days.Select(x => x.Policy.PolicyVersionId).Distinct().Count() != 1)
            throw AttendanceOperation.Conflict("work_treatment_policy_boundary");
        return source.Days[0].Policy.Rules!;
    }
    public static WorkTreatmentResult Evaluate(Guid requestId, WorkTreatmentRequest p, WorkTreatmentSource source,
        WorkTreatmentSubmission? instruction, IReadOnlyList<WorkCreditRange> alreadyClaimed, DateTimeOffset now) {
        Validate(p); var rules = Policy(source);
        if (p.Kind != source.Kind || p.From != source.From || p.To != source.To || p.ExpectedSourceFingerprint != source.Fingerprint)
            throw AttendanceOperation.Conflict("work_treatment_source_changed");
        if (p.Kind is WorkTreatmentKind.EarlyInstruction or WorkTreatmentKind.StudyInstruction) {
            var roster = source.Days.Single().Roster;
            if (roster.StartsAt is null || roster.EndsAt is null || roster.ShiftVersionId is null)
                throw AttendanceOperation.Conflict("work_treatment_roster_unavailable");
            if (p.Kind == WorkTreatmentKind.EarlyInstruction) {
                if (p.EndUtc > roster.StartsAt || p.StartUtc < roster.StartsAt.Value.AddTicks(-Seconds(rules.EarlyCredit!.MaximumPreStartSeconds!.Value)))
                    throw AttendanceOperation.Invalid("early_instruction_outside_policy_window");
            } else {
                if (p.StartUtc < roster.StartsAt || p.EndUtc > roster.EndsAt ||
                    p.Instruction!.RequiredMakeUpTicks > Seconds(rules.StudyMakeUp!.MaximumMakeUpSeconds!.Value) ||
                    p.Instruction.FulfillmentDeadline <= p.From || p.Instruction.FulfillmentDeadline > p.From.AddDays(rules.StudyMakeUp.FulfillmentDeadlineDays!.Value))
                    throw AttendanceOperation.Invalid("study_instruction_outside_policy_window");
            }
            return new("reviewed_instruction", 0, p.Kind == WorkTreatmentKind.StudyInstruction,
                rules.StudyMakeUp?.KpiTreatment, [], [], "Dated named instruction only; actual fulfillment requires a separate reviewed interval allocation.");
        }
        if (p.Kind == WorkTreatmentKind.PersonalHomeTravel)
            return new("leave_or_absence", 0, false, null, [], [], "Personal home travel remains subject to existing leave/absence review; no normal-work proposal is created.");
        if (p.Kind is WorkTreatmentKind.AllegedFakePunch or WorkTreatmentKind.AnnualLate)
            return new("reviewed_case_only", 0, false, null, [], [], "Reviewed factual outcome only; no employment, access, blacklist, KPI score or payroll effect.");
        if (p.EndUtc > now) throw AttendanceOperation.Conflict("work_treatment_actual_interval_not_finished");
        var ranges = new List<WorkCreditRange>();
        foreach (var chosen in p.Intervals) {
            var day = source.Days.Single(x => x.BusinessDate == chosen.BusinessDate);
            var s = day.Sessions.SingleOrDefault(x => x.Id == chosen.SessionId);
            if (s is null || !s.Verified || s.EndUtc is null || chosen.StartUtc < s.StartUtc || chosen.EndUtc > s.EndUtc ||
                s.BusinessDate != chosen.BusinessDate || s.SiteId != day.Employment?.SiteId || s.BranchId != day.Employment?.BranchId ||
                s.ShiftVersionId != day.Roster.ShiftVersionId)
                throw AttendanceOperation.Conflict("verified_actual_work_session_required");
            if (p.Kind == WorkTreatmentKind.EarlyCredit) {
                RequireInstruction(instruction, source.EmployeeId, WorkTreatmentKind.EarlyInstruction);
                if (chosen.StartUtc < instruction!.Proposal.StartUtc || chosen.EndUtc > instruction.Proposal.EndUtc ||
                    day.Roster.StartsAt is null || chosen.EndUtc > day.Roster.StartsAt ||
                    chosen.StartUtc < day.Roster.StartsAt.Value.AddTicks(-Seconds(rules.EarlyCredit!.MaximumPreStartSeconds!.Value)))
                    throw AttendanceOperation.Conflict("early_credit_outside_written_instruction");
            }
            if (p.Kind == WorkTreatmentKind.MakeUp) {
                RequireInstruction(instruction, source.EmployeeId, WorkTreatmentKind.StudyInstruction);
                if (chosen.BusinessDate < instruction!.Proposal.From || chosen.BusinessDate > instruction.Proposal.Instruction!.FulfillmentDeadline ||
                    Overlap(chosen.StartUtc, chosen.EndUtc, instruction.Proposal.StartUtc, instruction.Proposal.EndUtc))
                    throw AttendanceOperation.Conflict("make_up_outside_obligation_window");
            }
            if (instruction is not null && source.Days.Any(x => x.Policy.PolicyVersionId != instruction.Source.Days[0].Policy.PolicyVersionId))
                throw AttendanceOperation.Conflict("work_instruction_policy_boundary");
            var role = p.Kind switch { WorkTreatmentKind.EarlyCredit => WorkCreditRole.EarlyCredit,
                WorkTreatmentKind.MakeUp => WorkCreditRole.MakeUp, _ => WorkCreditRole.NormalWorkTravel };
            ranges.Add(new(source.EmployeeId, requestId, role, s.Key, s.Id, s.BusinessDate, chosen.StartUtc, chosen.EndUtc));
        }
        var ticks = ranges.Sum(x => x.Ticks);
        if (p.Kind == WorkTreatmentKind.MakeUp && ticks > Seconds(rules.StudyMakeUp!.MaximumMakeUpSeconds!.Value))
            throw AttendanceOperation.Conflict("make_up_policy_cap_exceeded");
        if (p.Kind == WorkTreatmentKind.ContinuousDuty) {
            if (p.Intervals.Any(x => { var s = source.Days.SelectMany(d => d.Sessions).Single(z => z.Id == x.SessionId); return x.StartUtc != s.StartUtc || x.EndUtc != s.EndUtc; }) ||
                source.Days.SelectMany(x => x.Sessions).Any(x => x.StartUtc < p.EndUtc && (x.EndUtc is null || x.EndUtc > p.StartUtc) && !p.Intervals.Any(r => r.SessionId == x.Id)))
                throw AttendanceOperation.Conflict("continuous_complete_actual_sessions_required");
            var cycles = Cycles(p, source, rules.ContinuousDuty!);
            return new("reviewed_continuous_proposal", cycles.Sum(x => x.ActualTicks), false, null, [], cycles,
                "Exact actual cycles and approved example boundaries retained for calculation integration; no new payable source or automatic reset is applied.");
        }
        Exclusive(ranges, alreadyClaimed);
        return new("reviewed_work_proposal", ticks, p.Kind == WorkTreatmentKind.MakeUp, rules.StudyMakeUp?.KpiTreatment,
            ranges, [], "Exclusive factual reservation only; normal pay, OT, TDA, KPI score, finance and recovery require their verified consuming integration.");
    }
    public static void Exclusive(IReadOnlyList<WorkCreditRange> proposed, IReadOnlyList<WorkCreditRange> claimed) {
        foreach (var x in proposed) {
            Window(x.StartUtc, x.EndUtc);
            if (x.EmployeeId == Guid.Empty || x.SourceId == Guid.Empty || x.SessionKey == Guid.Empty || x.SessionId == Guid.Empty || !Enum.IsDefined(x.Role))
                throw AttendanceOperation.Invalid("invalid_work_credit_range");
            if (claimed.Any(y => y.EmployeeId == x.EmployeeId && Overlap(x.StartUtc, x.EndUtc, y.StartUtc, y.EndUtc)))
                throw AttendanceOperation.Conflict("work_interval_already_claimed");
        }
        for (var i = 0; i < proposed.Count; i++) for (var j = i + 1; j < proposed.Count; j++)
            if (proposed[i].EmployeeId == proposed[j].EmployeeId && Overlap(proposed[i].StartUtc, proposed[i].EndUtc, proposed[j].StartUtc, proposed[j].EndUtc))
                throw AttendanceOperation.Conflict("work_interval_reused_in_proposal");
    }
    private static void RequireInstruction(WorkTreatmentSubmission? instruction, Guid employeeId, WorkTreatmentKind kind) {
        if (instruction is null || instruction.EmployeeId != employeeId || instruction.Proposal.Kind != kind || instruction.Proposal.Instruction is null)
            throw AttendanceOperation.Conflict("approved_named_instruction_required");
    }
    public static bool Overlap(DateTimeOffset a, DateTimeOffset b, DateTimeOffset c, DateTimeOffset d) => a < d && c < b;
    public static long Seconds(decimal seconds) => ExactTicks(seconds * TimeSpan.TicksPerSecond);
    public static long Hours(decimal hours) => ExactTicks(hours * TimeSpan.TicksPerHour);
    private static long ExactTicks(decimal ticks) => ticks >= 0 && ticks <= long.MaxValue && decimal.Truncate(ticks) == ticks
        ? (long)ticks : throw AttendanceOperation.Conflict("policy_time_precision_unrepresentable");
    public static bool HashValid(string? s) => s is { Length: 64 } && s.All(Uri.IsHexDigit);
    public static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    public static string Fingerprint(WorkTreatmentSource source) => Hash(source with { Fingerprint = "" });
    public static string InstructionContextHash(WorkTreatmentSource source) => Hash(new {
        source.EmployeeId, source.Kind, source.From, source.To,
        Days = source.Days.Select(x => new { x.BusinessDate, x.Employment, x.Roster, x.Policy })
    });

    private static IReadOnlyList<WorkContinuousCycle> Cycles(WorkTreatmentRequest p, WorkTreatmentSource source, ContinuousDutyRules rules) {
        var segments = new List<(DateTimeOffset Start, DateTimeOffset End, DateOnly Date, bool Reset)>();
        foreach (var r in p.Intervals.OrderBy(x => x.StartUtc)) {
            var s = source.Days.SelectMany(x => x.Sessions).Single(x => x.Id == r.SessionId);
            var cursor = r.StartUtc;
            foreach (var e in p.CycleEvents.Where(x => Overlap(r.StartUtc, r.EndUtc, x.StartUtc, x.EndUtc)).OrderBy(x => x.StartUtc)) {
                var start = e.StartUtc > r.StartUtc ? e.StartUtc : r.StartUtc;
                if (start > cursor) segments.Add((cursor, start, r.BusinessDate, e.Kind == WorkCycleEventKind.HomeVisit ? rules.HomeVisitEndsCycle == true : rules.BreaksEndCycle == true));
                cursor = e.EndUtc < r.EndUtc ? e.EndUtc : r.EndUtc;
            }
            if (cursor < r.EndUtc) segments.Add((cursor, r.EndUtc, r.BusinessDate, s.EndsContinuousDuty));
        }
        var result = new List<WorkContinuousCycle>();
        DateTimeOffset? cycleStart = null; DateTimeOffset cycleEnd = default; long actual = 0, extra = 0;
        void Close() {
            if (cycleStart is null) return;
            result.Add(new(cycleStart.Value, cycleEnd, actual, extra, actual >= Hours(rules.ResetAfterActualDutyHours!.Value),
                extra >= Hours(rules.ExtraHoursBoundary!.Value), Hours(rules.NextDayRequiredHours!.Value)));
            cycleStart = null; actual = 0; extra = 0;
        }
        foreach (var s in segments.OrderBy(x => x.Start)) {
            if (cycleStart is not null && s.Start > cycleEnd && (rules.BreaksEndCycle == true || p.CycleEvents.Any(e => e.Kind == WorkCycleEventKind.HomeVisit && rules.HomeVisitEndsCycle == true && e.StartUtc >= cycleEnd && e.EndUtc <= s.Start))) Close();
            cycleStart ??= s.Start; cycleEnd = s.End;
            var duration = s.End.UtcTicks - s.Start.UtcTicks; actual = checked(actual + duration);
            var roster = source.Days.Single(x => x.BusinessDate == s.Date).Roster;
            if (roster.StartsAt is null || roster.EndsAt is null) throw AttendanceOperation.Conflict("continuous_roster_required");
            var withinStart = s.Start > roster.StartsAt ? s.Start : roster.StartsAt.Value;
            var withinEnd = s.End < roster.EndsAt ? s.End : roster.EndsAt.Value;
            extra = checked(extra + duration - Math.Max(0, withinEnd.UtcTicks - withinStart.UtcTicks));
            if (s.Reset) Close();
        }
        Close(); return result;
    }
}
