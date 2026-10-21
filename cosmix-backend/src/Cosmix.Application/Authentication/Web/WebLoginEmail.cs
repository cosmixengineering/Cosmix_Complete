using System.Net.Mail;

namespace Cosmix.Application.Authentication.Web;

public static class WebLoginEmail
{
    /// <summary>A single bare ASCII mailbox, normalized exactly like UserAccount.NormalizedLoginName.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 320) return null;
        var email = value.Trim();
        if (email.Any(c => c < '!' || c > '~' || "<>(),;:\\\"[]".Contains(c)) ||
            !MailAddress.TryCreate(email, out var mailbox) || mailbox.DisplayName.Length != 0 ||
            !string.Equals(mailbox.Address, email, StringComparison.Ordinal)) return null;
        return email.ToUpperInvariant();
    }
}
