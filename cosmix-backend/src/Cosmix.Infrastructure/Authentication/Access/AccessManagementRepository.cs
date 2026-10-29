using System.Data;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Authentication.Scopes;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Departments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Authentication.Access;

public sealed class AccessManagementRepository(CosmixDbContext db, MobileAuthenticationOptions options)
    : IAccessManagementRepository
{
    public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken) => ExecuteTransactionAsync(action, false, false, cancellationToken);

    public Task<T> InReadTransactionAsync<T>(Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken, bool serializeReaders = false) =>
        ExecuteTransactionAsync(action, true, serializeReaders, cancellationToken);

    private async Task<T> ExecuteTransactionAsync<T>(Func<CancellationToken, Task<T>> action,
        bool readOnly, bool serializeReaders, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Stage access mutations inside their own audited transaction.");
        // Authentication middleware can have tracked these accounts before this transaction began.
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        EventHandler<SavingChangesEventArgs> guard = (_, _) => RequireAuditOnlyChanges();
        if (readOnly) db.SavingChanges += guard;
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=N'cosmix:authentication',
                    @LockMode=@mode, @LockOwner=N'Transaction', @LockTimeout=10000;
                IF @result < 0 THROW 51001, 'Authentication is temporarily busy.', 1;
                """, [new SqlParameter("@mode", !readOnly ? "Exclusive" : serializeReaders ? "Update" : "Shared")], cancellationToken);
            var result = await action(cancellationToken);
            if (readOnly) RequireAuditOnlyChanges();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AuthenticationFailure("access_version_conflict", 409);
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 51016 })
        {
            throw new AuthenticationFailure("bank_month_closed", 409);
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 or 547 or 1205 })
        {
            throw new AuthenticationFailure("access_write_conflict", 409);
        }
        catch (SqlException error) when (error.Number == 1205)
        {
            throw new AuthenticationFailure("access_write_conflict", 409);
        }
        catch (SqlException error) when (error.Number == 51001)
        {
            throw new AuthenticationFailure("access_temporarily_busy", 503);
        }
        finally
        {
            if (readOnly) db.SavingChanges -= guard;
            db.ChangeTracker.Clear();
        }
    }

    private void RequireAuditOnlyChanges()
    {
        // Shared readers may append audit evidence, but must never upgrade to a business writer.
        // Check every SaveChanges (including intermediate saves), not just the final tracked state.
        db.ChangeTracker.DetectChanges();
        if (db.ChangeTracker.Entries().Any(entry =>
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
                !(entry.Entity is AuditEvent && entry.State == EntityState.Added)))
            throw new InvalidOperationException("Audited read transactions cannot change business records.");
    }

    public async Task<bool> IsCurrentAdministratorAsync(AuthenticatedAccount actor, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (actor.Client != SessionClient.Web || !actor.MfaSatisfied ||
            !TryGetBusinessDate(options.BusinessTimeZoneId, now, out var businessDate)) return false;
        var history = await db.Set<EmployeeStatusChange>().AsNoTracking()
            .Where(s => s.EmployeeId == actor.EmployeeId && s.EffectiveAt <= now)
            .OrderByDescending(s => s.EffectiveAt).Take(2).ToListAsync(cancellationToken);
        var marker = await db.Employees.AsNoTracking().Where(e => e.Id == actor.EmployeeId)
            .Select(e => e.LastAppliedStatusChangeId).SingleOrDefaultAsync(cancellationToken);
        return HasEligibleEmploymentHistory(history, now) && EmployeeLifecycleAccessRules.IsReconciled(history.FirstOrDefault(), marker) &&
            await CurrentAdministratorQuery(actor, now, businessDate).AnyAsync(cancellationToken);
    }

    internal IQueryable<Guid> CurrentAdministratorQuery(AuthenticatedAccount actor, DateTimeOffset now, DateOnly businessDate)
    {
        var sms = db.Set<MobileOtpChallenge>().AsNoTracking()
            .Where(p => p.Purpose == "WebLogin" && p.Platform == "web" && p.RevokedAt == null &&
                p.MaximumAttempts > 0 && p.FailedAttempts >= 0 && p.FailedAttempts < p.MaximumAttempts &&
                p.DeviceHash.Length == 64)
            .Select(p => new { p.Id, p.UserAccountId, p.SecurityVersion, PhoneBindingId = (Guid?)p.PhoneBindingId,
                p.IssuedAt, p.ExpiresAt, p.ConsumedAt, Authenticator = false });
        var totp = from p in db.Set<MfaChallenge>().AsNoTracking()
                   join c in db.Set<MfaCredential>().AsNoTracking() on p.VerifiedCredentialId equals c.Id
                   where p.Purpose == "WebLoginTotp" && p.RegisteredDeviceId == null && p.RevokedAt == null &&
                         p.FailedAttempts >= 0 && p.FailedAttempts < options.MaximumOtpAttempts &&
                         p.BrowserContextHash != null && p.BrowserContextHash.Length == 64 &&
                         p.VerifiedAt != null && p.VerifiedAt >= p.IssuedAt && p.VerifiedAt <= p.ConsumedAt &&
                         c.UserAccountId == p.UserAccountId && c.RevokedAt == null && c.ConfirmedAt != null && c.ConfirmedAt <= p.VerifiedAt &&
                         ((c.Method == MfaMethod.Totp && c.LastAcceptedCounter != null) ||
                          (c.Method == MfaMethod.RecoveryCode && c.RecoveryCodeConsumedAt == p.VerifiedAt))
                   select new { p.Id, p.UserAccountId, p.SecurityVersion, p.PhoneBindingId,
                       p.IssuedAt, p.ExpiresAt, p.ConsumedAt, Authenticator = true };
        return from session in db.Set<UserSession>().AsNoTracking()
         join proof in sms.Concat(totp) on (session.MfaChallengeId ?? session.SmsOtpChallengeId) equals (Guid?)proof.Id
         join phone in db.Set<LoginPhoneBinding>().AsNoTracking() on proof.PhoneBindingId equals phone.Id
         where session.Id == actor.SessionId && session.UserAccountId == actor.AccountId &&
               session.Client == SessionClient.Web && session.SecurityVersion == actor.SecurityVersion &&
               session.CreatedAt <= now && now < session.ExpiresAt && session.RevokedAt == null &&
               session.UserAccount.Status == AccountStatus.Active && session.UserAccount.WebAccessEnabled &&
               session.UserAccount.SecurityVersion == actor.SecurityVersion && session.UserAccount.EmployeeId == actor.EmployeeId &&
               (session.UserAccount.LockedUntil == null || session.UserAccount.LockedUntil <= now) &&
               session.UserAccount.Employee.Status == EmployeeStatus.Active &&
               session.UserAccount.Employee.JoinedOn != null && session.UserAccount.Employee.JoinedOn <= businessDate &&
               session.UserAccount.Employee.OnboardingStatus == OnboardingStatus.Approved &&
               session.UserAccount.Employee.ApprovedOnboardingDecisionId == session.UserAccount.ApprovalDecisionId &&
               session.UserAccount.ApprovalDecision.EmployeeId == actor.EmployeeId &&
               session.UserAccount.ApprovalDecision.Decision == OnboardingDecision.Approve &&
               session.RegisteredDeviceId == null && session.UserAccount.AuthenticatorRequired == proof.Authenticator &&
               ((proof.Authenticator && session.MfaChallengeId != null && session.SmsOtpChallengeId == null) ||
                (!proof.Authenticator && session.SmsOtpChallengeId != null && session.MfaChallengeId == null)) &&
               session.UserAccount.PasswordHash != null && session.UserAccount.PasswordHashFormat != null &&
               session.UserAccount.PasswordChangedAt != null && session.UserAccount.PasswordChangedAt <= proof.IssuedAt &&
               proof.UserAccountId == actor.AccountId && proof.SecurityVersion == actor.SecurityVersion &&
               proof.IssuedAt <= session.CreatedAt &&
               proof.ConsumedAt == session.CreatedAt && session.CreatedAt < proof.ExpiresAt &&
               phone.UserAccountId == actor.AccountId && phone.RevokedAt == null &&
               phone.VerifiedAt != null && phone.VerifiedAt <= session.CreatedAt && phone.CreatedAt <= proof.IssuedAt &&
               (!proof.Authenticator || phone.VerifiedAt <= proof.IssuedAt) &&
               phone.ApprovedSubmissionId == session.UserAccount.ApprovalDecision.SubmissionId &&
               ((phone.LoginPhoneChangeId == null && phone.PhoneNumber == session.UserAccount.ApprovalDecision.Submission.ApprovedLoginPhoneNumber) ||
                (phone.LoginPhoneChangeId != null && phone.LoginPhoneChange != null &&
                 phone.LoginPhoneChange.UserAccountId == actor.AccountId && phone.LoginPhoneChange.NewPhoneNumber == phone.PhoneNumber &&
                 phone.LoginPhoneChange.CompletedAt == phone.VerifiedAt && phone.LoginPhoneChange.CompletionChallengeId != null &&
                 phone.LoginPhoneChange.SupersededAt == null && phone.LoginPhoneChange.ApprovedAt <= phone.CreatedAt))
         select session.Id;
    }

    internal static bool HasEligibleEmploymentHistory(IEnumerable<EmployeeStatusChange> history, DateTimeOffset now)
    {
        var latest = history.Where(s => s.EffectiveAt <= now).OrderByDescending(s => s.EffectiveAt).Take(2).ToArray();
        return latest.Length == 0 || (latest[0].NewStatus == EmployeeStatus.Active &&
            (latest.Length == 1 || latest[0].EffectiveAt != latest[1].EffectiveAt));
    }

    internal static bool TryGetBusinessDate(string timeZoneId, DateTimeOffset now, out DateOnly businessDate)
    {
        businessDate = default;
        if (string.IsNullOrWhiteSpace(timeZoneId)) return false;
        try
        {
            businessDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,
                TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).DateTime);
            return true;
        }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            return false;
        }
    }

    public Task<string?> GetAssignmentRoleCodeAsync(Guid assignmentId, CancellationToken cancellationToken) =>
        db.Set<UserRoleAssignment>().Where(a => a.Id == assignmentId).Select(a => a.Role.Code)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> IsCurrentSuperAdminAsync(AuthenticatedAccount actor, DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await IsCurrentAdministratorAsync(actor, now, cancellationToken) &&
        await SystemRoleAssignmentsQuery(db.Set<UserRoleAssignment>().AsNoTracking(), actor.AccountId, now,
                includeScheduled: false)
            .AnyAsync(a => a.Role.IsActive && a.Role.SystemRole == SystemRoleKind.SuperAdmin, cancellationToken);

    public Task<bool> HasSystemRoleAsync(Guid accountId, DateTimeOffset now, bool includeScheduled,
        CancellationToken cancellationToken) =>
        SystemRoleAssignmentsQuery(db.Set<UserRoleAssignment>().AsNoTracking(), accountId, now, includeScheduled)
            .AnyAsync(cancellationToken);

    public async Task<bool> IsEmployeeEligibleForSystemRoleGrantAsync(Guid employeeId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await IsEmployeeActiveForAccessAsync(employeeId, now, cancellationToken) &&
            !await db.EmployeeStatusChanges.AsNoTracking().AnyAsync(s => s.EmployeeId == employeeId &&
                s.EffectiveAt > now && s.NewStatus != EmployeeStatus.Active, cancellationToken);
    }

    public async Task<bool> IsEmployeeActiveForAccessAsync(Guid employeeId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var employee = await db.Employees.AsNoTracking().Where(e => e.Id == employeeId && e.Status == EmployeeStatus.Active)
            .Select(e => new { e.LastAppliedStatusChangeId }).SingleOrDefaultAsync(cancellationToken);
        if (employee is null) return false;
        var history = await db.EmployeeStatusChanges.AsNoTracking()
            .Where(s => s.EmployeeId == employeeId && s.EffectiveAt <= now)
            .OrderByDescending(s => s.EffectiveAt).Take(2).ToListAsync(cancellationToken);
        return HasEligibleEmploymentHistory(history, now) &&
            EmployeeLifecycleAccessRules.IsReconciled(history.FirstOrDefault(), employee.LastAppliedStatusChangeId);
    }

    public Task<IdempotencyReceipt?> FindReceiptAsync(string actor, string operation, string key,
        CancellationToken cancellationToken) => db.Set<IdempotencyReceipt>()
        .SingleOrDefaultAsync(r => r.ActorUserId == actor && r.Operation == operation && r.Key == key, cancellationToken);

    public Task<bool> IsCurrentSystemRoleAssignmentAsync(Guid accountId, Guid assignmentId, DateTimeOffset now,
        CancellationToken cancellationToken) =>
        SystemRoleAssignmentsQuery(db.Set<UserRoleAssignment>().AsNoTracking(), accountId, now, includeScheduled: false)
            .AnyAsync(a => a.Id == assignmentId && a.Role.IsActive &&
                (a.Role.SystemRole == SystemRoleKind.Admin || a.Role.SystemRole == SystemRoleKind.SuperAdmin), cancellationToken);

    internal static IQueryable<UserRoleAssignment> SystemRoleAssignmentsQuery(IQueryable<UserRoleAssignment> assignments,
        Guid accountId, DateTimeOffset now, bool includeScheduled) =>
        assignments.Where(a => a.UserAccountId == accountId && a.Role.SystemRole != SystemRoleKind.None &&
            (includeScheduled || a.EffectiveFrom <= now) &&
            (a.EffectiveTo == null || now < a.EffectiveTo) && (a.RevokedAt == null || now < a.RevokedAt));

    public async Task<PagedResult<AccessRoleResponse>> ListRolesAsync(AccessListRequest request,
        CancellationToken cancellationToken)
    {
        var query = db.Set<Role>().AsNoTracking();
        var count = await query.CountAsync(cancellationToken);
        var roles = await query.OrderBy(r => r.Code).ThenBy(r => r.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        var ids = roles.Select(r => r.Id).ToArray();
        var permissions = await db.Set<RolePermission>().AsNoTracking()
            .Where(p => ids.Contains(p.RoleId) && p.RevokedAt == null)
            .Select(p => new { p.RoleId, p.Permission.Code }).ToListAsync(cancellationToken);
        return new PagedResult<AccessRoleResponse>(roles.Select(r => new AccessRoleResponse(r.Id, r.Code,
            r.Name, r.IsActive, permissions.Where(p => p.RoleId == r.Id).Select(p => p.Code).Order(StringComparer.Ordinal).ToArray(),
            Convert.ToBase64String(r.RowVersion), r.SystemRole)).ToArray(), count, request.Page, request.PageSize);
    }

    public Task<Role?> FindRoleAsync(Guid roleId, CancellationToken cancellationToken) =>
        db.Set<Role>().SingleOrDefaultAsync(r => r.Id == roleId, cancellationToken);

    public Task<bool> RoleCodeExistsAsync(string code, CancellationToken cancellationToken) =>
        db.Set<Role>().AnyAsync(r => r.Code == code, cancellationToken);

    public Task<bool> RoleHasEverBeenAssignedAsync(Guid roleId, CancellationToken cancellationToken) =>
        db.Set<UserRoleAssignment>().AnyAsync(a => a.RoleId == roleId, cancellationToken);

    public async Task<IReadOnlyList<RolePermission>> GetRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken) =>
        await db.Set<RolePermission>().Include(p => p.Role).Include(p => p.Permission)
            .Where(p => p.RoleId == roleId && p.RevokedAt == null).ToListAsync(cancellationToken);

    public async Task<Permission> GetOrAddPermissionAsync(AccessPermissionDefinition definition, CancellationToken cancellationToken)
    {
        var permission = await db.Set<Permission>().SingleOrDefaultAsync(p => p.Code == definition.Code, cancellationToken);
        if (permission is null)
        {
            permission = new Permission(definition.Code, definition.Description, definition.Sensitive);
            db.Set<Permission>().Add(permission);
        }
        // Case-insensitive database collations must not admit a noncanonical catalog code.
        if (permission.Code != definition.Code) throw new AuthenticationFailure("unsupported_permission", 400);
        return permission;
    }

    public Task<UserAccount?> FindAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        db.Set<UserAccount>().Include(a => a.Employee).Include(a => a.ApprovalDecision).ThenInclude(a => a.Submission)
            .SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken);

    public async Task<PagedResult<AccessAssignmentResponse>> ListAssignmentsAsync(Guid accountId,
        AccessListRequest request, CancellationToken cancellationToken)
    {
        var query = db.Set<UserRoleAssignment>().AsNoTracking().Where(a => a.UserAccountId == accountId);
        var count = await query.CountAsync(cancellationToken);
        var assignments = await query.Include(a => a.Role).OrderByDescending(a => a.EffectiveFrom).ThenBy(a => a.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        var ids = assignments.Select(a => a.Id).ToArray();
        var scopes = await db.Set<UserAccessScope>().AsNoTracking()
            .Where(s => ids.Contains(s.UserRoleAssignmentId)).ToListAsync(cancellationToken);
        return new PagedResult<AccessAssignmentResponse>(assignments.Select(a => new AccessAssignmentResponse(a.Id,
            a.RoleId, a.Role.Code, a.EffectiveFrom, a.EffectiveTo, a.RevokedAt,
            scopes.Where(s => s.UserRoleAssignmentId == a.Id).OrderBy(s => s.Kind).ThenBy(s => s.Id)
                .Select(s => new AccessScopeRequest(s.Kind, ScopeTarget(s))).ToArray(),
            Convert.ToBase64String(a.RowVersion))).ToArray(), count, request.Page, request.PageSize);
    }

    public Task<UserRoleAssignment?> FindAssignmentAsync(Guid accountId, Guid assignmentId, CancellationToken cancellationToken) =>
        db.Set<UserRoleAssignment>().SingleOrDefaultAsync(a => a.Id == assignmentId && a.UserAccountId == accountId, cancellationToken);

    public async Task<IReadOnlyList<RolePermission>> GetPotentialAccountPermissionsAsync(Guid accountId, DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await db.Set<RolePermission>().Include(p => p.Permission).Where(p => p.RevokedAt == null &&
            db.Set<UserRoleAssignment>().Any(a => a.UserAccountId == accountId && a.RoleId == p.RoleId &&
                a.Role.IsActive && (a.RevokedAt == null || a.RevokedAt > now) &&
                (a.EffectiveTo == null || a.EffectiveTo > now))).ToListAsync(cancellationToken);

    public Task<bool> ScopeTargetExistsAsync(AccessScopeRequest scope, CancellationToken cancellationToken) => scope.Kind switch
    {
        AccessScopeKind.Self or AccessScopeKind.All => Task.FromResult(scope.TargetId is null),
        AccessScopeKind.Employee => db.Set<Employee>().AnyAsync(e => e.Id == scope.TargetId, cancellationToken),
        AccessScopeKind.Department => db.Set<Department>().AnyAsync(e => e.Id == scope.TargetId, cancellationToken),
        AccessScopeKind.Team => db.Set<Team>().AnyAsync(e => e.Id == scope.TargetId, cancellationToken),
        AccessScopeKind.Site => db.Set<Site>().AnyAsync(e => e.Id == scope.TargetId, cancellationToken),
        AccessScopeKind.Project => db.Set<Project>().AnyAsync(e => e.Id == scope.TargetId, cancellationToken),
        _ => Task.FromResult(false)
    };

    public void ExpectRoleVersion(Role role, byte[] version)
    {
        CheckVersion(role.RowVersion, version);
        db.Entry(role).Property(r => r.RowVersion).OriginalValue = version;
        // Child permission changes and assignments participate in the same role concurrency boundary.
        db.Entry(role).Property(r => r.IsActive).IsModified = true;
    }

    public void ExpectAccountVersion(UserAccount account, byte[] version)
    {
        CheckVersion(account.RowVersion, version);
        db.Entry(account).Property(a => a.RowVersion).OriginalValue = version;
    }

    public void ExpectAssignmentVersion(UserRoleAssignment assignment, byte[] version)
    {
        CheckVersion(assignment.RowVersion, version);
        db.Entry(assignment).Property(a => a.RowVersion).OriginalValue = version;
    }

    public void AddRole(Role role) => db.Set<Role>().Add(role);
    public void AddRolePermission(RolePermission permission) => db.Set<RolePermission>().Add(permission);
    public void AddAssignment(UserRoleAssignment assignment, IReadOnlyList<UserAccessScope> scopes)
    {
        db.Set<UserRoleAssignment>().Add(assignment);
        db.Set<UserAccessScope>().AddRange(scopes);
    }
    public void AddAudit(AuditEvent audit) => db.Set<AuditEvent>().Add(audit);
    public void AddReceipt(IdempotencyReceipt receipt) => db.Set<IdempotencyReceipt>().Add(receipt);
    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await db.SaveChangesAsync(cancellationToken);

    private static void CheckVersion(byte[] actual, byte[] expected)
    {
        if (expected.Length != 8 || !actual.SequenceEqual(expected))
            throw new AuthenticationFailure("access_version_conflict", 409);
    }

    private static Guid? ScopeTarget(UserAccessScope scope) => scope.Kind switch
    {
        AccessScopeKind.Employee => scope.EmployeeId,
        AccessScopeKind.Department => scope.DepartmentId,
        AccessScopeKind.Team => scope.TeamId,
        AccessScopeKind.Site => scope.SiteId,
        AccessScopeKind.Project => scope.ProjectId,
        _ => null
    };
}
