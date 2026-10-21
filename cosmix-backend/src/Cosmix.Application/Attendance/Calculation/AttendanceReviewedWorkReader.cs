using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.Kpi;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.Attendance.Calculation;

public sealed record AttendanceReviewedWorkSource(ReviewedAttendanceWork? Time,
    IReadOnlyList<WorkTreatmentCreditEvidence> Credits, IReadOnlyList<WorkStudyEvidence> Study,
    bool FinancialUnverified, string Fingerprint)
{
    public IReadOnlyList<WorkContinuousEvidence> Continuous { get; init; } = [];
}
public interface IAttendanceReviewedWorkReader {
    Task<AttendanceReviewedWorkSource?> ReadAsync(Guid employee, DateOnly date, EffectiveRosterResponse roster,
        IReadOnlyList<OvertimeSessionSource> sessions, IReadOnlyList<AttendanceTimeInterval> overtime, CancellationToken ct);
}

/// <summary>Consumes actual approved records. Null means the bounded source query found no applicable treatment.</summary>
public sealed class AttendanceReviewedWorkReader(IWorkTreatmentCreditReader reader) : IAttendanceReviewedWorkReader
{
    public async Task<AttendanceReviewedWorkSource?> ReadAsync(Guid employee, DateOnly date, EffectiveRosterResponse roster,
        IReadOnlyList<OvertimeSessionSource> sessions, IReadOnlyList<AttendanceTimeInterval> overtime, CancellationToken ct)
    {
        var credits = await reader.ReadAsync(employee, date, date, ct);
        var studyRows = await reader.StudyAsync(employee, date, date, ct);
        var continuous = await reader.ContinuousAsync(employee, date, date, ct);
        if (credits.Count == 0 && studyRows.Count == 0 && continuous.Count == 0) return null;
        if (roster.StartsAt is null || roster.EndsAt is null || credits.Count + studyRows.Count > 512)
            throw AttendanceOperation.Conflict("reviewed_work_schedule_required");
        var start = roster.StartsAt.Value.ToUniversalTime(); var end = roster.EndsAt.Value.ToUniversalTime();
        foreach (var credit in credits)
        {
            var r = credit.Range;
            if (credit.RequestId == Guid.Empty || credit.ApprovalId == Guid.Empty || credit.DecisionId == Guid.Empty ||
                credit.PolicyVersionId == Guid.Empty || credit.SourceFingerprint.Length != 64 || credit.PolicyFingerprint.Length != 64 ||
                !AttendanceScopeEvaluation.IsValid(credit.OriginalScopes) ||
                r.EmployeeId != employee || r.BusinessDate != date || r.SourceId != credit.RequestId ||
                !sessions.Any(s => s.Id == r.SessionId && s.SessionKey == r.SessionKey && s.StartsAtUtc <= r.StartUtc && s.EndsAtUtc >= r.EndUtc) ||
                overtime.Any(x => x.StartsAtUtc < r.EndUtc && r.StartUtc < x.EndsAtUtc))
                throw AttendanceOperation.Conflict("reviewed_work_source_conflict");
            if (r.Role is WorkCreditRole.EarlyCredit or WorkCreditRole.MakeUp)
            {
                var proof = credit.InstructionProof;
                if (proof is null || proof.EmployeeId != employee || proof.RequestId == Guid.Empty || proof.ApprovalId == Guid.Empty ||
                    proof.DecisionId == Guid.Empty || proof.PolicyVersionId == Guid.Empty || proof.WrittenDocumentId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(proof.NamedWrittenInstruction) || proof.WrittenDocumentSha256.Length != 64 ||
                    credit.ObligationRequestId != proof.RequestId ||
                    proof.Kind != (r.Role == WorkCreditRole.EarlyCredit ? WorkTreatmentKind.EarlyInstruction : WorkTreatmentKind.StudyInstruction) ||
                    r.Role == WorkCreditRole.EarlyCredit && (r.StartUtc < proof.StartUtc || r.EndUtc > proof.EndUtc))
                    throw AttendanceOperation.Conflict("reviewed_work_instruction_proof_required");
            }
        }
        var early = credits.Where(x => x.Range.Role == WorkCreditRole.EarlyCredit).Select(x => new AttendanceTimeInterval(x.Range.StartUtc, x.Range.EndUtc)).ToArray();
        var reserved = credits.Where(x => x.Range.Role == WorkCreditRole.MakeUp).Select(x => new AttendanceTimeInterval(x.Range.StartUtc, x.Range.EndUtc)).ToArray();
        if (credits.Any(x => x.Range.Role is not (WorkCreditRole.EarlyCredit or WorkCreditRole.MakeUp or WorkCreditRole.NormalWorkTravel)))
            throw AttendanceOperation.Conflict("reviewed_work_role_interpretation_required");
        var studies = studyRows.Where(x => x.ExcusedStartUtc < end && start < x.ExcusedEndUtc).ToArray();
        if (studies.Any(x => x.EmployeeId != employee || x.RequestId == Guid.Empty || x.ApprovalId == Guid.Empty || x.DecisionId == Guid.Empty ||
            x.PolicyVersionId == Guid.Empty || x.WrittenDocumentId == Guid.Empty || x.Rules.TdaExcluded != true ||
            x.InstructionProof is null || x.InstructionProof.RequestId != x.RequestId || x.InstructionProof.EmployeeId != employee ||
            x.Rules.ExclusiveWorkAllocation != true || x.Rules.HourlyTreatment is null || x.Rules.KpiTreatment is null))
            throw AttendanceOperation.Conflict("reviewed_study_source_conflict");
        if (studies.Select(x => JsonSerializer.Serialize(x.Rules)).Distinct(StringComparer.Ordinal).Count() > 1)
            throw AttendanceOperation.Conflict("reviewed_study_policy_conflict");
        var excused = studies.Select(x => new AttendanceTimeInterval(x.ExcusedStartUtc > start ? x.ExcusedStartUtc : start,
            x.ExcusedEndUtc < end ? x.ExcusedEndUtc : end)).OrderBy(x => x.StartsAtUtc).ToArray();
        var targets = continuous.SelectMany(c => c.TargetDays.Where(x => x.TargetBusinessDate == date && x.ResetReached).Select(x => (Evidence: c, Target: x))).ToArray();
        if (targets.Any(x => x.Evidence.EmployeeId != employee || x.Evidence.ApprovalId == Guid.Empty || x.Evidence.DecisionId == Guid.Empty ||
                x.Evidence.PolicyVersionId == Guid.Empty || x.Target.NextDayRequiredTicks <= 0 ||
                x.Target.NextDayRequiredTicks != x.Evidence.Rules.NextDayRequiredHours * TimeSpan.TicksPerHour) ||
            targets.Select(x => x.Target.NextDayRequiredTicks).Distinct().Count() > 1)
            throw AttendanceOperation.Conflict("continuous_required_duty_source_conflict");
        long? required = targets.Length == 0 ? null : targets[0].Target.NextDayRequiredTicks;
        var time = early.Length + reserved.Length + excused.Length == 0 && required is null ? null : new ReviewedAttendanceWork(early.OrderBy(x => x.StartsAtUtc).ToArray(), reserved.OrderBy(x => x.StartsAtUtc).ToArray(),
            excused, studies.Length == 0 ? null : studies[0].Rules.HourlyTreatment,
            studies.Length == 0 ? null : studies[0].Rules.KpiTreatment == StudyKpiTreatment.ExcludeApprovedExcusedInterval) { RequiredDutyTicks = required };
        return new(time, credits, studies, studies.Length != 0 || reserved.Length != 0 || continuous.Count != 0,
            AttendanceKpiCalculation.Hash(new { employee, date, credits, studies, continuous, time })) { Continuous = continuous };
    }
}
