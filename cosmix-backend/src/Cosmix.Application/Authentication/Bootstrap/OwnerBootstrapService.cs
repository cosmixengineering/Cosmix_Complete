using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Application.Authentication.Web;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.Authentication.Bootstrap;

/// <summary>Invoked by the local owner command only. No authenticated HTTP workflow delegates to this service.</summary>
public sealed class OwnerBootstrapService(IBootstrapRepository repository, IBootstrapSnapshotStore snapshots,
    IWebPasswordHasher passwords, IPhoneNumberNormalizer phones, BootstrapPolicy policy, TimeProvider clock)
{
    // Deliberately enumerated: later catalog additions must not silently expand fixed-role authority.
    public static IReadOnlyList<string> FixedPermissionCodes { get; } = Array.AsReadOnly(new[]
    {
        "hr.organization.read", "hr.organization.manage", "hr.holidays.read", "hr.holidays.manage",
        "hr.sites.read", "hr.sites.manage",
        "hr.employees.read", "hr.employees.drafts.manage", "hr.employees.submit", "hr.employees.submissions.review",
        "hr.employees.financial.read", "hr.employees.financial.manage", "hr.employees.medical.read", "hr.employees.medical.manage",
        "hr.employees.documents.read", "hr.employees.documents.manage", "hr.employees.documents.verify",
        "hr.employees.lifecycle.read", "hr.employees.lifecycle.request", "hr.employees.lifecycle.review",
        "hr.employees.compensation.read", "hr.employees.compensation.request", "hr.employees.compensation.review",
        "hr.employees.basic.manage", "hr.employees.changes.request", "hr.employees.changes.review",
        "hr.employees.identity.read", "hr.employees.identity.request", "hr.employees.bank.read", "hr.employees.bank.request",
        "auth.self.read", "auth.self.sessions.manage", "access.roles.read",
        "access.roles.create", "access.roles.permissions.manage", "access.accounts.read",
        "access.accounts.assign", "access.accounts.mobile.manage", "access.accounts.web.manage"
    });

    public async Task<BootstrapResult> InitializeAsync(BootstrapRequest request, string password,
        BootstrapTarget expectedTarget, string localExecutor, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        Validate(request, password, localExecutor, now);
        var employee = new Employee(now, localExecutor);
        employee.UpdateDraft(request.Employee);
        employee.SelectLoginPhone(request.LoginPhone, false);
        BootstrapSnapshot? snapshot = null;
        try
        {
            return await repository.InEmptyDatabaseTransactionAsync(expectedTarget, async token =>
            {
                snapshot = await snapshots.WriteAsync(new(employee.Id, expectedTarget, request, localExecutor, now, FixedPermissionCodes), token);
                repository.AddEmployee(employee);
                await repository.SaveChangesAsync(token);

                var submission = employee.SubmitForOwnerBootstrap(localExecutor, now, snapshot.StorageKey,
                    snapshot.ObjectVersion, snapshot.Hash, snapshot.KeyReference, snapshot.SchemaVersion);
                repository.AddSubmission(submission);
                await repository.SaveChangesAsync(token);

                var approval = employee.ApproveOwnerBootstrap(submission, request.OwnerLegalName.Trim(), request.OwnerApprovalReference.Trim(), now);
                repository.AddDecision(approval);
                await repository.SaveChangesAsync(token);

                var account = new UserAccount(employee, approval, request.Employee.OfficialEmail!.Trim(), now);
                repository.AddAccount(account);
                await repository.SaveChangesAsync(token);

                // The real owner explicitly authorizes self-initialization; the local executor is recorded separately.
                var superAdmin = Role.CreateSystem(SystemRoleKind.SuperAdmin, account.Id, now);
                var admin = Role.CreateSystem(SystemRoleKind.Admin, account.Id, now);
                var permissions = FixedPermissionCodes.Select(code =>
                {
                    var definition = AccessPermissionCatalog.Require(code);
                    return new Permission(definition.Code, definition.Description, definition.Sensitive);
                }).ToArray();
                var grants = new[] { superAdmin, admin }.SelectMany(role => permissions.Select(p => new RolePermission(role, p, account.Id, now))).ToArray();
                var assignment = new UserRoleAssignment(account, superAdmin, account.Id, now, null);
                var scope = new UserAccessScope(assignment, AccessScopeKind.All);
                repository.AddAccess(new([superAdmin, admin], permissions, grants, assignment, scope));
                await repository.SaveChangesAsync(token);

                account.ConfigureInitialAccess(assignment, scope, now);
                var binding = new LoginPhoneBinding(account, request.LoginPhone, submission.Id, now);
                account.ProvisionInitialWebCredentials(request.Employee.OfficialEmail!.Trim(), passwords.Hash(account, password),
                    WebPasswordFormats.IdentityV3, binding, assignment, scope, now);
                account.RequireMfa();
                repository.AddBinding(binding);
                repository.AddAudit(new AuditEvent("account.owner.bootstrap", localExecutor, "owner-bootstrap",
                    "UserAccount", account.Id.ToString(), now, Guid.NewGuid().ToString("N"), employee.Id,
                    protectedChangesReference: snapshot.StorageKey));
                if (!account.WebAccessEnabled || account.MobileAccessEnabled || !account.MfaRequired || binding.VerifiedAt.HasValue ||
                    account.PhoneVerifiedAt.HasValue || account.Status != AccountStatus.Active)
                    throw new InvalidOperationException("Bootstrap channel invariants failed.");
                await repository.SaveChangesAsync(token);
                return new BootstrapResult(employee.Id, account.Id, snapshot.StorageKey);
            }, ct);
        }
        catch (BootstrapTransactionException error)
        {
            var retained = snapshot?.StorageKey ?? (error.InnerException as BootstrapFailedException)?.RetainedSnapshotReference;
            if (snapshot is not null && error.RollbackConfirmed && await snapshots.TryDeleteAsync(snapshot)) retained = null;
            throw new BootstrapFailedException(retained, !error.RollbackConfirmed);
        }
    }

    private void Validate(BootstrapRequest request, string password, string executor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Employee);
        if (policy.MinimumPasswordLength is < 12 or > 128)
            throw new InvalidOperationException("Configure WebAuthentication:MinimumPasswordLength between 12 and 128.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < policy.MinimumPasswordLength || password.Length > 1024)
            throw new ArgumentException("Password does not satisfy the configured policy.");
        if (string.IsNullOrWhiteSpace(executor) || executor.Length > 200 ||
            !request.AuthorizesSelfInitialization || !request.AuthorizesFixedRoleGrants ||
            string.IsNullOrWhiteSpace(request.OwnerLegalName) || request.OwnerLegalName.Trim().Length > 200 ||
            !string.Equals(request.OwnerLegalName.Trim(), request.Employee.FullLegalName?.Trim(), StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.OwnerIdentityDocumentNumber) ||
            !string.Equals(request.OwnerIdentityDocumentNumber.Trim(), request.Employee.IdentityDocumentNumber?.Trim(), StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.OwnerApprovalReference) || request.OwnerApprovalReference.Trim().Length > 500 ||
            request.OwnerApprovedAt == default || request.OwnerApprovedAt > now ||
            request.OwnerApprovalEvidence is not { Length: > 0 and <= 1048576 } ||
            WebLoginEmail.Normalize(request.Employee.OfficialEmail) is null)
            throw new ArgumentException("Real owner identity, retained authorization evidence and explicit self-initialization grants are required.");
        if (string.IsNullOrWhiteSpace(policy.BusinessTimeZoneId))
            throw new InvalidOperationException("Configure MobileAuthentication:BusinessTimeZoneId.");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(policy.BusinessTimeZoneId)).DateTime);
        if (!request.Employee.JoinedOn.HasValue || request.Employee.JoinedOn > today ||
            string.IsNullOrWhiteSpace(request.Employee.FullLegalName) || !request.Employee.IdentityDocumentType.HasValue ||
            string.IsNullOrWhiteSpace(request.Employee.IdentityDocumentNumber) ||
            (string.IsNullOrWhiteSpace(request.Employee.PersonalPhone) && string.IsNullOrWhiteSpace(request.Employee.CompanyPhone)))
            throw new ArgumentException("An existing real employee identity, contact and joining date are required.");
        if (phones.Normalize(request.LoginPhone) != request.LoginPhone)
            throw new ArgumentException("Supply the approved login phone in canonical international form.");
    }
}
