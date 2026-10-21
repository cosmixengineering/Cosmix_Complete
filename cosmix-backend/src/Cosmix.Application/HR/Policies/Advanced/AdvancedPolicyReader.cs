using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.Policies.Advanced;

public sealed class AdvancedPolicyReader(IAdvancedPolicyPublicationRepository repository) : IAdvancedAttendancePolicyReader
{
    public Task<AdvancedPolicyResolution> ReadAsync(Guid employeeId, AdvancedAttendanceFamily family, DateOnly date, CancellationToken ct)
    {
        ValidateSubject(employeeId, date);
        return Enum.IsDefined(family) ? ReadSourceAsync(employeeId, family, date, ct) :
            Task.FromResult(Resolve(employeeId, family, date, new(null, new([], []), [])));
    }

    private async Task<AdvancedPolicyResolution> ReadSourceAsync(Guid employeeId, AdvancedAttendanceFamily family, DateOnly date, CancellationToken ct) =>
        Resolve(employeeId, family, date, await repository.DatedSourceAsync(employeeId, family, date, ct));

    public static AdvancedPolicyResolution Resolve(Guid employeeId, AdvancedAttendanceFamily family, DateOnly date, AdvancedPolicyDatedSource dated)
    {
        ValidateSubject(employeeId, date);
        var memberships = dated.Source.Memberships.Where(x => x.EmployeeId == employeeId && x.EffectiveFrom <= date &&
            (x.EffectiveTo is null || x.EffectiveTo > date)).OrderBy(x => x.Id).ToArray();
        var codes = dated.StaffCategories.Where(x => memberships.Any(m => m.Id == x.MembershipId))
            .OrderBy(x => x.MembershipId).ThenBy(x => x.Code, StringComparer.Ordinal).ToArray();
        var candidates = dated.Source.Candidates.Where(x => x.Assignment.EffectiveFrom <= date &&
            (x.Assignment.EffectiveTo is null || x.Assignment.EffectiveTo > date) &&
            (x.End is null || x.End.EffectiveTo > date || !ValidAuthority(x) || !ValidEnd(x)) &&
            (x.Assignment.EmployeeId == employeeId || memberships.Any(m =>
                x.Assignment.DepartmentId == m.DepartmentId || x.Assignment.StaffCategoryId == m.StaffCategoryId ||
                x.Assignment.SiteId.HasValue && x.Assignment.SiteId == m.SiteId))).OrderBy(x => x.Assignment.Id).ToArray();
        var ids = candidates.Select(x => x.Assignment.Id).ToArray();
        var sourceHash = AdvancedAttendanceRulesCodec.Hash(JsonSerializer.Serialize(new {
            employeeId, family, date, memberships, codes,
            Policy = dated.Policy is { } policy ? new { policy.Id, policy.Code, policy.Category, policy.IsActive } : null,
            Candidates = candidates.Select(x => new {
                Assignment = new { x.Assignment.Id, x.Assignment.PolicyVersionId, x.Assignment.Authority, x.Assignment.ApprovalRequestId,
                    x.Assignment.RoleId, x.Assignment.EmployeeId, x.Assignment.DepartmentId, x.Assignment.StaffCategoryId, x.Assignment.SiteId,
                    x.Assignment.EffectiveFrom, x.Assignment.EffectiveTo, x.Assignment.Reason, x.Assignment.InstructionPrivateReference,
                    x.Assignment.RecordedByUserId, x.Assignment.RecordedAt },
                Version = new { x.Version.Id, x.Version.PolicyId, x.Version.VersionNumber, x.Version.EffectiveFrom, x.Version.EffectiveTo,
                    x.Version.RulesSchemaVersion, x.Version.RulesJson, x.Version.PolicyDocumentPrivateReference,
                    x.Version.Authority, x.Version.ApprovalRequestId, x.Version.PublishedByUserId, x.Version.PublishedAt },
                End = x.End is { } end ? new { end.Id, end.PolicyAssignmentId, end.EffectiveTo, end.Reason,
                    end.Authority, end.ApprovalRequestId, end.RecordedByUserId, end.RecordedAt } : null,
                x.AuthorityValid
            })
        }));
        AdvancedPolicyResolution Result(string state, PolicyVersion? selected = null, AdvancedAttendanceRules? rules = null) =>
            new(state, employeeId, family, date, selected?.Id, ids, rules, AdvancedAttendanceRulesCodec.Hash(state + ":" + sourceHash));
        if (!Enum.IsDefined(family)) return Result("invalid_family");
        if (memberships.Length != 1 || memberships[0].StaffCategoryId == Guid.Empty || memberships[0].DepartmentId == Guid.Empty)
            return Result("historical_membership_unavailable");
        if (codes.Length != 1 || codes[0].StaffCategoryId != memberships[0].StaffCategoryId || string.IsNullOrWhiteSpace(codes[0].Code))
            return Result("historical_staff_category_unavailable");
        if (candidates.Length == 0) return Result("missing");
        if (dated.Policy is not { IsActive: true, Category: PolicyCategory.EmployeeException } controlled ||
            controlled.Code != AdvancedAttendanceRulesCodec.Code(family) || ids.Distinct().Count() != ids.Length ||
            candidates.Any(x => x.Version.PolicyId != controlled.Id || !ValidCandidate(x, date))) return Result("invalid_configuration");
        var validations = candidates.Select(x => AdvancedAttendanceRulesCodec.Parse(x.Version.RulesJson)).ToArray();
        if (validations.Any(x => !x.IsValid || x.Rules!.Family != family)) return Result("invalid_configuration");
        if (validations.Select(x => x.CanonicalJson).Distinct(StringComparer.Ordinal).Count() != 1) return Result("conflict");
        if (!validations[0].Rules!.ApplicableStaffCategoryCodes!.Contains(codes[0].Code, StringComparer.Ordinal))
            return Result("staff_category_not_applicable");
        var selected = candidates.OrderBy(x => x.Version.Id).First();
        return Result("configured", selected.Version, AdvancedAttendanceRulesCodec.Parse(selected.Version.RulesJson).Rules);
    }

    internal static bool ValidAuthority(AttendancePolicyCandidate row) => row.AuthorityValid &&
        row.Assignment.Authority == PolicyAuthority.DirectHr && row.Assignment.ApprovalRequestId is null &&
        row.Version.Authority == PolicyAuthority.DirectHr && row.Version.ApprovalRequestId is null &&
        (row.End is null || row.End.Authority == PolicyAuthority.DirectHr && row.End.ApprovalRequestId is null);

    internal static bool ValidEnd(AttendancePolicyCandidate row) => row.End is null ||
        row.End.PolicyAssignmentId == row.Assignment.Id && row.End.EffectiveTo > row.Assignment.EffectiveFrom &&
        (row.Assignment.EffectiveTo is null || row.End.EffectiveTo <= row.Assignment.EffectiveTo);

    internal static bool ValidCandidate(AttendancePolicyCandidate row, DateOnly date) => ValidAuthority(row) && ValidEnd(row) &&
        row.Assignment.Id != Guid.Empty && row.Assignment.PolicyVersionId == row.Version.Id && row.Version.Id != Guid.Empty &&
        row.Version.RulesSchemaVersion == AdvancedAttendanceRulesCodec.Schema && row.Version.VersionNumber > 0 &&
        row.Assignment.RoleId is null &&
        new[] { row.Assignment.EmployeeId, row.Assignment.DepartmentId, row.Assignment.StaffCategoryId, row.Assignment.SiteId }.Count(x => x.HasValue) == 1 &&
        new[] { row.Assignment.EmployeeId, row.Assignment.DepartmentId, row.Assignment.StaffCategoryId, row.Assignment.SiteId }.All(x => x != Guid.Empty) &&
        row.Version.EffectiveFrom <= date && (row.Version.EffectiveTo is null || row.Version.EffectiveTo > date) &&
        row.Assignment.EffectiveFrom >= row.Version.EffectiveFrom &&
        (row.Version.EffectiveTo is null || row.Assignment.EffectiveTo.HasValue && row.Assignment.EffectiveTo <= row.Version.EffectiveTo);

    private static void ValidateSubject(Guid employeeId, DateOnly date)
    {
        if (employeeId == Guid.Empty || date == default || date == DateOnly.MaxValue) throw AttendanceOperation.Invalid("invalid_advanced_policy_subject");
    }
}
