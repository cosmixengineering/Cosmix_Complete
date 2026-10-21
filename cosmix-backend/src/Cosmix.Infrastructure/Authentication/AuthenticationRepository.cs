using System.Data;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Notifications;
using Cosmix.Domain.HR.Employees;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Authentication;

public sealed class AuthenticationRepository(CosmixDbContext db) : IAuthenticationRepository
{
    public async Task<T> AtomicAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Authentication requires its own audited transaction boundary.");
        if (db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Stage authentication mutations inside the transaction.");
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        // A database lock coordinates API replicas, SMS dispatch, verification and refresh rotation.
        // No external HTTP call is permitted inside this transaction.
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource=N'cosmix:authentication',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51001, 'Authentication is temporarily busy.', 1;
            """, cancellationToken);
        try
        {
            var result = await action(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<AuthenticationAccount?> FindByPhoneAsync(string phone, DateTimeOffset now, CancellationToken ct)
    {
        var binding = await db.Set<LoginPhoneBinding>().SingleOrDefaultAsync(x => x.PhoneNumber == phone && x.RevokedAt == null, ct);
        return binding is null ? null : await GetAccountAsync(binding.UserAccountId, now, ct);
    }

    public async Task<AuthenticationAccount?> GetAccountAsync(Guid id, DateTimeOffset now, CancellationToken ct)
    {
        var account = await db.UserAccounts.Include(x => x.Employee)
            .Include(x => x.ApprovalDecision).ThenInclude(x => x.Submission).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (account is null) return null;
        var binding = await db.Set<LoginPhoneBinding>().Include(x => x.LoginPhoneChange)
            .SingleOrDefaultAsync(x => x.UserAccountId == id && x.RevokedAt == null, ct);
        var assignments = await db.UserRoleAssignments.Include(x => x.Role).Where(x => x.UserAccountId == id &&
            x.EffectiveFrom <= now && (x.EffectiveTo == null || x.EffectiveTo > now) &&
            (x.RevokedAt == null || x.RevokedAt > now) && x.Role.IsActive).ToListAsync(ct);
        var ids = assignments.Select(x => x.Id).ToArray();
        var scopes = await db.UserAccessScopes.Where(x => ids.Contains(x.UserRoleAssignmentId)).ToListAsync(ct);
        var access = (from a in assignments from s in scopes where s.UserRoleAssignmentId == a.Id
            select new AccountAccessPair(a, s)).ToArray();
        var statuses = await db.EmployeeStatusChanges.Where(x => x.EmployeeId == account.EmployeeId && x.EffectiveAt <= now)
            .OrderByDescending(x => x.EffectiveAt).Take(2).ToListAsync(ct);
        return new(account, binding, access, statuses.FirstOrDefault()?.NewStatus ?? account.Employee.Status,
            statuses.Count > 1 && statuses[0].EffectiveAt == statuses[1].EffectiveAt ||
            !EmployeeLifecycleAccessRules.IsReconciled(statuses.FirstOrDefault(), account.Employee.LastAppliedStatusChangeId));
    }

    public Task<MobileOtpChallenge?> GetChallengeAsync(Guid id, CancellationToken ct) =>
        db.Set<MobileOtpChallenge>().Include(x => x.PhoneBinding).ThenInclude(x => x.LoginPhoneChange).SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<MobileOtpChallenge>> GetOpenChallengesAsync(Guid accountId, CancellationToken ct) =>
        await db.Set<MobileOtpChallenge>().Where(x => x.UserAccountId == accountId && x.ConsumedAt == null && x.RevokedAt == null).ToListAsync(ct);
    public Task<MobileOtpChallenge?> GetLatestChallengeAsync(Guid accountId, CancellationToken ct) =>
        db.Set<MobileOtpChallenge>().Where(x => x.UserAccountId == accountId && x.Purpose == "MobileLogin")
            .OrderByDescending(x => x.IssuedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
    public Task<AuthRateBucket?> GetRateBucketAsync(string key, CancellationToken ct) =>
        db.Set<AuthRateBucket>().SingleOrDefaultAsync(x => x.Key == key, ct);
    public Task<RegisteredDevice?> FindDeviceAsync(Guid accountId, string hash, CancellationToken ct) =>
        db.RegisteredDevices.Where(x => x.UserAccountId == accountId && x.DeviceIdentifierHash == hash)
            .OrderBy(x => x.RevokedAt != null).ThenByDescending(x => x.RegisteredAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
    public Task<RegisteredDevice?> GetDeviceAsync(Guid accountId, Guid deviceId, CancellationToken ct) =>
        db.RegisteredDevices.SingleOrDefaultAsync(x => x.UserAccountId == accountId && x.Id == deviceId, ct);
    public Task<RefreshToken?> FindRefreshTokenAsync(string hash, CancellationToken ct) =>
        db.RefreshTokens.Include(x => x.UserSession).ThenInclude(x => x.RegisteredDevice).SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
    public Task<UserSession?> GetSessionAsync(Guid id, CancellationToken ct) =>
        db.UserSessions.Include(x => x.RegisteredDevice).SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<UserSession>> GetSessionsAsync(Guid accountId, CancellationToken ct) =>
        await db.UserSessions.Where(x => x.UserAccountId == accountId && x.RevokedAt == null).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
    public async Task<IReadOnlyList<UserSession>> GetActiveSessionsPageAsync(Guid accountId, DateTimeOffset now,
        int page, int pageSize, CancellationToken ct) =>
        await db.UserSessions.AsNoTracking().Where(x => x.UserAccountId == accountId && x.RevokedAt == null &&
            x.CreatedAt <= now && now < x.ExpiresAt).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    public async Task<IReadOnlyList<RegisteredDevice>> GetDevicesAsync(Guid accountId, int page, int pageSize, CancellationToken ct) =>
        await db.RegisteredDevices.Where(x => x.UserAccountId == accountId)
            .OrderByDescending(x => x.RegisteredAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    public void AddChallenge(MobileOtpChallenge challenge, OtpDelivery delivery) { db.Add(challenge); db.Add(delivery); }
    public void AddRateBucket(AuthRateBucket bucket) => db.Add(bucket);
    public void AddDevice(RegisteredDevice device) => db.Add(device);
    public void AddSession(UserSession session, RefreshToken token) { db.Add(session); db.Add(token); }
    public void AddRefreshToken(RefreshToken token) => db.Add(token);
    public void AddAudit(AuditEvent audit) => db.Add(audit);
    public void AddNotification(Notification notification) => db.Add(notification);
}
