using System.ComponentModel.DataAnnotations;
using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Authentication;

public sealed record OtpRequest(
    [Required, StringLength(40)] string PhoneNumber,
    [Required, StringLength(256, MinimumLength = 32)] string DeviceSecret,
    [Required, RegularExpression("^(android|ios)$")] string Platform,
    [Required, StringLength(150)] string DeviceName);
public sealed record OtpResendRequest([Required] OtpRequest Request, Guid ChallengeId);
public sealed record OtpVerifyRequest(Guid ChallengeId,
    [Required, RegularExpression("^[0-9]{6}$")] string Code,
    [Required, StringLength(256, MinimumLength = 32)] string DeviceSecret);
public sealed record RefreshRequest(
    [Required, StringLength(256, MinimumLength = 32)] string RefreshToken,
    [Required, StringLength(256, MinimumLength = 32)] string DeviceSecret);
public sealed record OtpRequestResponse(Guid ChallengeId, DateTimeOffset ExpiresAt,
    DateTimeOffset ResendAvailableAt, string NextStep = "enter_otp");
public sealed record SessionResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, Guid SessionId,
    string TokenType = "Bearer", string NextStep = "authenticated");
public sealed record AuthRequestContext(string IpAddress, string CorrelationId);
public sealed record DeviceResponse(Guid Id, string Platform, string Name, DateTimeOffset RegisteredAt,
    DateTimeOffset? LastSeenAt, DateTimeOffset? RevokedAt);
public sealed record SessionSummary(Guid Id, SessionClient Client, Guid? DeviceId,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, bool IsCurrent);
public sealed record CurrentAccountResponse(Guid AccountId, Guid EmployeeId, string? DisplayName,
    bool MobileAccessEnabled, bool WebAccessEnabled, SessionClient Client);
public sealed record AuthenticatedAccount(Guid AccountId, Guid EmployeeId, Guid SessionId,
    int SecurityVersion, SessionClient Client, bool MfaSatisfied);

public interface ICurrentAuthentication
{
    AuthenticatedAccount Require();
}

public interface IAccessTokenIssuer
{
    bool IsConfigured { get; }
    string Issue(UserAccount account, UserSession session, DateTimeOffset now, DateTimeOffset expiresAt);
}

public sealed class AuthenticationFailure(string code, int statusCode) : Exception(code)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public sealed record AuthenticationResult<T>(T? Value, string? Error = null, int StatusCode = 401)
{
    public T RequireValue() => Error is null && Value is not null ? Value :
        throw new AuthenticationFailure(Error ?? "invalid_authentication", StatusCode);
}
