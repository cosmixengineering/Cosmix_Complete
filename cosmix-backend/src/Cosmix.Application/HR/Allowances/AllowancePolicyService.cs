using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.Allowances;

/// <summary>Direct allowance policy publication and dated assignment. Money values exist only in
/// published, effective-dated versions; nothing here seeds or infers an amount.</summary>
public sealed class AllowancePolicyService(AttendanceOperation operation, IAllowancePolicyRepository repository,
    IAllowancePolicyResolver resolver, IAttendanceSummaryRepository summaries)
{
    public Task<AllowancePolicyPage<AllowancePolicyView>> ListAsync(AuthRequestContext context, CancellationToken ct,
        int page = 1, int pageSize = 50, string? search = null) =>
        operation.ReadAsync(AllowancePermissions.Policy, "attendance.allowance.policy.list", null, context,
            async (_, token) => Page((await repository.ListAsync(token)).Where(x => string.IsNullOrEmpty(search) ||
                x.Code.Contains(search, StringComparison.OrdinalIgnoreCase) || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .Select(Map), page, pageSize), ct, requireSystemRole: true);

    public Task<AllowancePolicyView> DetailAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(AllowancePermissions.Policy, "attendance.allowance.policy.detail", null, context,
            async (_, token) => Map(await Find(id, token)), ct, requireSystemRole: true);

    public Task<AllowancePolicyPage<AllowancePolicyVersionView>> VersionsAsync(Guid id, AuthRequestContext context, CancellationToken ct,
        int page = 1, int pageSize = 50) =>
        operation.ReadAsync(AllowancePermissions.Policy, "attendance.allowance.policy.versions", null, context,
            async (_, token) => { await Find(id, token); return Page(
                (await repository.VersionsAsync(id, token)).Select(Map), page, pageSize); }, ct, requireSystemRole: true);

    public Task<AllowancePolicyPage<AllowancePolicyAssignmentView>> AssignmentsAsync(Guid id, AuthRequestContext context, CancellationToken ct,
        int page = 1, int pageSize = 50) =>
        operation.ReadAsync(AllowancePermissions.Policy, "attendance.allowance.policy.assignments", null, context,
            async (_, token) => { await Find(id, token); return Page(
                (await repository.AssignmentsAsync(id, token)).Select(x => Map(x.Assignment, x.End)), page, pageSize); }, ct, requireSystemRole: true);

    public Task<AllowancePolicyResolution> EffectiveAsync(Guid employeeId, DateOnly date, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(AllowancePermissions.Policy, "attendance.allowance.policy.effective", employeeId, context,
            (_, token) => resolver.ResolveAsync(employeeId, date, token), ct,
            resourceScopes: (_, token) => summaries.ResourceScopesAsync(employeeId, date, date, token));

    public Task<AllowancePolicyView> CreateAsync(CreateAllowancePolicyRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        Text(request.Name, 200); Text(request.Code, 40);
        if (!Regex.IsMatch(request.Code.Trim(), @"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,39}\z")) throw AttendanceOperation.Invalid("invalid_policy_code");
        return operation.WriteAsync(AllowancePermissions.Policy, "attendance.allowance.policy.create", null, key, request, request.Reason,
            context, async (_, token) =>
            {
                var policy = new Policy(request.Code, request.Name, PolicyCategory.Allowance);
                if (await repository.CodeExistsAsync(policy.Code, token)) throw AttendanceOperation.Conflict("policy_code_exists");
                repository.Add(policy); await repository.SaveChangesAsync(token);
                return new AttendanceChange<AllowancePolicyView>(policy.Id, null, Map(policy));
            }, ct, requireSystemRole: true);
    }

    public Task<AllowancePolicyPublicationPreview> PreviewAsync(Guid id, PreviewAllowancePolicyRequest request,
        AuthRequestContext context, CancellationToken ct) => operation.ReadAsync(AllowancePermissions.Policy,
            "attendance.allowance.policy.publication.preview", null, context, async (actor, token) =>
            {
                var policy = await Current(id, request.ExpectedVersion, request.ExpectedRevision, token);
                return await Preview(policy, request, actor.Now, token);
            }, ct, requireSystemRole: true);

    public Task<AllowancePolicyPublicationResult> PublishAsync(Guid id, PublishAllowancePolicyRequest request, string key,
        AuthRequestContext context, CancellationToken ct) => operation.WriteAsync(AllowancePermissions.Policy,
            "attendance.allowance.policy.publish", null, key, new { id, request }, request.Reason, context, async (actor, token) =>
            {
                var policy = await Current(id, request.ExpectedVersion, request.ExpectedRevision, token);
                var preview = await Preview(policy, new(request.ExpectedVersion, request.ExpectedRevision, request.EffectiveFrom,
                    request.EffectiveTo, request.Rules), actor.Now, token);
                if (!preview.CanPublish) throw AttendanceOperation.Invalid("allowance_policy_publication_invalid");
                if (preview.PreviewFingerprint != request.ExpectedPreviewFingerprint) throw AttendanceOperation.Conflict("allowance_policy_preview_changed");
                var before = Map(policy);
                var versions = await repository.VersionsAsync(id, token);
                var version = new PolicyVersion(id, versions.Count == 0 ? 1 : checked(versions.Max(x => x.VersionNumber) + 1),
                    request.EffectiveFrom, request.EffectiveTo, $"allowance.v{request.Rules!.SchemaVersion}", preview.CanonicalJson!, null, null,
                    actor.Identity.AccountId.ToString(), actor.Now, PolicyAuthority.DirectHr);
                repository.Add(version); policy.AdvanceRevision(); await repository.SaveChangesAsync(token);
                return new AttendanceChange<AllowancePolicyPublicationResult>(version.Id, before, new(Map(policy), Map(version)));
            }, ct, requireSystemRole: true);

    public Task<AllowancePolicyAssignmentResult> AssignAsync(Guid id, AssignAllowancePolicyRequest request, string key,
        AuthRequestContext context, CancellationToken ct) => operation.WriteAsync(AllowancePermissions.Policy,
            "attendance.allowance.policy.assign", null, key, new { id, request }, request.Reason, context, async (actor, token) =>
            {
                var policy = await Current(id, request.ExpectedVersion, request.ExpectedRevision, token);
                await ValidateAssignment(id, request.PolicyVersionId, request.Target, request.TargetId,
                    request.EffectiveFrom, request.EffectiveTo, null, actor.Now, token);
                var assignment = CreateAssignment(request.PolicyVersionId, request.Target, request.TargetId,
                    request.EffectiveFrom, request.EffectiveTo, request.Reason, actor);
                repository.Add(assignment); policy.AdvanceRevision(); await repository.SaveChangesAsync(token);
                return new AttendanceChange<AllowancePolicyAssignmentResult>(assignment.Id, null, new(Map(policy), Map(assignment, null)));
            }, ct, requireSystemRole: true);

    public Task<AllowancePolicyAssignmentResult> EndAsync(Guid id, Guid assignmentId, EndAllowancePolicyAssignmentRequest request,
        string key, AuthRequestContext context, CancellationToken ct) => operation.WriteAsync(AllowancePermissions.Policy,
            "attendance.allowance.policy.assignment.end", null, key, new { id, assignmentId, request }, request.Reason, context, async (actor, token) =>
            {
                var policy = await Current(id, request.ExpectedVersion, request.ExpectedRevision, token);
                var previous = await FindAssignment(id, assignmentId, token);
                await ValidateEnd(previous, request.EffectiveTo, actor.Now, token);
                var end = new PolicyAssignmentEnd(previous.Assignment, request.EffectiveTo, request.Reason, null,
                    actor.Identity.AccountId.ToString(), actor.Now, PolicyAuthority.DirectHr);
                repository.Add(end); policy.AdvanceRevision(); await repository.SaveChangesAsync(token);
                return new AttendanceChange<AllowancePolicyAssignmentResult>(end.Id, Map(previous.Assignment, null),
                    new(Map(policy), Map(previous.Assignment, end)));
            }, ct, requireSystemRole: true);

    public Task<AllowancePolicyAssignmentResult> ReplaceAsync(Guid id, Guid assignmentId, ReplaceAllowancePolicyAssignmentRequest request,
        string key, AuthRequestContext context, CancellationToken ct) => operation.WriteAsync(AllowancePermissions.Policy,
            "attendance.allowance.policy.assignment.replace", null, key, new { id, assignmentId, request }, request.Reason, context, async (actor, token) =>
            {
                var policy = await Current(id, request.ExpectedVersion, request.ExpectedRevision, token);
                var previous = await FindAssignment(id, assignmentId, token);
                await ValidateEnd(previous, request.EffectiveFrom, actor.Now, token);
                var target = Target(previous.Assignment);
                await ValidateAssignment(id, request.PolicyVersionId, target.Kind, target.Id, request.EffectiveFrom,
                    request.EffectiveTo, assignmentId, actor.Now, token);
                var end = new PolicyAssignmentEnd(previous.Assignment, request.EffectiveFrom, request.Reason, null,
                    actor.Identity.AccountId.ToString(), actor.Now, PolicyAuthority.DirectHr);
                var assignment = CreateAssignment(request.PolicyVersionId, target.Kind, target.Id,
                    request.EffectiveFrom, request.EffectiveTo, request.Reason, actor);
                repository.Add(end); repository.Add(assignment); policy.AdvanceRevision(); await repository.SaveChangesAsync(token);
                return new AttendanceChange<AllowancePolicyAssignmentResult>(assignment.Id, Map(previous.Assignment, null),
                    new(Map(policy), Map(assignment, null)));
            }, ct, requireSystemRole: true);

    private async Task<AllowancePolicyPublicationPreview> Preview(Policy policy, PreviewAllowancePolicyRequest request,
        DateTimeOffset now, CancellationToken ct)
    {
        var validation = AllowancePolicyRulesCodec.Validate(request.Rules);
        var errors = validation.Errors.ToList();
        var datesValid = ValidDates(request.EffectiveFrom, request.EffectiveTo);
        if (!datesValid) errors.Add(new("$.effectiveFrom", "invalid_policy_dates", "Use a valid start-inclusive, end-exclusive policy period."));
        if (datesValid && request.EffectiveFrom <= RosterSchedule.Today(now))
            errors.Add(new("$.effectiveFrom", "policy_future_date_required", "New policy periods must start after the current Pakistan business date."));
        var canonical = errors.Count == 0 ? validation.CanonicalJson : null;
        var configHash = canonical is null ? null : AllowancePolicyResolver.Hash(canonical);
        var fingerprint = AllowancePolicyResolver.Hash(JsonSerializer.Serialize(new { policy.Id, policy.Revision,
            Version = Convert.ToBase64String(policy.RowVersion), request.EffectiveFrom, request.EffectiveTo,
            validation.CanonicalJson, Errors = errors.Select(x => new { x.Path, x.Code }) }));
        return new(errors.Count == 0, policy.Revision, Convert.ToBase64String(policy.RowVersion), canonical, configHash, fingerprint, errors);
    }

    private async Task ValidateAssignment(Guid policyId, Guid versionId, AllowancePolicyTarget target, Guid targetId,
        DateOnly from, DateOnly? to, Guid? exceptId, DateTimeOffset now, CancellationToken ct)
    {
        if (!Enum.IsDefined(target) || targetId == Guid.Empty || !ValidDates(from, to))
            throw AttendanceOperation.Invalid("invalid_policy_assignment");
        Future(from, now);
        if (!await repository.TargetExistsAsync(target, targetId, ct)) throw AttendanceOperation.Missing("policy_target_not_found");
        var version = (await repository.VersionsAsync(policyId, ct)).SingleOrDefault(x => x.Id == versionId)
            ?? throw AttendanceOperation.Missing("policy_version_not_found");
        var rules = AllowancePolicyRulesCodec.ValidateAndCanonicalize(version.RulesJson);
        if (version.Authority != PolicyAuthority.DirectHr || version.ApprovalRequestId is not null ||
            !rules.IsValid || version.RulesSchemaVersion != $"allowance.v{rules.Rules!.SchemaVersion}")
            throw AttendanceOperation.Conflict("allowance_policy_version_not_publishable");
        if (from < version.EffectiveFrom || version.EffectiveTo.HasValue && (!to.HasValue || to > version.EffectiveTo))
            throw AttendanceOperation.Invalid("policy_assignment_outside_version");
        if (await repository.HasTargetOverlapAsync(target, targetId, from, to, exceptId, ct))
            throw AttendanceOperation.Conflict("policy_target_assignment_overlap");
    }

    private async Task ValidateEnd(AllowancePolicyCandidate row, DateOnly end, DateTimeOffset now, CancellationToken ct)
    {
        if (row.End is not null) throw AttendanceOperation.Conflict("policy_assignment_already_ended");
        if (!row.AuthorityValid) throw AttendanceOperation.Conflict("policy_assignment_authority_invalid");
        Future(end, now);
        if (end <= row.Assignment.EffectiveFrom || end == DateOnly.MaxValue || row.Assignment.EffectiveTo.HasValue && end >= row.Assignment.EffectiveTo)
            throw AttendanceOperation.Invalid("invalid_policy_assignment_end");
    }

    private async Task<Policy> Find(Guid id, CancellationToken ct) => id == Guid.Empty ? throw AttendanceOperation.Invalid() :
        await repository.FindAsync(id, ct) ?? throw AttendanceOperation.Missing("allowance_policy_not_found");

    private async Task<Policy> Current(Guid id, string expectedVersion, int expectedRevision, CancellationToken ct)
    {
        var bytes = SchedulingOperation.Version(expectedVersion); var policy = await Find(id, ct);
        if (!policy.IsActive) throw AttendanceOperation.Conflict("allowance_policy_inactive");
        if (expectedRevision != policy.Revision || !policy.RowVersion.SequenceEqual(bytes)) throw AttendanceOperation.Conflict("allowance_policy_version_conflict");
        repository.ExpectVersion(policy, bytes); return policy;
    }

    private async Task<AllowancePolicyCandidate> FindAssignment(Guid policyId, Guid id, CancellationToken ct) =>
        (await repository.AssignmentsAsync(policyId, ct)).SingleOrDefault(x => x.Assignment.Id == id)
        ?? throw AttendanceOperation.Missing("policy_assignment_not_found");

    private static PolicyAssignment CreateAssignment(Guid versionId, AllowancePolicyTarget target, Guid targetId,
        DateOnly from, DateOnly? to, string reason, AttendanceActor actor) => new(versionId, null,
        target == AllowancePolicyTarget.Employee ? targetId : null, target == AllowancePolicyTarget.Department ? targetId : null,
        target == AllowancePolicyTarget.StaffCategory ? targetId : null, target == AllowancePolicyTarget.Site ? targetId : null,
        from, to, reason, null, null, actor.Identity.AccountId.ToString(), actor.Now, PolicyAuthority.DirectHr);

    private static bool ValidDates(DateOnly from, DateOnly? to) => from != default && from < DateOnly.MaxValue &&
        (!to.HasValue || to > from);
    private static void Future(DateOnly date, DateTimeOffset now)
    { if (date <= RosterSchedule.Today(now)) throw AttendanceOperation.Invalid("policy_future_date_required"); }

    private static AllowancePolicyPage<T> Page<T>(IEnumerable<T> rows, int page, int pageSize)
    {
        if (page is < 1 or > 4096 || pageSize is < 1 or > 100) throw AttendanceOperation.Invalid("invalid_policy_page");
        var items = rows.Skip((page - 1) * pageSize).Take(pageSize + 1).ToArray();
        return new(items.Take(pageSize).ToArray(), page, pageSize, items.Length > pageSize);
    }

    private static void Text(string? text, int maximum)
    { if (string.IsNullOrWhiteSpace(text) || text.Length > maximum || text.Any(char.IsControl)) throw AttendanceOperation.Invalid(); }

    private static AllowancePolicyView Map(Policy policy) => new(policy.Id, policy.Code, policy.Name, policy.Revision, Convert.ToBase64String(policy.RowVersion));

    private static AllowancePolicyVersionView Map(PolicyVersion version) => new(version.Id, version.VersionNumber, version.EffectiveFrom,
        version.EffectiveTo, version.RulesJson, AllowancePolicyResolver.Hash(version.RulesJson), version.PublishedAt,
        version.Authority.ToString(), version.PublishedByUserId);

    private static AllowancePolicyAssignmentView Map(PolicyAssignment assignment, PolicyAssignmentEnd? end)
    { var target = Target(assignment); return new(assignment.Id, assignment.PolicyVersionId, target.Kind, target.Id,
        assignment.EffectiveFrom, end?.EffectiveTo ?? assignment.EffectiveTo, end?.Id, assignment.Reason,
        assignment.Authority.ToString(), assignment.RecordedByUserId, assignment.RecordedAt,
        assignment.EffectiveTo, end?.Reason, end?.RecordedByUserId, end?.RecordedAt); }

    private static (AllowancePolicyTarget Kind, Guid Id) Target(PolicyAssignment assignment) =>
        assignment.EmployeeId is { } employee ? (AllowancePolicyTarget.Employee, employee) :
        assignment.DepartmentId is { } department ? (AllowancePolicyTarget.Department, department) :
        assignment.StaffCategoryId is { } category ? (AllowancePolicyTarget.StaffCategory, category) :
        assignment.SiteId is { } site ? (AllowancePolicyTarget.Site, site) :
        throw AttendanceOperation.Conflict("policy_target_invalid");
}
