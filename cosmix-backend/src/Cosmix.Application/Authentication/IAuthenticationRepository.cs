using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Notifications;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.Authentication;

public sealed record AccountAccessPair(UserRoleAssignment Assignment, UserAccessScope Scope);
public sealed record AuthenticationAccount(UserAccount Account, LoginPhoneBinding? Phone,
    IReadOnlyList<AccountAccessPair> Access, EmployeeStatus EffectiveEmploymentStatus, bool HasConflictingStatus);

/// <summary>All writes run inside one SQL-backed authentication transaction; never return IQueryable.</summary>
public interface IAuthenticationRepository
{
    Task<T> AtomicAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
    Task<AuthenticationAccount?> FindByPhoneAsync(string phone, DateTimeOffset now, CancellationToken cancellationToken);
    Task<AuthenticationAccount?> GetAccountAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);
    Task<MobileOtpChallenge?> GetChallengeAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<MobileOtpChallenge>> GetOpenChallengesAsync(Guid accountId, CancellationToken cancellationToken);
    Task<MobileOtpChallenge?> GetLatestChallengeAsync(Guid accountId, CancellationToken cancellationToken);
    Task<AuthRateBucket?> GetRateBucketAsync(string key, CancellationToken cancellationToken);
    Task<RegisteredDevice?> FindDeviceAsync(Guid accountId, string hash, CancellationToken cancellationToken);
    Task<RegisteredDevice?> GetDeviceAsync(Guid accountId, Guid deviceId, CancellationToken cancellationToken);
    Task<RefreshToken?> FindRefreshTokenAsync(string hash, CancellationToken cancellationToken);
    Task<UserSession?> GetSessionAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserSession>> GetSessionsAsync(Guid accountId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserSession>> GetActiveSessionsPageAsync(Guid accountId, DateTimeOffset now,
        int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<RegisteredDevice>> GetDevicesAsync(Guid accountId, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<RegisteredDevice>> GetNonRevokedDevicesAsync(Guid accountId, CancellationToken cancellationToken);
    void AddChallenge(MobileOtpChallenge challenge, OtpDelivery delivery);
    void AddRateBucket(AuthRateBucket bucket);
    void AddDevice(RegisteredDevice device);
    void AddSession(UserSession session, RefreshToken refreshToken);
    void AddRefreshToken(RefreshToken token);
    void AddAudit(AuditEvent audit);
    void AddNotification(Notification notification);
}
