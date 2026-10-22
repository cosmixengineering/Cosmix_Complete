namespace Cosmix.Application.Authentication;

/// <summary>Explicit operator-approved limits. Zero values never enable an unsafe fallback.</summary>
public sealed class MobileAuthenticationOptions
{
    public bool Enabled { get; set; }
    public int OtpLifetimeSeconds { get; set; }
    public int ResendCooldownSeconds { get; set; }
    public int MaximumOtpAttempts { get; set; }
    public int RateWindowSeconds { get; set; }
    public int RequestsPerPhone { get; set; }
    public int RequestsPerIp { get; set; }
    public int RequestsGlobal { get; set; }
    public int VerificationAttemptsPerPhone { get; set; }
    public int VerificationAttemptsPerIp { get; set; }
    public int VerificationAttemptsGlobal { get; set; }
    public int AccessTokenLifetimeSeconds { get; set; }
    public int RefreshTokenLifetimeSeconds { get; set; }
    public int SessionLifetimeSeconds { get; set; }
    public int MinimumRequestDurationMilliseconds { get; set; }
    public string BusinessTimeZoneId { get; set; } = string.Empty;

    public bool IsValid()
    {
        if (!Enabled) return true;
        return OtpLifetimeSeconds is >= 60 and <= 600 &&
            ResendCooldownSeconds is >= 30 && ResendCooldownSeconds < OtpLifetimeSeconds &&
            MaximumOtpAttempts is >= 1 and <= 10 && RateWindowSeconds is >= 60 and <= 86400 &&
            RequestsPerPhone is >= 1 and <= 20 && RequestsPerIp >= RequestsPerPhone &&
            RequestsGlobal >= RequestsPerIp && VerificationAttemptsPerPhone >= MaximumOtpAttempts &&
            VerificationAttemptsPerIp >= VerificationAttemptsPerPhone &&
            VerificationAttemptsGlobal >= VerificationAttemptsPerIp &&
            AccessTokenLifetimeSeconds is >= 60 and <= 86400 &&
            RefreshTokenLifetimeSeconds >= AccessTokenLifetimeSeconds &&
            SessionLifetimeSeconds >= RefreshTokenLifetimeSeconds && SessionLifetimeSeconds <= 2592000 &&
            MinimumRequestDurationMilliseconds is >= 100 and <= 3000 && ValidTimeZone();
    }

    private bool ValidTimeZone()
    {
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(BusinessTimeZoneId); return true; }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        { return false; }
    }
}
