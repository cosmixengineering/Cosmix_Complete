using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.HR.Allowances;

namespace Cosmix.Application.HR.Allowances;

public sealed partial class AllowanceService
{
    private static readonly TimeZoneInfo ScopeZone = TimeZoneInfo.FindSystemTimeZoneById(RosterSchedule.TimeZoneId);

    private async Task<IReadOnlyList<AttendanceResourceScope>> PageScopes(AllowancePageQuery query, Guid? requester, CancellationToken ct)
    {
        var employeeId = query.EmployeeId!.Value;
        var scopes = new List<AttendanceResourceScope>(await summaries.ResourceScopesAsync(employeeId,
            query.From!.Value, query.To!.Value.AddDays(-1), ct));
        foreach (var row in await repository.PageAsync(query, requester, ct))
        {
            if (row.Claim.Kind == AllowanceKind.OutstationDaily)
            {
                var retained = await repository.FindAsync(employeeId, row.Claim.Id, ct) ?? throw AttendanceOperation.Missing();
                scopes.AddRange(await DeploymentWorkflow.AllowanceClaimScopesAsync(retained, ct));
            }
            else
            {
                var dates = ScopeDates(row.Claim.PayableDate, row.Claim.EligibilityPeriodStart, row.Claim.EligibilityPeriodEnd);
                scopes.AddRange(await summaries.ResourceScopesAsync(employeeId, dates.From, dates.To, ct));
            }
        }
        if (!AttendanceScopeEvaluation.IsValid(scopes)) throw AttendanceOperation.Conflict("allowance_scope_limit");
        return scopes;
    }

    private async Task<IReadOnlyList<AttendanceResourceScope>> SourceScopes(Guid employeeId,
        DateOnly date, DateTimeOffset now, CancellationToken ct)
    {
        var sessions = await repository.SessionSourcesAsync(employeeId, date, ct);
        if (sessions.Count > 512 || sessions.Select(x => x.SessionKey).Distinct().Count() != sessions.Count)
            throw AttendanceOperation.Conflict("allowance_source_limit");
        var from = date;
        var to = date;
        foreach (var session in sessions.Where(x => x.ValidDutyEvidence && x.EndsAtUtc.HasValue &&
                     x.EndsAtUtc > x.StartsAtUtc && x.EndsAtUtc <= now))
        {
            var dates = ScopeDates(date, session.StartsAtUtc, session.EndsAtUtc!.Value);
            if (dates.From < from) from = dates.From;
            if (dates.To > to) to = dates.To;
        }
        if (to.DayNumber - from.DayNumber > 366) throw AttendanceOperation.Conflict("allowance_source_limit");
        return await summaries.ResourceScopesAsync(employeeId, from, to, ct);
    }

    private static (DateOnly From, DateOnly To) ScopeDates(DateOnly payableDate,
        DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (payableDate == default || startsAt == default || endsAt <= startsAt)
            throw AttendanceOperation.Conflict("allowance_evidence_unavailable");
        var first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(startsAt, ScopeZone).DateTime);
        // The interval is half-open: a cycle ending exactly at midnight does not expose the next day.
        var last = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(endsAt.AddTicks(-1), ScopeZone).DateTime);
        var from = first < payableDate ? first : payableDate;
        var to = last > payableDate ? last : payableDate;
        if (to == DateOnly.MaxValue || to.DayNumber - from.DayNumber > 366)
            throw AttendanceOperation.Conflict("allowance_source_limit");
        return (from, to);
    }
}
