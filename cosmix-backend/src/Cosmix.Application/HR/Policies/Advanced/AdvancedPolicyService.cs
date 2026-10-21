using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.Policies.Advanced;

public sealed class AdvancedPolicyService(AttendanceOperation operation, IAdvancedPolicyPublicationRepository repository,
    IAdvancedAttendancePolicyReader reader, IAttendanceSummaryRepository summaries)
{
    public Task<AttendancePolicyView> DetailAsync(AdvancedAttendanceFamily family, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(AttendancePolicyPermissions.Configure, "attendance.advanced-policy.detail", null, context,
            async (_, token) => PolicyView(await Find(family, token)), ct, requireSystemRole: true);

    public Task<AttendancePolicyPage<AdvancedPolicyVersionView>> VersionsAsync(AdvancedAttendanceFamily family,
        AuthRequestContext context, CancellationToken ct, int page = 1, int pageSize = 50) =>
        operation.ReadAsync(AttendancePolicyPermissions.Configure, "attendance.advanced-policy.versions", null, context, async (_, token) => {
            var policy = await Find(family, token);
            return Page((await repository.VersionsAsync(policy.Id, token)).Select(x => VersionView(policy, x, family)), page, pageSize);
        }, ct, requireSystemRole: true);

    public Task<AttendancePolicyPage<AttendancePolicyAssignmentView>> AssignmentsAsync(AdvancedAttendanceFamily family,
        AuthRequestContext context, CancellationToken ct, int page = 1, int pageSize = 50) =>
        operation.ReadAsync(AttendancePolicyPermissions.Configure, "attendance.advanced-policy.assignments", null, context, async (_, token) => {
            var policy = await Find(family, token);
            return Page((await repository.AssignmentsAsync(policy.Id, token)).Select(x => AssignmentView(x.Assignment, x.End)), page, pageSize);
        }, ct, requireSystemRole: true);

    public Task<AdvancedPolicyResolution> EffectiveAsync(Guid employeeId, AdvancedAttendanceFamily family, DateOnly date,
        AuthRequestContext context, CancellationToken ct) => operation.ReadAsync(AttendancePolicyPermissions.ApplicabilityRead,
            "attendance.advanced-policy.effective", employeeId, context, (_, token) => reader.ReadAsync(employeeId, family, date, token), ct,
            resourceScopes: (_, token) => summaries.ResourceScopesAsync(employeeId, date, date, token));

    public Task<AdvancedPolicyVersionView> PublishAsync(PublishAdvancedPolicyRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync(AttendancePolicyPermissions.Publish, "attendance.advanced-policy.publish", null, key, request, request.Reason,
            context, async (actor, token) => {
                Family(request.Family);
                var policy = await repository.FindAsync(request.Family, token);
                if (policy is null) {
                    if (request.ExpectedRevision != 0 || request.ExpectedVersion is not null)
                        throw AttendanceOperation.Conflict("advanced_policy_version_conflict");
                } else CheckCurrent(policy, request.Family, request.ExpectedVersion, request.ExpectedRevision);
                FuturePeriod(request.EffectiveFrom, request.EffectiveTo, actor.Now);
                var validation = AdvancedAttendanceRulesCodec.Validate(request.Rules);
                if (!validation.IsValid || validation.Rules!.Family != request.Family)
                    throw AttendanceOperation.Invalid("advanced_policy_rules_invalid");
                if (!await repository.StaffCategoryCodesExistAsync(validation.Rules!.ApplicableStaffCategoryCodes!, token))
                    throw AttendanceOperation.Invalid("advanced_policy_staff_category_unknown");
                if (await repository.HasLockedPeriodAsync(request.EffectiveFrom, request.EffectiveTo, token))
                    throw AttendanceOperation.Conflict("attendance_period_locked");
                IReadOnlyList<PolicyVersion> versions = policy is null ? [] : await repository.VersionsAsync(policy.Id, token);
                var maximum = versions.Count == 0 ? 0 : versions.Max(x => x.VersionNumber);
                if (maximum == int.MaxValue) throw AttendanceOperation.Conflict("advanced_policy_version_limit");
                var before = policy is null ? null : PolicyView(policy);
                if (policy is null) {
                    policy = new Policy(AdvancedAttendanceRulesCodec.Code(request.Family), "Advanced attendance " + request.Family,
                        PolicyCategory.EmployeeException);
                    repository.Add(policy);
                }
                var version = new PolicyVersion(policy.Id, maximum + 1, request.EffectiveFrom, request.EffectiveTo,
                    AdvancedAttendanceRulesCodec.Schema, validation.CanonicalJson!, null, null,
                    actor.Identity.AccountId.ToString(), actor.Now, PolicyAuthority.DirectHr);
                repository.Add(version); policy.AdvanceRevision(); await repository.SaveChangesAsync(token);
                return new AttendanceChange<AdvancedPolicyVersionView>(version.Id, before, VersionView(policy, version, request.Family));
            }, ct, requireSystemRole: true);

    public Task<AttendancePolicyAssignmentResult> AssignAsync(AdvancedAttendanceFamily family, AssignAttendancePolicyRequest request,
        string key, AuthRequestContext context, CancellationToken ct) => operation.WriteAsync(AttendancePolicyPermissions.Publish,
            "attendance.advanced-policy.assign", null, key, new { family, request }, request.Reason, context, async (actor, token) => {
                var policy = await Current(family, request.ExpectedVersion, request.ExpectedRevision, token);
                await ValidateAssignment(policy, family, request.PolicyVersionId, request.Target, request.TargetId,
                    request.EffectiveFrom, request.EffectiveTo, null, actor.Now, token);
                var assignment = CreateAssignment(request.PolicyVersionId, request.Target, request.TargetId,
                    request.EffectiveFrom, request.EffectiveTo, request.Reason, actor);
                repository.Add(assignment); policy.AdvanceRevision(); await repository.SaveChangesAsync(token);
                return new AttendanceChange<AttendancePolicyAssignmentResult>(assignment.Id, null,
                    new(PolicyView(policy), AssignmentView(assignment, null)));
            }, ct, requireSystemRole: true);

    public Task<AttendancePolicyAssignmentResult> EndAsync(AdvancedAttendanceFamily family, Guid assignmentId,
        EndAttendancePolicyAssignmentRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync(AttendancePolicyPermissions.Publish, "attendance.advanced-policy.end", null, key,
            new { family, assignmentId, request }, request.Reason, context, async (actor, token) => {
                var policy = await Current(family, request.ExpectedVersion, request.ExpectedRevision, token);
                var previous = await FindAssignment(policy, assignmentId, token);
                await ValidateEnd(previous, family, request.EffectiveTo, actor.Now, token);
                var end = new PolicyAssignmentEnd(previous.Assignment, request.EffectiveTo, request.Reason, null,
                    actor.Identity.AccountId.ToString(), actor.Now, PolicyAuthority.DirectHr);
                repository.Add(end); policy.AdvanceRevision(); await repository.SaveChangesAsync(token);
                return new AttendanceChange<AttendancePolicyAssignmentResult>(end.Id, AssignmentView(previous.Assignment, null),
                    new(PolicyView(policy), AssignmentView(previous.Assignment, end)));
            }, ct, requireSystemRole: true);

    public Task<AttendancePolicyAssignmentResult> ReplaceAsync(AdvancedAttendanceFamily family, Guid assignmentId,
        ReplaceAttendancePolicyAssignmentRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync(AttendancePolicyPermissions.Publish, "attendance.advanced-policy.replace", null, key,
            new { family, assignmentId, request }, request.Reason, context, async (actor, token) => {
                var policy = await Current(family, request.ExpectedVersion, request.ExpectedRevision, token);
                var previous = await FindAssignment(policy, assignmentId, token);
                await ValidateEnd(previous, family, request.EffectiveFrom, actor.Now, token);
                var (target, id) = Target(previous.Assignment);
                await ValidateAssignment(policy, family, request.PolicyVersionId, target, id, request.EffectiveFrom,
                    request.EffectiveTo, assignmentId, actor.Now, token);
                var end = new PolicyAssignmentEnd(previous.Assignment, request.EffectiveFrom, request.Reason, null,
                    actor.Identity.AccountId.ToString(), actor.Now, PolicyAuthority.DirectHr);
                var assignment = CreateAssignment(request.PolicyVersionId, target, id, request.EffectiveFrom, request.EffectiveTo, request.Reason, actor);
                repository.Add(end); repository.Add(assignment); policy.AdvanceRevision(); await repository.SaveChangesAsync(token);
                return new AttendanceChange<AttendancePolicyAssignmentResult>(assignment.Id, AssignmentView(previous.Assignment, null),
                    new(PolicyView(policy), AssignmentView(assignment, null)));
            }, ct, requireSystemRole: true);

    private async Task ValidateAssignment(Policy policy, AdvancedAttendanceFamily family, Guid versionId, AttendancePolicyTarget target,
        Guid targetId, DateOnly from, DateOnly? to, Guid? exceptId, DateTimeOffset now, CancellationToken ct)
    {
        if (!Enum.IsDefined(target) || target == AttendancePolicyTarget.Role || targetId == Guid.Empty)
            throw AttendanceOperation.Invalid("invalid_advanced_policy_assignment");
        FuturePeriod(from, to, now);
        if (!await repository.TargetExistsAsync(target, targetId, ct)) throw AttendanceOperation.Missing("policy_target_not_found");
        var version = (await repository.VersionsAsync(policy.Id, ct)).SingleOrDefault(x => x.Id == versionId)
            ?? throw AttendanceOperation.Missing("policy_version_not_found");
        Publishable(version, family);
        if (version.PolicyId != policy.Id || from < version.EffectiveFrom || version.EffectiveTo.HasValue && (!to.HasValue || to > version.EffectiveTo))
            throw AttendanceOperation.Invalid("policy_assignment_outside_version");
        if (await repository.HasLockedPeriodAsync(from, to, ct)) throw AttendanceOperation.Conflict("attendance_period_locked");
        if (await repository.HasTargetOverlapAsync(family, target, targetId, from, to, exceptId, ct))
            throw AttendanceOperation.Conflict("advanced_policy_target_assignment_overlap");
    }

    private async Task ValidateEnd(AttendancePolicyCandidate row, AdvancedAttendanceFamily family, DateOnly to, DateTimeOffset now, CancellationToken ct)
    {
        if (row.End is not null) throw AttendanceOperation.Conflict("policy_assignment_already_ended");
        Publishable(row.Version, family);
        if (!AdvancedPolicyReader.ValidCandidate(row, row.Assignment.EffectiveFrom))
            throw AttendanceOperation.Conflict("advanced_policy_assignment_authority_invalid");
        FuturePeriod(to, null, now);
        if (to <= row.Assignment.EffectiveFrom || row.Assignment.EffectiveTo.HasValue && to >= row.Assignment.EffectiveTo)
            throw AttendanceOperation.Invalid("invalid_policy_assignment_end");
        if (await repository.HasLockedPeriodAsync(to, row.Assignment.EffectiveTo, ct)) throw AttendanceOperation.Conflict("attendance_period_locked");
    }

    private async Task<Policy> Find(AdvancedAttendanceFamily family, CancellationToken ct)
    {
        Family(family);
        var policy = await repository.FindAsync(family, ct) ?? throw AttendanceOperation.Missing("advanced_policy_not_found");
        if (policy.Category != PolicyCategory.EmployeeException || policy.Code != AdvancedAttendanceRulesCodec.Code(family))
            throw AttendanceOperation.Conflict("advanced_policy_identity_invalid");
        return policy;
    }

    private async Task<Policy> Current(AdvancedAttendanceFamily family, string version, int revision, CancellationToken ct)
    {
        var policy = await Find(family, ct); CheckCurrent(policy, family, version, revision); return policy;
    }

    private void CheckCurrent(Policy policy, AdvancedAttendanceFamily family, string? version, int revision)
    {
        if (policy.Category != PolicyCategory.EmployeeException || policy.Code != AdvancedAttendanceRulesCodec.Code(family))
            throw AttendanceOperation.Conflict("advanced_policy_identity_invalid");
        if (!policy.IsActive) throw AttendanceOperation.Conflict("advanced_policy_inactive");
        if (version is null || revision != policy.Revision) throw AttendanceOperation.Conflict("advanced_policy_version_conflict");
        var bytes = SchedulingOperation.Version(version);
        if (!policy.RowVersion.SequenceEqual(bytes)) throw AttendanceOperation.Conflict("advanced_policy_version_conflict");
        repository.ExpectVersion(policy, bytes);
    }

    private async Task<AttendancePolicyCandidate> FindAssignment(Policy policy, Guid id, CancellationToken ct) =>
        (await repository.AssignmentsAsync(policy.Id, ct)).SingleOrDefault(x => x.Assignment.Id == id && x.Version.PolicyId == policy.Id)
        ?? throw AttendanceOperation.Missing("policy_assignment_not_found");

    private static AdvancedAttendanceRules Publishable(PolicyVersion version, AdvancedAttendanceFamily family)
    {
        var validation = AdvancedAttendanceRulesCodec.Parse(version.RulesJson);
        if (version.Authority != PolicyAuthority.DirectHr || version.ApprovalRequestId is not null ||
            version.RulesSchemaVersion != AdvancedAttendanceRulesCodec.Schema || !validation.IsValid || validation.Rules!.Family != family)
            throw AttendanceOperation.Conflict("advanced_policy_version_not_publishable");
        return validation.Rules!;
    }

    private static AdvancedPolicyVersionView VersionView(Policy policy, PolicyVersion version, AdvancedAttendanceFamily family) =>
        new(policy.Id, version.Id, family, version.VersionNumber, policy.Revision, Convert.ToBase64String(policy.RowVersion),
            version.EffectiveFrom, version.EffectiveTo, Publishable(version, family), AdvancedAttendanceRulesCodec.Hash(JsonSerializer.Serialize(new {
                version.Id, version.PolicyId, version.VersionNumber, version.EffectiveFrom, version.EffectiveTo,
                version.RulesSchemaVersion, version.RulesJson, version.PolicyDocumentPrivateReference,
                version.Authority, version.ApprovalRequestId, version.PublishedByUserId, version.PublishedAt
            })));

    private static AttendancePolicyView PolicyView(Policy policy) => new(policy.Id, policy.Code, policy.Name, policy.Revision, Convert.ToBase64String(policy.RowVersion));
    private static AttendancePolicyAssignmentView AssignmentView(PolicyAssignment assignment, PolicyAssignmentEnd? end)
    {
        var (target, id) = Target(assignment);
        return new(assignment.Id, assignment.PolicyVersionId, target, id, assignment.EffectiveFrom, end?.EffectiveTo ?? assignment.EffectiveTo,
            end?.Id, assignment.Reason, assignment.Authority.ToString(), assignment.RecordedByUserId, assignment.RecordedAt,
            assignment.EffectiveTo, end?.Reason, end?.RecordedByUserId, end?.RecordedAt);
    }

    private static (AttendancePolicyTarget Target, Guid Id) Target(PolicyAssignment assignment)
    {
        if (assignment.RoleId is not null || new[] { assignment.EmployeeId, assignment.DepartmentId, assignment.StaffCategoryId, assignment.SiteId }.Count(x => x.HasValue) != 1)
            throw AttendanceOperation.Conflict("advanced_policy_target_invalid");
        return assignment.EmployeeId is { } employee ? (AttendancePolicyTarget.Employee, employee) :
            assignment.DepartmentId is { } department ? (AttendancePolicyTarget.Department, department) :
            assignment.StaffCategoryId is { } category ? (AttendancePolicyTarget.StaffCategory, category) : (AttendancePolicyTarget.Site, assignment.SiteId!.Value);
    }

    private static PolicyAssignment CreateAssignment(Guid versionId, AttendancePolicyTarget target, Guid targetId,
        DateOnly from, DateOnly? to, string reason, AttendanceActor actor) => new(versionId, null,
            target == AttendancePolicyTarget.Employee ? targetId : null, target == AttendancePolicyTarget.Department ? targetId : null,
            target == AttendancePolicyTarget.StaffCategory ? targetId : null, target == AttendancePolicyTarget.Site ? targetId : null,
            from, to, reason, null, null, actor.Identity.AccountId.ToString(), actor.Now, PolicyAuthority.DirectHr);

    private static void Family(AdvancedAttendanceFamily family)
    { if (!Enum.IsDefined(family)) throw AttendanceOperation.Invalid("invalid_advanced_policy_family"); }

    private static void FuturePeriod(DateOnly from, DateOnly? to, DateTimeOffset now)
    {
        if (from == default || from == DateOnly.MaxValue || to.HasValue && to <= from) throw AttendanceOperation.Invalid("invalid_policy_dates");
        if (from <= RosterSchedule.Today(now)) throw AttendanceOperation.Invalid("policy_future_date_required");
    }

    private static AttendancePolicyPage<T> Page<T>(IEnumerable<T> items, int page, int size)
    {
        if (page is < 1 or > 4096 || size is < 1 or > 100) throw AttendanceOperation.Invalid("invalid_policy_page");
        var rows = items.Skip((page - 1) * size).Take(size + 1).ToArray();
        return new(rows.Take(size).ToArray(), page, size, rows.Length > size);
    }
}
