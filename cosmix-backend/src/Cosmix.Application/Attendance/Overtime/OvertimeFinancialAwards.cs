using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.Overtime;

public static class OvertimeFinancialAwardRules
{
    public static IReadOnlyList<OvertimeEligibleInterval> ApprovedIntervals(OvertimeSourceResponse source,
        SubmitOvertimeRequest proposal) => source.DurationPolicy.EligibleIntervals
        .Select(x => new OvertimeEligibleInterval(x.StartsAtUtc > proposal.StartsAtUtc ? x.StartsAtUtc : proposal.StartsAtUtc,
            x.EndsAtUtc < proposal.EndsAtUtc ? x.EndsAtUtc : proposal.EndsAtUtc))
        .Where(x => x.EndsAtUtc > x.StartsAtUtc).OrderBy(x => x.StartsAtUtc).ToArray();

    public static void RequireUnallocated(Guid employeeId, IReadOnlyList<OvertimeEligibleInterval> intervals,
        IReadOnlyList<WorkTreatmentCreditEvidence> credits)
    {
        if (credits is null || credits.Any(x => x is null || x.Range is null || x.Range.EmployeeId != employeeId ||
            x.RequestId == Guid.Empty || x.ApprovalId == Guid.Empty || x.DecisionId == Guid.Empty || x.ReviewerAccountId == Guid.Empty ||
            x.Range.SourceId != x.RequestId || !Enum.IsDefined(x.Range.Role) ||
            x.Range.StartUtc.Offset != TimeSpan.Zero || x.Range.EndUtc.Offset != TimeSpan.Zero || x.Range.EndUtc <= x.Range.StartUtc ||
            !OvertimeRules.Hash(x.SourceFingerprint) || !OvertimeRules.Hash(x.PolicyFingerprint)))
            throw AttendanceOperation.Conflict("overtime_work_credit_evidence_unavailable");
        if (credits.Any(x => intervals.Any(i => x.Range.StartUtc < i.EndsAtUtc && x.Range.EndUtc > i.StartsAtUtc)))
            throw AttendanceOperation.Conflict("overtime_work_credit_overlap");
    }

    /// <summary>An evaluated duration helper is not authority for an unconfigured monetary formula.</summary>
    public static void RequireSupportedFormula(OvertimeSourceResponse source)
    {
        if (source.Roster.StartsAt is null || source.Roster.EndsAt is null)
            throw AttendanceOperation.Conflict("overtime_schedule_unavailable");
        if ((source.Roster.EndsAt.Value - source.Roster.StartsAt.Value).Ticks != OvertimeFormula.StandardDutyTicks)
            throw AttendanceOperation.Conflict("overtime_non_nine_hour_schedule_unavailable");
        if (source.DurationPolicy.RateRuleCode != OvertimeFormula.Version)
            throw AttendanceOperation.Conflict("overtime_rate_policy_unavailable");
    }

    /// <summary>Bind all retained source, amount and decision fields. A digest does not authorize
    /// a client-supplied award; Finance must obtain the actual record through the permissioned reader.</summary>
    public static string Fingerprint(OvertimeFinancialAward value) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, award = value with { AwardFingerprint = "" } })));
}

public sealed partial class OvertimeService
{
    private static OvertimeFinancialAward? MapFinancialAward(EvidenceState value)
    {
        if (value.Row.Approval.State != ApprovalState.Approved || value.Decision?.FinancialEvidence is not { } financial)
            return null;
        var decision = value.Row.Decision!;
        if (!Guid.TryParse(decision.ActorUserId, out var reviewer) || reviewer == Guid.Empty)
            throw AttendanceOperation.Conflict("overtime_decision_evidence_unavailable");
        var proposal = value.Snapshot.Proposal;
        var intervals = OvertimeFinancialAwardRules.ApprovedIntervals(value.Snapshot.Inputs.Source, proposal);
        if (intervals.Sum(x => (x.EndsAtUtc - x.StartsAtUtc).Ticks) != financial.ApprovedTicks)
            throw AttendanceOperation.Conflict("overtime_decision_evidence_unavailable");
        var award = new OvertimeFinancialAward(1, value.Row.Request.Id, value.Row.Approval.Id, decision.Id,
            value.Snapshot.SubmissionId, value.Row.Request.EmployeeId, value.Row.BusinessDate, value.Row.Request.WorkSessionId,
            value.Snapshot.Inputs.Source.SiteId!.Value, value.Row.Request.JobReference, value.Snapshot.Inputs, intervals, financial,
            new(value.Snapshot.RequesterAccountId, reviewer, decision.ActorRoleCode, decision.DecidedAt), "");
        return award with { AwardFingerprint = OvertimeFinancialAwardRules.Fingerprint(award) };
    }
}
