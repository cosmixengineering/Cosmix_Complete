using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication.Mobile;

public enum OtpDeliveryStatus { Pending = 0, Submitted = 1, Failed = 2, Expired = 3, Dispatching = 4 }

/// <summary>One dispatch attempt. Persist the claim with concurrency control before calling the provider.</summary>
public sealed class OtpDelivery
{
    private OtpDelivery() { }

    public OtpDelivery(MobileOtpChallenge challenge, string protectedPayload, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        if (!challenge.IsUsableAt(createdAt)) throw new InvalidOperationException("Delivery requires a current challenge.");
        Id = Guid.NewGuid(); ChallengeId = challenge.Id; Challenge = challenge;
        ProtectedPayload = ModelGuard.Required(protectedPayload, 8000, nameof(protectedPayload));
        CreatedAt = ModelGuard.Utc(createdAt); ExpiresAt = challenge.ExpiresAt;
    }

    public Guid Id { get; private set; }
    public Guid ChallengeId { get; private set; }
    public MobileOtpChallenge Challenge { get; private set; } = null!;
    public string? ProtectedPayload { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public int Attempts { get; private set; }
    public OtpDeliveryStatus Status { get; private set; }
    public DateTimeOffset? ClaimedAt { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>A lost/expired lease must be failed, never reclaimed: the provider may already have accepted the send.</summary>
    public void Claim(DateTimeOffset at, DateTimeOffset leaseUntil)
    {
        if (Status != OtpDeliveryStatus.Pending || Attempts != 0 || at < CreatedAt || at >= ExpiresAt ||
            leaseUntil <= at || leaseUntil > ExpiresAt)
            throw new InvalidOperationException("Delivery cannot be claimed.");
        ClaimedAt = ModelGuard.Utc(at); LeaseUntil = ModelGuard.Utc(leaseUntil);
        Attempts = 1; Status = OtpDeliveryStatus.Dispatching;
    }

    /// <summary>Records provider acceptance only, never handset delivery.</summary>
    public void MarkSubmitted(DateTimeOffset at)
    {
        if (Status != OtpDeliveryStatus.Dispatching || !ClaimedAt.HasValue || !LeaseUntil.HasValue ||
            at < ClaimedAt || at >= LeaseUntil || at >= ExpiresAt)
            throw new InvalidOperationException("Delivery has no current dispatch lease.");
        SubmittedAt = ModelGuard.Utc(at); Status = OtpDeliveryStatus.Submitted; ProtectedPayload = null;
    }

    public void MarkFailed(DateTimeOffset at)
    {
        RequireNonterminal(at);
        Status = OtpDeliveryStatus.Failed; ProtectedPayload = null;
    }

    public void Expire(DateTimeOffset at)
    {
        RequireNonterminal(at);
        if (at < ExpiresAt) throw new InvalidOperationException("Delivery has not expired.");
        Status = OtpDeliveryStatus.Expired; ProtectedPayload = null;
    }

    private void RequireNonterminal(DateTimeOffset at)
    {
        if (Status is not (OtpDeliveryStatus.Pending or OtpDeliveryStatus.Dispatching) || at < CreatedAt || at < ClaimedAt)
            throw new InvalidOperationException("Delivery is terminal or the transition precedes its evidence.");
    }
}
