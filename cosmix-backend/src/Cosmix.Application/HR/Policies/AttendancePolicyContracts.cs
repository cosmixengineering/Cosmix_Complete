using System.Text.Json.Serialization;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.Policies;

public interface IAttendancePolicyResolver
{
    Task<AttendancePolicyResolution> ResolveAsync(Guid employeeId, DateOnly businessDate, CancellationToken ct);
}

public sealed record AttendancePolicyResolution(string State, Guid? PolicyVersionId,
    IReadOnlyList<Guid> AssignmentIds, AttendancePolicyRules? Rules, string? CanonicalJson, string Fingerprint);
[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendancePolicyTarget>))]
public enum AttendancePolicyTarget { Employee = 1, Department = 2, StaffCategory = 3, Site = 4, Role = 5 }
public sealed record AttendancePolicyMembership(Guid Id, Guid EmployeeId, Guid DepartmentId, Guid StaffCategoryId,
    Guid? SiteId, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record AttendancePolicyCandidate
{
    public AttendancePolicyCandidate() { }
    public AttendancePolicyCandidate(PolicyAssignment assignment, PolicyVersion version, PolicyAssignmentEnd? end, bool authorityValid)
    { Assignment = assignment; Version = version; End = end; AuthorityValid = authorityValid; }
    public PolicyAssignment Assignment { get; init; } = null!;
    public PolicyVersion Version { get; init; } = null!;
    public PolicyAssignmentEnd? End { get; init; }
    public bool AuthorityValid { get; init; }
}
public sealed record AttendancePolicySource(IReadOnlyList<AttendancePolicyMembership> Memberships,
    IReadOnlyList<AttendancePolicyCandidate> Candidates);
public sealed record AttendancePolicyView(Guid Id, string Code, string Name, int Revision, string Version);
public sealed record AttendancePolicyPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, bool HasMore);
public sealed record AttendancePolicyVersionView(Guid Id, int Number, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    string CanonicalJson, string ConfigurationSha256, DateTimeOffset PublishedAt, string Authority, string PublishedByUserId);
public sealed record AttendancePolicyAssignmentView(Guid Id, Guid PolicyVersionId, AttendancePolicyTarget Target,
    Guid TargetId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, Guid? EndId, string Reason,
    string Authority, string RecordedByUserId, DateTimeOffset RecordedAt, DateOnly? OriginalEffectiveTo,
    string? EndingReason, string? EndedByUserId, DateTimeOffset? EndRecordedAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateAttendancePolicyRequest([property: JsonRequired] string Code,
    [property: JsonRequired] string Name, [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewAttendancePolicyRequest([property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] DateOnly EffectiveFrom,
    [property: JsonRequired] DateOnly? EffectiveTo, [property: JsonRequired] AttendancePolicyRules? Rules);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishAttendancePolicyRequest([property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] DateOnly EffectiveFrom,
    [property: JsonRequired] DateOnly? EffectiveTo, [property: JsonRequired] AttendancePolicyRules? Rules,
    [property: JsonRequired] string ExpectedPreviewFingerprint, [property: JsonRequired] string Reason);
public sealed record AttendancePolicyPublicationPreview(bool CanPublish, int Revision, string Version,
    string? CanonicalJson, string? ConfigurationSha256, string PreviewFingerprint,
    IReadOnlyList<AttendancePolicyRuleError> Errors, bool AffectsBaseSalary = false);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssignAttendancePolicyRequest([property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] Guid PolicyVersionId,
    [property: JsonRequired] AttendancePolicyTarget Target, [property: JsonRequired] Guid TargetId,
    [property: JsonRequired] DateOnly EffectiveFrom, [property: JsonRequired] DateOnly? EffectiveTo,
    [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EndAttendancePolicyAssignmentRequest([property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] DateOnly EffectiveTo,
    [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReplaceAttendancePolicyAssignmentRequest([property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] Guid PolicyVersionId,
    [property: JsonRequired] DateOnly EffectiveFrom, [property: JsonRequired] DateOnly? EffectiveTo,
    [property: JsonRequired] string Reason);
public sealed record AttendancePolicyPublicationResult(AttendancePolicyView Policy, AttendancePolicyVersionView Published);
public sealed record AttendancePolicyAssignmentResult(AttendancePolicyView Policy, AttendancePolicyAssignmentView Assignment);

public interface IAttendancePolicyRepository
{
    Task<IReadOnlyList<Policy>> ListAsync(CancellationToken ct);
    Task<Policy?> FindAsync(Guid id, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<IReadOnlyList<PolicyVersion>> VersionsAsync(Guid policyId, CancellationToken ct);
    Task<IReadOnlyList<AttendancePolicyCandidate>> AssignmentsAsync(Guid policyId, CancellationToken ct);
    Task<AttendancePolicySource> SourceAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<bool> TargetExistsAsync(AttendancePolicyTarget target, Guid id, CancellationToken ct);
    Task<bool> HasLockedPeriodAsync(DateOnly from, DateOnly? to, CancellationToken ct);
    Task<bool> HasTargetOverlapAsync(AttendancePolicyTarget target, Guid targetId, DateOnly from,
        DateOnly? to, Guid? exceptAssignmentId, CancellationToken ct);
    void Add(Policy policy);
    void Add(PolicyVersion version);
    void Add(PolicyAssignment assignment);
    void Add(PolicyAssignmentEnd end);
    void ExpectVersion(Policy policy, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
