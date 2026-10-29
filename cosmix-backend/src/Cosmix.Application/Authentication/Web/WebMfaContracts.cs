using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Authentication.Web;

public sealed record WebMfaVerifyRequest(Guid ChallengeId, string Code, bool RecoveryCode, string BrowserSecret);
public sealed record WebMfaEnrollmentRequest(string Password, string BrowserSecret);
public sealed record WebMfaConfirmRequest(Guid CredentialId, string Code, string BrowserSecret);
public sealed record WebMfaRegenerateRequest(string Password, string Code, string BrowserSecret);
public sealed record WebMfaSetupResponse(Guid CredentialId, string Base32Secret, string Uri, DateTimeOffset ExpiresAt);
public sealed record WebMfaRecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes, string NextStep = "sign_in_again");
public sealed record WebMfaStatusResponse(bool AuthenticatorRequired, int RemainingRecoveryCodes);
public sealed record WebMfaResetRequest(string ExpectedAccountVersion, string Reason, string ApprovalReference);
public sealed record WebMfaResetResponse(Guid ApprovalId, string NextStep = "sign_in_with_sms_and_enroll_authenticator");

/// <summary>Tracked records share the existing authentication transaction. Never create a second unit of work.</summary>
public interface IWebMfaRepository
{
    Task<IReadOnlyList<MfaCredential>> CredentialsAsync(Guid accountId, CancellationToken ct);
    Task<MfaChallenge?> ChallengeAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<MfaChallenge>> OpenChallengesAsync(Guid accountId, CancellationToken ct);
    Task<MfaChallenge?> LatestChallengeAsync(Guid accountId, CancellationToken ct);
    void Add(MfaCredential credential);
    void Add(MfaChallenge challenge);
    void Add(MfaResetApproval approval);
}
