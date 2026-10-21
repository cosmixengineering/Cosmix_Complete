using System.Text.Json.Serialization;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.Allowances;

public interface IAllowancePolicyResolver
{
    Task<AllowancePolicyResolution> ResolveAsync(Guid employeeId, DateOnly businessDate, CancellationToken ct);
}

/// <summary>Dated allowance applicability. Non-effective states never yield a payable amount.</summary>
public sealed record AllowancePolicyResolution(string State, Guid? PolicyVersionId,
    IReadOnlyList<Guid> AssignmentIds, AllowancePolicyRules? Rules, string? CanonicalJson, string Fingerprint);

public enum AllowancePolicyTarget { Employee = 1, Department = 2, StaffCategory = 3, Site = 4 }

public sealed record AllowancePolicyMembership(Guid Id, Guid EmployeeId, Guid DepartmentId, Guid StaffCategoryId,
    Guid? SiteId, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public sealed record AllowancePolicyCandidate
{
    public AllowancePolicyCandidate() { }
    public AllowancePolicyCandidate(PolicyAssignment assignment, PolicyVersion version, PolicyAssignmentEnd? end, bool authorityValid)
    { Assignment = assignment; Version = version; End = end; AuthorityValid = authorityValid; }
    public PolicyAssignment Assignment { get; init; } = null!;
    public PolicyVersion Version { get; init; } = null!;
    public PolicyAssignmentEnd? End { get; init; }
    public bool AuthorityValid { get; init; }
}

public sealed record AllowancePolicySource(IReadOnlyList<AllowancePolicyMembership> Memberships,
    IReadOnlyList<AllowancePolicyCandidate> Candidates);

public sealed record AllowancePolicyView(Guid Id, string Code, string Name, int Revision, string Version);
public sealed record AllowancePolicyPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, bool HasMore);
public sealed record AllowancePolicyVersionView(Guid Id, int Number, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    string CanonicalJson, string ConfigurationSha256, DateTimeOffset PublishedAt, string Authority, string PublishedByUserId);
public sealed record AllowancePolicyAssignmentView(Guid Id, Guid PolicyVersionId, AllowancePolicyTarget Target,
    Guid TargetId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, Guid? EndId, string Reason,
    string Authority, string RecordedByUserId, DateTimeOffset RecordedAt, DateOnly? OriginalEffectiveTo,
    string? EndingReason, string? EndedByUserId, DateTimeOffset? EndRecordedAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateAllowancePolicyRequest([property: JsonRequired] string Code,
    [property: JsonRequired] string Name, [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewAllowancePolicyRequest([property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] DateOnly EffectiveFrom,
    [property: JsonRequired] DateOnly? EffectiveTo, [property: JsonRequired] AllowancePolicyRules? Rules);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishAllowancePolicyRequest([property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] DateOnly EffectiveFrom,
    [property: JsonRequired] DateOnly? EffectiveTo, [property: JsonRequired] AllowancePolicyRules? Rules,
    [property: JsonRequired] string ExpectedPreviewFingerprint, [property: JsonRequired] string Reason);
public sealed record AllowancePolicyPublicationPreview(bool CanPublish, int Revision, string Version,
    string? CanonicalJson, string? ConfigurationSha256, string PreviewFingerprint,
    IReadOnlyList<AllowancePolicyRuleError> Errors);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssignAllowancePolicyRequest([property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] Guid PolicyVersionId,
    [property: JsonRequired] AllowancePolicyTarget Target, [property: JsonRequired] Guid TargetId,
    [property: JsonRequired] DateOnly EffectiveFrom, [property: JsonRequired] DateOnly? EffectiveTo,
    [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EndAllowancePolicyAssignmentRequest([property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] DateOnly EffectiveTo,
    [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReplaceAllowancePolicyAssignmentRequest([property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] Guid PolicyVersionId,
    [property: JsonRequired] DateOnly EffectiveFrom, [property: JsonRequired] DateOnly? EffectiveTo,
    [property: JsonRequired] string Reason);
public sealed record AllowancePolicyPublicationResult(AllowancePolicyView Policy, AllowancePolicyVersionView Published);
public sealed record AllowancePolicyAssignmentResult(AllowancePolicyView Policy, AllowancePolicyAssignmentView Assignment);

public interface IAllowancePolicyRepository
{
    Task<IReadOnlyList<Policy>> ListAsync(CancellationToken ct);
    Task<Policy?> FindAsync(Guid id, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<IReadOnlyList<PolicyVersion>> VersionsAsync(Guid policyId, CancellationToken ct);
    Task<IReadOnlyList<AllowancePolicyCandidate>> AssignmentsAsync(Guid policyId, CancellationToken ct);
    Task<AllowancePolicySource> SourceAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<bool> TargetExistsAsync(AllowancePolicyTarget target, Guid id, CancellationToken ct);
    Task<bool> HasTargetOverlapAsync(AllowancePolicyTarget target, Guid targetId, DateOnly from,
        DateOnly? to, Guid? exceptAssignmentId, CancellationToken ct);
    void Add(Policy policy);
    void Add(PolicyVersion version);
    void Add(PolicyAssignment assignment);
    void Add(PolicyAssignmentEnd end);
    void ExpectVersion(Policy policy, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
