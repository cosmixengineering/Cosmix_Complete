using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.Leaves;

public sealed class LeaveSettingsService(AttendanceOperation operation, ILeaveRepository repository)
{
    public Task<LeaveTypeResponse> CreateTypeAsync(CreateLeaveTypeRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        if (request is null || !Enum.IsDefined(request.Kind)) throw AttendanceOperation.Invalid("invalid_leave_type");
        Text(request.Code, 40); Text(request.Name, 200);
        if (!Regex.IsMatch(request.Code.Trim(), @"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,39}\z")) throw AttendanceOperation.Invalid("invalid_leave_type_code");
        LeaveType type;
        try { type = new(request.Code, request.Name, request.RequiresEvidence, request.RequiresAdvanceApproval, request.ReducesRegularBalance, request.Kind); }
        catch (ArgumentException) { throw AttendanceOperation.Invalid("leave_type_contradicts_srs"); }
        return operation.WriteAsync(LeavePermissions.Configure, "attendance.leave.type.create", null, key, request, request.Reason, context,
            async (_, token) => {
                if (await repository.TypeCodeExistsAsync(type.Code, token)) throw AttendanceOperation.Conflict("leave_type_code_exists");
                repository.Add(type); await repository.SaveChangesAsync(token);
                return new AttendanceChange<LeaveTypeResponse>(type.Id, null, LeaveRules.Type(type));
            }, ct, requireSystemRole: true);
    }
    public Task<IReadOnlyList<LeavePolicyResponse>> PoliciesAsync(AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(LeavePermissions.Configure, "attendance.leave.settings.list", null, context,
            async (_, token) => { var versions = await repository.PolicyVersionsAsync(token);
                if (versions.Count > 500) throw AttendanceOperation.Conflict("leave_policy_history_limit");
                return (IReadOnlyList<LeavePolicyResponse>)versions.Select(x => Map(x.Policy, x)).ToArray(); }, ct, requireSystemRole: true);
    public Task<LeavePolicyResponse> PublishAsync(PublishLeavePolicyRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        if (request is null) throw AttendanceOperation.Invalid("invalid_leave_policy");
        Text(request.Code, 40); Text(request.Name, 200); LeavePolicySettings.Validate(request.Settings);
        if (!Regex.IsMatch(request.Code.Trim(), @"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,39}\z") || request.EffectiveFrom.Year is < 1901 or > 9997 ||
            request.EffectiveTo.Year > 9998 || request.EffectiveTo <= request.EffectiveFrom) throw AttendanceOperation.Invalid("invalid_leave_policy");
        return operation.WriteAsync(LeavePermissions.Configure, "attendance.leave.settings.publish", null, key, request, request.Reason, context,
            async (actor, token) =>
            {
                var policy = new Policy(request.Code, request.Name, PolicyCategory.Leave);
                if (await repository.PolicyCodeExistsAsync(policy.Code, token)) throw AttendanceOperation.Conflict("leave_policy_code_exists");
                if (!await repository.SettingsReferencesValidAsync(request.Settings, token)) throw AttendanceOperation.Invalid("leave_settings_reference_unavailable");
                if (await repository.AnyLockedAsync(request.EffectiveFrom, request.EffectiveTo.AddDays(-1), token)) throw AttendanceOperation.Conflict("leave_policy_period_locked");
                var version = new PolicyVersion(policy.Id, 1, request.EffectiveFrom, request.EffectiveTo, request.Settings.PublishedSchemaVersion,
                    JsonSerializer.Serialize(request.Settings), null, null, actor.Identity.AccountId.ToString(), actor.Now, PolicyAuthority.DirectHr);
                repository.Add(policy, version); await repository.SaveChangesAsync(token);
                return new AttendanceChange<LeavePolicyResponse>(version.Id, null, Map(policy, version));
            }, ct, requireSystemRole: true);
    }
    private static LeavePolicyResponse Map(Policy p, PolicyVersion v) => new(p.Id, v.Id, p.Code, p.Name, v.EffectiveFrom, v.EffectiveTo, LeavePolicySettings.Parse(v.RulesJson, v.RulesSchemaVersion), v.Authority.ToString());
    private static void Text(string value, int max)
    { if (string.IsNullOrWhiteSpace(value) || value.Length > max || value.Any(char.IsControl)) throw AttendanceOperation.Invalid("invalid_leave_setting_text"); }
}
