using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;

namespace Cosmix.Application.Authentication.Web;

/// <summary>BrowserSecret comes from the server's protected browser cookie, never a client-selected channel.</summary>
public sealed record WebLoginRequest(string Email, string Password, string BrowserSecret);

/// <summary>Resend repeats the password factor and requires the latest web challenge and its original browser context.</summary>
public sealed record WebOtpResendRequest(Guid ChallengeId, WebLoginRequest Request);
public sealed record WebOtpVerifyRequest(Guid ChallengeId, string Code, string BrowserSecret);

/// <summary>Internal cookie-controller input. Neither this secret nor SessionResponse tokens belong in response JSON.</summary>
public sealed record WebRefreshRequest(string RefreshToken, string BrowserSecret);

public static class WebPasswordFormats
{
    public const string IdentityV3 = "Microsoft.AspNetCore.Identity.PasswordHasherV3";
}

public interface IWebPasswordVerifier
{
    /// <summary>Fail closed for any missing, malformed or unsupported stored hash. Never mutate the account.</summary>
    bool Verify(UserAccount account, string password);

    /// <summary>Perform a real password-hash verification for unknown, invalid or blocked credentials.</summary>
    void VerifyDummy(string password);
}

public interface IWebPasswordHasher
{
    string Hash(UserAccount account, string password);
}

/// <summary>
/// Query-only helper using the same scoped context as IAuthenticationRepository.
/// Read tracked state inside that repository's AtomicAsync; it owns every write and commit.
/// </summary>
public interface IWebAuthenticationRepository
{
    Task<Guid?> FindAccountIdByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<MobileOtpChallenge?> GetLatestWebChallengeAsync(Guid accountId, CancellationToken cancellationToken);
    /// <summary>All historical sessions, including revoked and expired sessions, inside the caller's authentication transaction.</summary>
    Task<bool> HasSessionHistoryAsync(Guid accountId, CancellationToken cancellationToken);
}
