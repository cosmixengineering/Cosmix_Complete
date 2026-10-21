using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.Allowances;

namespace Cosmix.Application.HR.Allowances;

/// <summary>Dated, explicit allowance applicability. Missing or ambiguous rules produce no payable consequence.</summary>
public sealed class AllowancePolicyResolver(IAllowancePolicyRepository repository) : IAllowancePolicyResolver
{
    public async Task<AllowancePolicyResolution> ResolveAsync(Guid employeeId, DateOnly businessDate, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || businessDate == default || businessDate == DateOnly.MaxValue)
            throw AttendanceOperation.Invalid();
        return Resolve(employeeId, businessDate, await repository.SourceAsync(employeeId, businessDate, ct));
    }

    public static string SchemaVersion => $"allowance.v{AllowancePolicyRulesCodec.SupportedSchemaVersion}";

    public static AllowancePolicyResolution Resolve(Guid employeeId, DateOnly date, AllowancePolicySource source)
    {
        var memberships = source.Memberships.Where(x => x.EmployeeId == employeeId && x.EffectiveFrom <= date &&
            (x.EffectiveTo is null || x.EffectiveTo > date)).OrderBy(x => x.Id).ToArray();
        var candidates = source.Candidates.Where(x => x.Assignment.EffectiveFrom <= date &&
            (x.Assignment.EffectiveTo is null || x.Assignment.EffectiveTo > date) &&
            (x.End is null || x.End.EffectiveTo > date || !x.AuthorityValid)).OrderBy(x => x.Assignment.Id).ToArray();
        var ids = candidates.Select(x => x.Assignment.Id).ToArray();
        var sourceHash = Hash(JsonSerializer.Serialize(new { employeeId, date, memberships,
            candidates = candidates.Select(x => new { x.Assignment.Id, x.Assignment.PolicyVersionId,
                AssignmentAuthority = x.Assignment.Authority, x.Assignment.ApprovalRequestId,
                x.Assignment.EmployeeId, x.Assignment.DepartmentId, x.Assignment.StaffCategoryId, x.Assignment.SiteId,
                x.Assignment.RoleId, x.Assignment.EffectiveFrom, x.Assignment.EffectiveTo,
                x.Assignment.RecordedAt, x.Assignment.RecordedByUserId,
                EndId = x.End?.Id, EndDate = x.End?.EffectiveTo, EndAuthority = x.End?.Authority,
                EndApproval = x.End?.ApprovalRequestId, EndActor = x.End?.RecordedByUserId, EndRecordedAt = x.End?.RecordedAt,
                VersionId = x.Version.Id, VersionFrom = x.Version.EffectiveFrom, VersionTo = x.Version.EffectiveTo,
                x.Version.PublishedAt, x.Version.PublishedByUserId,
                x.Version.RulesSchemaVersion, x.Version.RulesJson, VersionAuthority = x.Version.Authority, x.AuthorityValid }) }));
        AllowancePolicyResolution Failure(string state) => new(state, null, ids, null, null, Hash(state + ":" + sourceHash));
        if (memberships.Length != 1) return Failure("historical_membership_unavailable");
        if (candidates.Length == 0) return Failure("missing");
        var member = memberships[0];
        if (candidates.Any(x => !x.AuthorityValid || x.Assignment.PolicyVersionId != x.Version.Id || x.Assignment.RoleId.HasValue ||
                x.Version.EffectiveFrom > date || x.Version.EffectiveTo <= date ||
                x.End is not null && (x.End.PolicyAssignmentId != x.Assignment.Id || x.End.EffectiveTo <= x.Assignment.EffectiveFrom) ||
                !(x.Assignment.EmployeeId == employeeId || x.Assignment.DepartmentId == member.DepartmentId ||
                    x.Assignment.StaffCategoryId == member.StaffCategoryId ||
                    x.Assignment.SiteId.HasValue && x.Assignment.SiteId == member.SiteId)))
            return Failure("invalid_configuration");
        var rules = candidates.Select(x => AllowancePolicyRulesCodec.ValidateAndCanonicalize(x.Version.RulesJson)).ToArray();
        if (rules.Any(x => !x.IsValid) || candidates.Where((x, i) =>
                x.Version.RulesSchemaVersion != $"allowance.v{rules[i].Rules!.SchemaVersion}").Any())
            return Failure("invalid_configuration");
        if (rules.Select(x => x.CanonicalJson).Distinct(StringComparer.Ordinal).Count() != 1) return Failure("conflict");
        // Equivalent rules have no competing interpretation. Retain every assignment in the digest.
        var selected = candidates.OrderBy(x => x.Version.Id).First();
        return new("effective", selected.Version.Id, ids, rules[0].Rules, rules[0].CanonicalJson,
            Hash("effective:" + sourceHash + ":" + rules[0].CanonicalJson));
    }

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
