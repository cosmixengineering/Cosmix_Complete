using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Application.Authentication.Bootstrap;

public sealed record BootstrapTarget(string Server, string Database)
{
    public string Confirmation => $"{Server}/{Database}";
}

public sealed record BootstrapRequest(EmployeeDraftDetails Employee, string LoginPhone,
    string OwnerLegalName, string OwnerIdentityDocumentNumber, string OwnerApprovalReference,
    DateTimeOffset OwnerApprovedAt, bool AuthorizesSelfInitialization, bool AuthorizesFixedRoleGrants,
    byte[] OwnerApprovalEvidence)
{
    public override string ToString() => nameof(BootstrapRequest);
}

public sealed record BootstrapSnapshot(string StorageKey, string ObjectVersion, string Hash, string KeyReference, int SchemaVersion);
public sealed record BootstrapSnapshotPayload(Guid EmployeeId, BootstrapTarget Target, BootstrapRequest Request,
    string LocalExecutor, DateTimeOffset RecordedAt, IReadOnlyList<string> GrantedPermissions)
{
    public override string ToString() => nameof(BootstrapSnapshotPayload);
}
public sealed record BootstrapResult(Guid EmployeeId, Guid AccountId, string SnapshotReference);
public sealed record BootstrapAccess(IReadOnlyList<Role> Roles, IReadOnlyList<Permission> Permissions,
    IReadOnlyList<RolePermission> Grants, UserRoleAssignment Assignment, UserAccessScope Scope);
public sealed record BootstrapPolicy(int MinimumPasswordLength, string BusinessTimeZoneId);

public interface IBootstrapRepository
{
    Task<BootstrapTarget> GetTargetAsync(CancellationToken ct);
    Task<T> InEmptyDatabaseTransactionAsync<T>(BootstrapTarget expected, Func<CancellationToken, Task<T>> action, CancellationToken ct);
    void AddEmployee(Employee employee);
    void AddSubmission(EmployeeSubmission submission);
    void AddDecision(EmployeeOnboardingDecision decision);
    void AddAccount(UserAccount account);
    void AddAccess(BootstrapAccess access);
    void AddBinding(LoginPhoneBinding binding);
    void AddAudit(AuditEvent audit);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IBootstrapSnapshotStore
{
    Task<BootstrapSnapshot> WriteAsync(BootstrapSnapshotPayload payload, CancellationToken ct);
    Task<bool> TryDeleteAsync(BootstrapSnapshot snapshot);
}

/// <summary>Only a confirmed rollback permits deleting the associated external snapshot.</summary>
public sealed class BootstrapTransactionException(bool rollbackConfirmed, Exception inner)
    : Exception("Bootstrap transaction did not report a successful commit.", inner)
{
    public bool RollbackConfirmed { get; } = rollbackConfirmed;
}

public sealed class BootstrapFailedException(string? retainedSnapshotReference, bool commitUncertain)
    : Exception("Bootstrap failed. Inspect the target before retrying.")
{
    public string? RetainedSnapshotReference { get; } = retainedSnapshotReference;
    public bool CommitUncertain { get; } = commitUncertain;
}
