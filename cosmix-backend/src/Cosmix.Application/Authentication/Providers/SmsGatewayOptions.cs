namespace Cosmix.Application.Authentication.Providers;

/// <summary>
/// Opt-in contract for an operator-owned HTTPS gateway, not a universal SMS-vendor adapter.
/// POST JSON is exactly {recipient,message,idempotencyKey}, authorized using a Bearer token.
/// The gateway must return 200/202 only after accepting the message, and guarantee that any
/// 4xx except 408 means not accepted. 408, 5xx, unexpected 2xx and redirects are Unknown.
/// It must honor the opaque idempotencyKey for duplicate suppression and own vendor translation.
/// Responses are ignored; receipts and handset-delivery confirmation are outside this contract.
/// Gateway/proxy/APM operators must suppress credential/body logging and automatic POST retries.
/// A chosen vendor with a different contract requires a separate reviewed adapter.
/// </summary>
public sealed class SmsGatewayOptions
{
    public const string SectionName = "MobileAuthentication:Sms";
    public const string GenericHttpProvider = "GenericHttp";

    /// <summary>Set from MobileAuthentication:Enabled by the provider registration.</summary>
    public bool Enabled { get; set; }
    public string Provider { get; set; } = string.Empty;

    /// <summary>Fixed HTTPS URL with no userinfo, query or fragment. Not supplied by login requests.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Secret-store/environment value; never commit or log.</summary>
    public string BearerToken { get; set; } = string.Empty;

    /// <summary>Explicit request timeout, 1-60 seconds. No retry is installed.</summary>
    public int TimeoutSeconds { get; set; }
}
