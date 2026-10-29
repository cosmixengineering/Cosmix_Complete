namespace Cosmix.Application.Authentication.Web;

/// <summary>Local RFC 6238 cryptography only. The authentication transaction owns replay persistence.</summary>
public interface ITotpProvider
{
    TotpEnrollment Generate(string accountLabel);
    long? Verify(byte[] secret, string code, DateTimeOffset at, long? lastAcceptedCounter);
}
public sealed record TotpEnrollment(byte[] Secret, string Base32Secret, string Uri);
