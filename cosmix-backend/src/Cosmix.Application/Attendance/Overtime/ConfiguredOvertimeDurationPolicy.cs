using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.Policies;

namespace Cosmix.Application.Attendance.Overtime;

public sealed class ConfiguredOvertimeDurationPolicy(IAttendancePolicyResolver policies) : IOvertimeDurationPolicy
{
    public async Task<OvertimeDurationResult> EvaluateAsync(Guid employeeId, DateOnly date,
        IReadOnlyList<OvertimeSessionSource> sessions, EffectiveRosterResponse roster, CancellationToken ct)
    {
        var policy = await policies.ResolveAsync(employeeId, date, ct);
        if (policy.State != "effective" || policy.PolicyVersionId is null || policy.PolicyVersionId == Guid.Empty ||
            policy.Rules?.Overtime is not { } rules)
            return new("overtime_policy_not_configured", null, null, null, []);
        if (Cosmix.Domain.HR.Policies.AttendanceOvertimeRulesValidation.Validate(rules).Count != 0)
            return new("overtime_policy_invalid", null, null, null, []);
        return ConfirmedOvertimeDurationPolicy.Evaluate(employeeId, date, sessions, roster,
            checked((long)rules.StandardDutySeconds!.Value * TimeSpan.TicksPerSecond),
            ConfiguredOvertimeFormula.Version, policy.PolicyVersionId, rules);
    }
}
