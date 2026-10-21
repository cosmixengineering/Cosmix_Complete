using System.Text.Json.Serialization;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.Policies.Advanced;

public sealed record AdvancedPolicyResolution(string State, Guid EmployeeId, AdvancedAttendanceFamily Family,
    DateOnly BusinessDate, Guid? PolicyVersionId, IReadOnlyList<Guid> AssignmentIds, AdvancedAttendanceRules? Rules, string Fingerprint);
public interface IAdvancedAttendancePolicyReader {
    Task<AdvancedPolicyResolution> ReadAsync(Guid employeeId, AdvancedAttendanceFamily family, DateOnly date, CancellationToken ct);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishAdvancedPolicyRequest([property: JsonRequired] AdvancedAttendanceFamily Family,
    [property: JsonRequired] string? ExpectedVersion, [property: JsonRequired] int ExpectedRevision,
    [property: JsonRequired] DateOnly EffectiveFrom, [property: JsonRequired] DateOnly? EffectiveTo,
    [property: JsonRequired] AdvancedAttendanceRules Rules, [property: JsonRequired] string Reason);
public sealed record AdvancedPolicyVersionView(Guid PolicyId, Guid Id, AdvancedAttendanceFamily Family,
    int Number, int PolicyRevision, string Version, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    AdvancedAttendanceRules Rules, string Fingerprint);
public interface IAdvancedAttendancePolicyRepository {
    Task<Policy?> FindAsync(AdvancedAttendanceFamily family, CancellationToken ct);
    Task<IReadOnlyList<PolicyVersion>> VersionsAsync(Guid policyId, CancellationToken ct);
    Task<IReadOnlyList<AttendancePolicyCandidate>> AssignmentsAsync(Guid policyId, CancellationToken ct);
    Task<AttendancePolicySource> SourceAsync(Guid employeeId, AdvancedAttendanceFamily family, DateOnly date, CancellationToken ct);
    Task<bool> TargetExistsAsync(AttendancePolicyTarget target, Guid id, CancellationToken ct);
    Task<bool> HasLockedPeriodAsync(DateOnly from, DateOnly? to, CancellationToken ct);
    void Add(Policy policy);
    void Add(PolicyVersion version);
    void Add(PolicyAssignment assignment);
    void ExpectVersion(Policy policy, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
