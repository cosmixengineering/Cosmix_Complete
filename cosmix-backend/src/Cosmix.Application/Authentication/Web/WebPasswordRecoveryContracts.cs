using Cosmix.Domain.Authentication.Mobile;

namespace Cosmix.Application.Authentication.Web;

public sealed record WebForgotPasswordRequest(string Email, string BrowserSecret);
public sealed record WebPasswordResendRequest(Guid ChallengeId, string Email, string BrowserSecret);
public sealed record WebResetPasswordRequest(Guid ChallengeId, string Code, string NewPassword, string BrowserSecret);
public sealed record WebChangePasswordRequest(string CurrentPassword, string NewPassword, string BrowserSecret);

/// <summary>Query-only, sharing IAuthenticationRepository's scoped context and atomic transaction.</summary>
public interface IWebPasswordRecoveryRepository
{
    // Include consumed/revoked history so exhausting a challenge cannot reset the resend cooldown.
    Task<MobileOtpChallenge?> GetLatestResetChallengeAsync(Guid accountId, CancellationToken cancellationToken);
}
