using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Application.Authentication;

public sealed record ApprovedEmployeeIdentity(Employee Employee, EmployeeOnboardingDecision Approval);
public sealed record MobileProvisioningResult(Guid AccountId, AccountStatus Status, bool AccessAssignmentRequired);

public interface IEmployeeMobileProvisioner
{
    Task<MobileProvisioningResult> ProvisionAsync(Guid employeeId, AuthRequestContext context, CancellationToken cancellationToken);
}
public interface IEmployeeMobileProvisioningRepository
{
    Task<ApprovedEmployeeIdentity?> GetApprovedEmployeeAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<UserAccount?> FindAccountAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LoginPhoneBinding>> GetBindingsAsync(Guid accountId, CancellationToken cancellationToken);
    Task<bool> IsPhoneBoundAsync(string phone, CancellationToken cancellationToken);
    void AddAccount(UserAccount account);
    void AddBinding(LoginPhoneBinding binding);
}

/// <summary>Internal HR handoff, not public signup. Call after the real approval transaction commits; retry is idempotent.</summary>
public sealed class EmployeeMobileProvisioningService(IAuthenticationRepository transaction,
    IEmployeeMobileProvisioningRepository repository, IAccessManagementRepository administrators,
    IAccountAccessReader permissions, ICurrentAuthentication current, IPhoneNumberNormalizer phones, TimeProvider clock)
    : IEmployeeMobileProvisioner
{
    public Task<MobileProvisioningResult> ProvisionAsync(Guid employeeId, AuthRequestContext context, CancellationToken ct)
    {
        var actor = current.Require();
        if (actor.Client != SessionClient.Web || !actor.MfaSatisfied || employeeId == actor.EmployeeId)
            throw new AuthenticationFailure("forbidden", 403);
        return transaction.AtomicAsync(async token =>
        {
            var now = clock.GetUtcNow();
            if (!await administrators.IsCurrentAdministratorAsync(actor, now, token)) throw new AuthenticationFailure("forbidden", 403);
            var grants = await permissions.GetGrantsAsync(actor.AccountId, now, token);
            if (!AccessPermissionEvaluator.HasAllScope(actor, grants, AccessPermissionCatalog.AccountsMobileManage))
                throw new AuthenticationFailure("forbidden", 403);
            var fixedAdmin = false;
            foreach (var grant in grants.Where(g => AccessPermissionEvaluator.HasAllScope(actor, [g], AccessPermissionCatalog.AccountsMobileManage)))
                if (await administrators.IsCurrentSystemRoleAssignmentAsync(actor.AccountId, grant.RoleAssignmentId, now, token))
                { fixedAdmin = true; break; }
            if (!fixedAdmin) throw new AuthenticationFailure("forbidden", 403);
            var employee = await repository.GetApprovedEmployeeAsync(employeeId, token)
                ?? throw new AuthenticationFailure("approved_employee_required", 409);
            var submission = employee.Approval.Submission;
            if (!submission.MobileAccessRequested || submission.ApprovedLoginPhoneNumber is null)
                throw new AuthenticationFailure("approved_login_phone_required", 409);
            var phone = phones.Normalize(submission.ApprovedLoginPhoneNumber);
            if (phone != submission.ApprovedLoginPhoneNumber) throw new AuthenticationFailure("approved_login_phone_required", 409);
            var account = await repository.FindAccountAsync(employeeId, token);
            var existingAccount = account is not null;
            if (account is null)
            {
                account = new UserAccount(employee.Employee, employee.Approval, "employee:" + employeeId.ToString("N"), now);
                repository.AddAccount(account);
            }
            if (account.ApprovalDecisionId != employee.Approval.Id || account.Status is AccountStatus.Disabled or AccountStatus.Suspended)
                throw new AuthenticationFailure("account_not_eligible", 409);
            if (await administrators.HasSystemRoleAsync(account.Id, now, includeScheduled: true, token))
                throw new AuthenticationFailure("system_account_is_web_only", 409);
            var bindings = await repository.GetBindingsAsync(account.Id, token);
            var existing = bindings.SingleOrDefault(x => !x.RevokedAt.HasValue);
            if (existing is not null)
            {
                if (!existing.IsApprovedFor(account, now))
                    throw new AuthenticationFailure("phone_recovery_review_required", 409);
                // Do not undo an administrator's later decision to disable mobile access.
                return new MobileProvisioningResult(account.Id, account.Status, !account.InitialRoleAssignmentId.HasValue);
            }
            if (bindings.Count != 0) throw new AuthenticationFailure("phone_recovery_review_required", 409);
            if (await repository.IsPhoneBoundAsync(phone, token)) throw new AuthenticationFailure("login_phone_conflict", 409);
            if (existingAccount)
            {
                // First enrollment can activate existing or scheduled rights, just like enabling a channel.
                if (!AccessPermissionEvaluator.HasAllScope(actor, grants, AccessPermissionCatalog.RolePermissionsManage))
                    throw new AuthenticationFailure("forbidden", 403);
                var potential = await administrators.GetPotentialAccountPermissionsAsync(account.Id, now, token);
                var requiresMfa = false;
                foreach (var permission in potential)
                {
                    var definition = AccessPermissionCatalog.Require(permission.Permission.Code);
                    if (!AccessPermissionEvaluator.HasAllScope(actor, grants, definition.Code))
                        throw new AuthenticationFailure("forbidden", 403);
                    requiresMfa |= permission.Permission.IsSensitive || definition.Sensitive || definition.Management;
                }
                if (requiresMfa && !account.MfaRequired) account.RequireMfa();
            }
            account.SetMobileAccess(true, now);
            repository.AddBinding(new LoginPhoneBinding(account, phone, submission.Id, now));
            transaction.AddAudit(new AuditEvent("account.mobile.provisioned", actor.AccountId.ToString(), "access-management",
                "account", account.Id.ToString(), now, context.CorrelationId, account.EmployeeId, ipAddress: context.IpAddress));
            return new MobileProvisioningResult(account.Id, account.Status, !account.InitialRoleAssignmentId.HasValue);
        }, ct);
    }
}
