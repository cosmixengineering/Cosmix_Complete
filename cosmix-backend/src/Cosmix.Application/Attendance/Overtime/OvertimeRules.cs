using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;

namespace Cosmix.Application.Attendance.Overtime;

public static class OvertimeRules
{
    public static void Validate(SubmitOvertimeRequest request)
    {
        RosterSchedule.ValidateDate(request.BusinessDate);
        if (request.WorkSessionId == Guid.Empty || !Enum.IsDefined(request.Activity) || request.ResubmissionOfRequestId == Guid.Empty ||
            request.StartsAtUtc.Offset != TimeSpan.Zero || request.EndsAtUtc.Offset != TimeSpan.Zero ||
            request.EndsAtUtc <= request.StartsAtUtc || request.EndsAtUtc - request.StartsAtUtc > TimeSpan.FromDays(7) ||
            (request.EndsAtUtc - request.StartsAtUtc).Ticks < TimeSpan.TicksPerMinute || !Hash(request.ExpectedSourceFingerprint))
            throw AttendanceOperation.Invalid("invalid_overtime_proposal");
        Text(request.Reason, 1000); Text(request.WrittenEvidence, 4000);
        if (request.JobReference is not null) Text(request.JobReference, 200);
    }
    public static int RequestedMinutes(SubmitOvertimeRequest request) => checked((int)((request.EndsAtUtc - request.StartsAtUtc).Ticks / TimeSpan.TicksPerMinute));
    public static OvertimeSessionSource Selected(OvertimeSourceResponse source, SubmitOvertimeRequest proposal, DateTimeOffset now)
    {
        if (source.BusinessDate != proposal.BusinessDate || !OvertimeTaxonomy.Outside(source))
            throw AttendanceOperation.Conflict(OvertimeTaxonomy.Office(source) ? "overtime_office_ineligible" : "overtime_category_unavailable");
        if (source.SiteId is null || source.SiteId == Guid.Empty || source.Roster.BranchId.HasValue || source.Roster.SiteId != source.SiteId)
            throw AttendanceOperation.Conflict("overtime_roster_source_conflict");
        if (source.IsLocked) throw AttendanceOperation.Conflict("overtime_locked_period_adjustment_required");
        if (source.Eligibility.Allowed == false) throw AttendanceOperation.Conflict("overtime_not_eligible");
        var session = source.Sessions.SingleOrDefault(x => x.Id == proposal.WorkSessionId);
        if (session?.EndsAtUtc is null || !session.ValidDutyEvidence || proposal.StartsAtUtc < session.StartsAtUtc ||
            proposal.EndsAtUtc > session.EndsAtUtc || proposal.EndsAtUtc > now)
            throw AttendanceOperation.Conflict("overtime_closed_session_required");
        if (source.Roster.Status != "scheduled" || source.Roster.ShiftVersionId != session.ShiftVersionId ||
            source.Roster.SiteId != session.SiteId || source.SiteId != session.SiteId || source.Roster.EndsAt is null)
            throw AttendanceOperation.Conflict("overtime_roster_source_conflict");
        if (proposal.StartsAtUtc < source.Roster.EndsAt)
            throw AttendanceOperation.Conflict("overtime_before_scheduled_end");
        return session;
    }
    public static int ApprovedMinutes(OvertimeSourceResponse source, SubmitOvertimeRequest proposal, DateTimeOffset now)
        => checked((int)(ApprovedTicks(source, proposal, now) / TimeSpan.TicksPerMinute));
    public static long ApprovedTicks(OvertimeSourceResponse source, SubmitOvertimeRequest proposal, DateTimeOffset now)
    {
        Selected(source, proposal, now);
        if (proposal.Activity != OvertimeActivity.Work) throw AttendanceOperation.Conflict("overtime_travel_requires_separate_policy");
        if (source.Eligibility.Status != "configured" || source.Eligibility.Allowed != true || source.Eligibility.VersionId is null || source.Eligibility.VersionId == Guid.Empty)
            throw AttendanceOperation.Conflict("overtime_eligibility_not_configured");
        var policy = source.DurationPolicy;
        ValidatePolicy(policy);
        if (source.Sessions.Any(x => x.EndsAtUtc is null || !x.ValidDutyEvidence))
            throw AttendanceOperation.Conflict("overtime_incomplete_duty_evidence");
        if (policy.Status != "evaluated" || policy.ScheduledDutyCompleted != true)
            throw AttendanceOperation.Conflict("overtime_scheduled_duty_not_confirmed");
        long ticks = 0;
        foreach (var interval in policy.EligibleIntervals)
        {
            var from = interval.StartsAtUtc > proposal.StartsAtUtc ? interval.StartsAtUtc : proposal.StartsAtUtc;
            var to = interval.EndsAtUtc < proposal.EndsAtUtc ? interval.EndsAtUtc : proposal.EndsAtUtc;
            if (to > from) ticks = checked(ticks + (to - from).Ticks);
        }
        if (ticks < TimeSpan.TicksPerMinute || ticks > (proposal.EndsAtUtc - proposal.StartsAtUtc).Ticks) throw AttendanceOperation.Conflict("overtime_no_approved_minutes");
        return ticks;
    }
    public static void ValidatePolicy(OvertimeDurationResult policy)
    {
        if (policy.EligibleIntervals is null || policy.EligibleIntervals.Count > 512) throw AttendanceOperation.Conflict("invalid_overtime_policy_contract");
        if (policy.Status != "evaluated")
        {
            if (policy.PolicyVersionId.HasValue || policy.CalculationVersion is not null || policy.ScheduledDutyCompleted.HasValue ||
                policy.EligibleIntervals.Count != 0 || policy.RateRuleCode is not null) throw AttendanceOperation.Conflict("invalid_overtime_policy_contract");
            return;
        }
        if (policy.PolicyVersionId == Guid.Empty || policy.ScheduledDutyCompleted is null ||
            string.IsNullOrWhiteSpace(policy.CalculationVersion) || policy.CalculationVersion.Length > 100 || policy.CalculationVersion.Any(char.IsControl) ||
            policy.RateRuleCode is { } rate && (string.IsNullOrWhiteSpace(rate) || rate.Length > 100 || rate.Any(char.IsControl)))
            throw AttendanceOperation.Conflict("invalid_overtime_policy_contract");
        DateTimeOffset? end = null;
        foreach (var interval in policy.EligibleIntervals.OrderBy(x => x.StartsAtUtc))
        {
            if (interval.StartsAtUtc.Offset != TimeSpan.Zero || interval.EndsAtUtc.Offset != TimeSpan.Zero || interval.EndsAtUtc <= interval.StartsAtUtc ||
                end.HasValue && interval.StartsAtUtc < end.Value) throw AttendanceOperation.Conflict("invalid_overtime_policy_contract");
            end = interval.EndsAtUtc;
        }
    }
    public static string Fingerprint(OvertimeSourceSnapshot source) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, source = source.Source with { SourceFingerprint = "" }, source.CompensationRecordId, source.Employment, source.OriginalScopes })));
    public static bool Hash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    public static void Text(string? value, int length)
    { if (string.IsNullOrWhiteSpace(value) || value.Length > length || value.Any(char.IsControl)) throw AttendanceOperation.Invalid("invalid_overtime_text"); }
}
