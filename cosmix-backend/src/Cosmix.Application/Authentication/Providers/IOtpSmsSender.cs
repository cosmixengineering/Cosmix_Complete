namespace Cosmix.Application.Authentication.Providers;

public enum SmsSendResult
{
    Unavailable = 0,
    Accepted = 1,
    Rejected = 2,
    Unknown = 3
}

public interface IOtpSmsSender
{
    bool IsConfigured { get; }

    /// <summary>
    /// Sends once. Accepted is gateway acceptance, never handset delivery. Unknown may have been
    /// accepted and must not be retried blindly. Unavailable means no request was attempted.
    /// Cancellation before dispatch propagates; cancellation after dispatch returns Unknown.
    /// Invalid arguments throw ArgumentException without including sensitive values.
    /// </summary>
    Task<SmsSendResult> SendAsync(string phone, string code, string idempotencyKey, CancellationToken cancellationToken);
}
