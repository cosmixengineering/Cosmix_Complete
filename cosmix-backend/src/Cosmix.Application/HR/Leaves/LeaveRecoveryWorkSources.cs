using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.WorkTreatments;

namespace Cosmix.Application.HR.Leaves;

public sealed class LeaveRecoveryWorkSources(LeaveEncashmentService parents, IWorkTreatmentRepository work,
    IIndividualRosterResolver rosters)
{
    public async Task<LeaveRecoveryWorkSource> LoadAsync(Guid leaveId, Guid commitmentId, Guid? employeeId,
        Guid policyId, DateTimeOffset now, CancellationToken ct)
    {
        work.RequireTransaction();
        var parent = await parents.RecoveryWorkParentAsync(leaveId, commitmentId, employeeId, now, ct);
        var policy = await parents.RecoveryWorkPolicyAsync(parent, policyId, now, ct);
        var date = parent.Commitment.CommitmentDate;
        var employment = await work.EmploymentAsync(parent.EmployeeId, date, ct);
        if (employment is null || employment.CategoryId != policy.StaffCategoryId)
            throw AttendanceOperation.Conflict("leave_recovery_employment_unavailable");
        var sessions = await work.SessionsAsync(parent.EmployeeId, date, date, ct);
        if (sessions.Count > 512 || sessions.Select(x => x.Id).Distinct().Count() != sessions.Count ||
            sessions.Select(x => x.Key).Distinct().Count() != sessions.Count || sessions.Any(x => x.BusinessDate != date))
            throw AttendanceOperation.Conflict("leave_recovery_work_source_unavailable");
        var schedules = new List<EffectiveRosterResponse>();
        // Adjacent duty starts protect physical overlap across the retained business-date boundary.
        for (var day = date.AddDays(-1); day <= date.AddDays(1); day = day.AddDays(1))
        {
            if (day == date && await work.LockedAsync(day, ct)) throw AttendanceOperation.Conflict("leave_recovery_period_locked");
            var roster = await rosters.ResolveAsync(parent.EmployeeId, day, ct);
            if (roster.EmployeeId != parent.EmployeeId || roster.BusinessDate != day || roster.ShiftVersionId is null ||
                roster.StartsAt is null || roster.EndsAt is null || roster.EndsAt <= roster.StartsAt ||
                roster.EndsAt - roster.StartsAt > TimeSpan.FromDays(1))
                throw AttendanceOperation.Conflict("leave_recovery_roster_unavailable");
            schedules.Add(roster);
        }
        var source = new LeaveRecoveryWorkSource(parent, policy, employment, schedules,
            sessions.OrderBy(x => x.Key).ThenBy(x => x.Id).ToArray(), "");
        return source with { Fingerprint = LeaveRecoveryWorkRules.Fingerprint(source) };
    }
}

public static class LeaveRecoveryWorkRules
{
    public static string Fingerprint(LeaveRecoveryWorkSource source) => WorkTreatmentRules.Hash(source with { Fingerprint = "" });

    public static IReadOnlyList<WorkCreditRange> Evaluate(Guid claimId, SubmitLeaveRecoveryWorkRequest request,
        LeaveRecoveryWorkSource source, DateTimeOffset now)
    {
        source.Policy.Settings.RequireConfigured();
        if (source.Fingerprint != Fingerprint(source) || request.ExpectedSourceFingerprint != source.Fingerprint ||
            request.PolicyVersionId != source.Policy.VersionId)
            throw AttendanceOperation.Conflict("leave_recovery_work_source_changed");
        if (request.Intervals is not { Count: > 0 and <= 64 } || request.Intervals.Any(x => x is null) || claimId == Guid.Empty)
            throw AttendanceOperation.Invalid("invalid_leave_recovery_work_intervals");
        var parent = source.Parent;
        var date = parent.Commitment.CommitmentDate;
        var roster = source.Rosters.Single(x => x.BusinessDate == date);
        var result = new List<WorkCreditRange>();
        foreach (var interval in request.Intervals)
        {
            WorkTreatmentRules.Window(interval.StartUtc, interval.EndUtc);
            if (interval.BusinessDate != date || interval.StartUtc.UtcTicks % TimeSpan.TicksPerMinute != 0 ||
                interval.EndUtc.UtcTicks % TimeSpan.TicksPerMinute != 0 || interval.EndUtc > now ||
                interval.StartUtc < source.Policy.PublishedAt)
                throw AttendanceOperation.Conflict("leave_recovery_actual_work_unavailable");
            var session = source.Sessions.SingleOrDefault(x => x.Id == interval.SessionId);
            if (session is null || !session.Verified || session.EndUtc is null || session.EndUtc > now || session.Revision < 1 ||
                session.StartUtc > interval.StartUtc || session.EndUtc < interval.EndUtc ||
                session.BusinessDate != date || session.SiteId != source.Employment.SiteId || session.BranchId != source.Employment.BranchId ||
                session.AssignmentId != source.Employment.AssignmentId || roster.SiteId != source.Employment.SiteId ||
                session.ShiftVersionId != roster.ShiftVersionId || session.Key == Guid.Empty)
                throw AttendanceOperation.Conflict("leave_recovery_verified_session_required");
            if (interval.StartUtc < source.Rosters[0].StartsAt || interval.EndUtc > source.Rosters[^1].EndsAt)
                throw AttendanceOperation.Conflict("leave_recovery_work_window_unavailable");
            if (source.Rosters.Any(x => WorkTreatmentRules.Overlap(interval.StartUtc, interval.EndUtc, x.StartsAt!.Value, x.EndsAt!.Value)))
                throw AttendanceOperation.Conflict("leave_recovery_scheduled_work_not_qualifying");
            result.Add(new(parent.EmployeeId, claimId, WorkCreditRole.LeaveRecovery, session.Key, session.Id, date, interval.StartUtc, interval.EndUtc));
        }
        WorkTreatmentRules.Exclusive(result, []);
        var minutes = Minutes(result);
        if (minutes < source.Policy.Settings.MinimumClaimMinutes || minutes > source.Policy.Settings.MaximumClaimMinutes)
            throw AttendanceOperation.Conflict("leave_recovery_claim_limit");
        return result;
    }

    public static int Minutes(IReadOnlyList<WorkCreditRange> ranges) => checked((int)(ranges.Sum(x => x.Ticks) / TimeSpan.TicksPerMinute));
}
