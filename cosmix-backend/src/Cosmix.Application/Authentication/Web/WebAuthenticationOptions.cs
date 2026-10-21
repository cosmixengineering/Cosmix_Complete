namespace Cosmix.Application.Authentication.Web;

/// <summary>Operator-approved password controls; shared OTP/session controls remain in MobileAuthenticationOptions.</summary>
public sealed class WebAuthenticationOptions
{
    public bool Enabled { get; set; }
    // Zero leaves initial credential provisioning unavailable until the operator chooses a policy.
    public int MinimumPasswordLength { get; set; }
    public int MaximumPasswordFailures { get; set; }
    public int PasswordLockoutSeconds { get; set; }
    public int PasswordAttemptsPerAccount { get; set; }
    public int PasswordAttemptsPerEmail { get; set; }
    public int PasswordAttemptsPerIp { get; set; }
    public int PasswordAttemptsGlobal { get; set; }

    public bool IsValid() => !Enabled ||
        (MaximumPasswordFailures is >= 1 and <= 20 && PasswordLockoutSeconds is >= 60 and <= 86400 &&
         PasswordAttemptsPerAccount >= MaximumPasswordFailures && PasswordAttemptsPerAccount <= 100 &&
         PasswordAttemptsPerEmail >= MaximumPasswordFailures && PasswordAttemptsPerEmail <= 100 &&
         PasswordAttemptsPerIp >= PasswordAttemptsPerAccount && PasswordAttemptsPerIp >= PasswordAttemptsPerEmail &&
         PasswordAttemptsGlobal >= PasswordAttemptsPerIp);
}
