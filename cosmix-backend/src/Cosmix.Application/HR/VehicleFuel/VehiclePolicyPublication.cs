using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.VehicleFuel;

public sealed partial class VehicleFuelService
{
    public Task<VehicleFuelPage<VehiclePolicyView>> ListPoliciesAsync(int page, int pageSize, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(VehiclePolicyPermissions.Manage, "vehicle.policy.list", null, context, async (_, token) =>
        {
            PolicyPage(page, pageSize);
            var rows = await repository.PagePoliciesAsync((page - 1) * pageSize, pageSize, token);
            return new VehicleFuelPage<VehiclePolicyView>(rows.Items.Select(PolicyView).ToArray(), page, pageSize, rows.TotalCount,
                page * pageSize < rows.TotalCount);
        }, ct, requireAdministrator: true);

    public Task<VehiclePolicyView> PolicyDetailAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(VehiclePolicyPermissions.Manage, "vehicle.policy.detail", null, context,
            async (_, token) => PolicyView(await FindPolicy(id, token)), ct, requireAdministrator: true);

    public Task<VehicleFuelPage<VehiclePolicyVersionView>> PolicyVersionsAsync(Guid id, int page, int pageSize,
        AuthRequestContext context, CancellationToken ct) => operation.ReadAsync(VehiclePolicyPermissions.Manage,
            "vehicle.policy.versions", null, context, async (_, token) =>
            {
                PolicyPage(page, pageSize); await FindPolicy(id, token);
                var rows = await repository.PagePolicyVersionsAsync(id, (page - 1) * pageSize, pageSize, token);
                return new VehicleFuelPage<VehiclePolicyVersionView>(rows.Items.Select(PolicyVersionView).ToArray(), page, pageSize,
                    rows.TotalCount, page * pageSize < rows.TotalCount);
            }, ct, requireAdministrator: true);

    public Task<VehiclePolicyVersionView> PolicyVersionDetailAsync(Guid id, Guid versionId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(VehiclePolicyPermissions.Manage, "vehicle.policy.version.detail", null, context, async (_, token) =>
        {
            await FindPolicy(id, token);
            var version = await repository.FindPolicyVersionAsync(id, versionId, token)
                ?? throw AttendanceOperation.Missing("vehicle_policy_version_not_found");
            return PolicyVersionView(version);
        }, ct, requireAdministrator: true);

    public Task<VehiclePolicyView> CreatePolicyAsync(CreateVehiclePolicyRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        request is null ? throw AttendanceOperation.Invalid("vehicle_policy_request_required") :
        operation.WriteAsync(VehiclePolicyPermissions.Manage, "vehicle.policy.create", null, key, request, request.Reason, context,
            async (_, token) =>
            {
                if (request.Code is null || !Regex.IsMatch(request.Code, @"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,39}\z") ||
                    string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200 || request.Name.Any(char.IsControl))
                    throw AttendanceOperation.Invalid("vehicle_policy_metadata_invalid");
                var policy = new Policy(request.Code, request.Name, PolicyCategory.VehicleFuel);
                if (await repository.PolicyCodeExistsAsync(policy.Code, token)) throw AttendanceOperation.Conflict("policy_code_exists");
                repository.AddPolicy(policy); await repository.SaveChangesAsync(token);
                return new AttendanceChange<VehiclePolicyView>(policy.Id, null, PolicyView(policy));
            }, ct, requireAdministrator: true);

    public Task<VehiclePolicyValidation> ValidatePolicyAsync(ValidateVehiclePolicyRequest request, AuthRequestContext context, CancellationToken ct) =>
        request is null ? throw AttendanceOperation.Invalid("vehicle_policy_request_required") :
        operation.ReadAsync(VehiclePolicyPermissions.Manage, "vehicle.policy.validate", null, context,
            (actor, token) => ValidatePublication(request.Rules, request.EffectiveFrom, request.EffectiveTo, actor.Now, token),
            ct, requireAdministrator: true);

    public Task<VehiclePolicyPreview> PreviewPolicyAsync(Guid id, PreviewVehiclePolicyRequest request, AuthRequestContext context, CancellationToken ct) =>
        request is null ? throw AttendanceOperation.Invalid("vehicle_policy_request_required") :
        operation.ReadAsync(VehiclePolicyPermissions.Manage, "vehicle.policy.preview", null, context, async (actor, token) =>
        {
            var policy = await CurrentPolicy(id, request.ExpectedVersion, request.ExpectedRevision, token);
            return await PolicyPreview(policy, request, actor.Now, token);
        }, ct, requireAdministrator: true);

    public Task<VehiclePolicyPublicationResult> PublishPolicyAsync(Guid id, PublishVehiclePolicyRequest request, string key,
        AuthRequestContext context, CancellationToken ct) =>
        request is null ? throw AttendanceOperation.Invalid("vehicle_policy_request_required") : operation.WriteAsync(VehiclePolicyPermissions.Manage,
            "vehicle.policy.publish", null, key, new { id, request }, request.Reason, context, async (actor, token) =>
            {
                var policy = await CurrentPolicy(id, request.ExpectedVersion, request.ExpectedRevision, token);
                var preview = await PolicyPreview(policy, new(request.ExpectedVersion, request.ExpectedRevision,
                    request.EffectiveFrom, request.EffectiveTo, request.Rules), actor.Now, token);
                if (!preview.CanPublish) throw AttendanceOperation.Invalid("vehicle_policy_publication_invalid");
                if (preview.PreviewFingerprint != request.ExpectedPreviewFingerprint)
                    throw AttendanceOperation.Conflict("vehicle_policy_preview_changed");
                var before = PolicyView(policy);
                var number = checked(await repository.LatestPolicyVersionNumberAsync(id, token) + 1);
                var version = new PolicyVersion(id, number, request.EffectiveFrom, request.EffectiveTo,
                    $"vehicle-fuel.v{request.Rules.GetProperty("SchemaVersion").GetInt32()}", preview.CanonicalJson!, null, null,
                    actor.Identity.AccountId.ToString(), actor.Now, PolicyAuthority.DirectHr);
                repository.ExpectPolicyVersion(policy, SchedulingOperation.Version(request.ExpectedVersion));
                repository.AddPolicyVersion(version); policy.AdvanceRevision(); await repository.SaveChangesAsync(token);
                return new AttendanceChange<VehiclePolicyPublicationResult>(version.Id, before, new(PolicyView(policy), PolicyVersionView(version)));
            }, ct, requireAdministrator: true);

    private async Task<VehiclePolicyPreview> PolicyPreview(Policy policy, PreviewVehiclePolicyRequest request, DateTimeOffset now, CancellationToken ct)
    {
        var validation = await ValidatePublication(request.Rules, request.EffectiveFrom, request.EffectiveTo, now, ct, policy.Id);
        var fingerprint = VehiclePolicyRulesCodec.Hash(JsonSerializer.Serialize(new { policy.Id, policy.Revision,
            Version = Convert.ToBase64String(policy.RowVersion), request.EffectiveFrom, request.EffectiveTo,
            validation.CanonicalJson, validation.Errors }));
        return new(validation.IsValid, policy.Revision, Convert.ToBase64String(policy.RowVersion), validation.CanonicalJson,
            validation.ConfigurationSha256, fingerprint, validation.Errors);
    }

    private async Task<VehiclePolicyValidation> ValidatePublication(JsonElement json, DateOnly from, DateOnly? to, DateTimeOffset now, CancellationToken ct, Guid? policyId = null)
    {
        var validation = VehiclePolicyRulesCodec.Validate(json, from, to);
        var errors = validation.Errors.ToList();
        if (from <= RosterSchedule.Today(now)) errors.Add(new("$.effectiveFrom", "vehicle_policy_future_date_required"));
        if (validation.IsValid)
        {
            var rules = JsonSerializer.Deserialize<VehiclePolicyRules>(validation.CanonicalJson!)!;
            if (rules.Succession is { } succession)
            {
                var predecessor = await repository.FindVehicleFuelPolicyVersionAsync(succession.PreviousPolicyVersionId, ct);
                if (predecessor is null || !VehiclePolicySuccession.CanSucceed(await repository.ListPolicyPeriodVersionsAsync(ct), policyId,
                    succession.PreviousPolicyVersionId, from, to!.Value)) errors.Add(new("$.rules.Succession", "vehicle_policy_successor_conflict"));
            }
            else if (await repository.HasPolicyPeriodOverlapAsync(from, to!.Value, ct))
                errors.Add(new("$.effectiveFrom", "vehicle_policy_period_overlap"));
            var ids = rules.Workflow.StaffAssignments!.Select(x => x.EmployeeId).Distinct().Order().ToArray();
            var existing = await repository.ExistingPolicyEmployeesAsync(ids, ct);
            if (ids.Except(existing).Any()) errors.Add(new("$.rules.Workflow.StaffAssignments", "vehicle_policy_employee_not_found"));
            var codes = (rules.Workflow.IncidentReview?.RoleCodes ?? [])
                .Concat(rules.Adjustments?.Monthly?.ReviewerRoleCodes ?? [])
                .Concat(rules.Adjustments?.TripAttribution?.ReviewerRoleCodes ?? [])
                .Concat(rules.Adjustments?.IncidentAmendments?.ReviewerRoleCodes ?? []).Distinct(StringComparer.Ordinal).ToArray();
            var active = await repository.ActivePolicyRoleCodesAsync(codes, ct);
            if (codes.Except(active, StringComparer.Ordinal).Any())
                errors.Add(new("$.rules.Workflow.IncidentReview", "vehicle_policy_role_not_active"));
            foreach (var transition in rules.Adjustments?.MaintenanceTransitions ?? [])
                if (await repository.FindVehicleFuelPolicyVersionAsync(transition.PreviousPolicyVersionId, ct) is not { } previous || previous.EffectiveFrom >= from)
                    errors.Add(new("$.rules.Adjustments.MaintenanceTransitions", "vehicle_previous_policy_not_found"));
        }
        return new(errors.Count == 0, errors.Count == 0 ? validation.CanonicalJson : null,
            errors.Count == 0 ? validation.ConfigurationSha256 : null, errors);
    }

    private async Task<Policy> FindPolicy(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) throw AttendanceOperation.Invalid("vehicle_policy_id_required");
        var policy = await repository.FindPolicyAsync(id, ct);
        return policy is not null && policy.Category == PolicyCategory.VehicleFuel ? policy :
            throw AttendanceOperation.Missing("vehicle_policy_not_found");
    }
    private async Task<Policy> CurrentPolicy(Guid id, string expected, int revision, CancellationToken ct)
    {
        var bytes = SchedulingOperation.Version(expected); var policy = await FindPolicy(id, ct);
        if (!policy.IsActive) throw AttendanceOperation.Conflict("vehicle_policy_inactive");
        if (revision != policy.Revision || !bytes.SequenceEqual(policy.RowVersion))
            throw AttendanceOperation.Conflict("vehicle_policy_version_conflict");
        return policy;
    }
    private static void PolicyPage(int page, int size)
    { if (page is < 1 or > 4096 || size is < 1 or > 100) throw AttendanceOperation.Invalid("vehicle_policy_page_invalid"); }
    private static VehiclePolicyView PolicyView(Policy policy) => new(policy.Id, policy.Code, policy.Name, policy.IsActive,
        policy.Revision, Convert.ToBase64String(policy.RowVersion));
    private static VehiclePolicyVersionView PolicyVersionView(PolicyVersion version) => new(version.Id, version.PolicyId,
        version.VersionNumber, version.EffectiveFrom, version.EffectiveTo, version.RulesSchemaVersion, version.RulesJson,
        VehiclePolicyRulesCodec.Hash(version.RulesJson), version.PublishedAt, version.PublishedByUserId, version.Authority.ToString());
}
