using System.Security.Cryptography;
using Cosmix.Application.Authentication.Web;
using Cosmix.Domain.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Cosmix.Infrastructure.Authentication.Web;

/// <summary>Identity V3 hashing and verification. Account mutation belongs to the authorized application service.</summary>
public sealed class MicrosoftWebPasswordVerifier : IWebPasswordVerifier, IWebPasswordHasher
{
    private readonly PasswordHasher<UserAccount> hasher;
    private readonly string dummyHash;

    public MicrosoftWebPasswordVerifier(PasswordHasher<UserAccount> hasher)
    {
        this.hasher = hasher;
        dummyHash = hasher.HashPassword(null!, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        if (Convert.FromBase64String(dummyHash)[0] != 1)
            throw new InvalidOperationException("Web passwords require an Identity V3 password hasher.");
    }

    public bool Verify(UserAccount account, string password)
    {
        if (account.PasswordHashFormat == WebPasswordFormats.IdentityV3 &&
            !string.IsNullOrEmpty(account.PasswordHash) && !string.IsNullOrEmpty(password))
        {
            try
            {
                // The explicit format label cannot disguise a legacy Identity V2 payload.
                var encoded = Convert.FromBase64String(account.PasswordHash);
                if (encoded.Length > 0 && encoded[0] == 1)
                {
                    var result = hasher.VerifyHashedPassword(account, account.PasswordHash, password);
                    return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
                }
            }
            catch (Exception error) when (error is FormatException or ArgumentException or OverflowException)
            {
                // Malformed stored credentials have the same external result as an incorrect password.
            }
        }
        VerifyDummy(password);
        return false;
    }

    public void VerifyDummy(string password) => hasher.VerifyHashedPassword(null!, dummyHash, password ?? string.Empty);

    public string Hash(UserAccount account, string password) => hasher.HashPassword(account, password);
}
