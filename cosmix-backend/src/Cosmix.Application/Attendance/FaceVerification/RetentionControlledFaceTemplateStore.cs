using System.Security.Cryptography;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;

namespace Cosmix.Application.Attendance.FaceVerification;

/// <summary>Limits biometric use; evidence and keys are always retained. Caller authorization is
/// still required. Never register the undecorated vault for biometric consumers alongside this gate.</summary>
public sealed class RetentionControlledFaceTemplateStore : IFaceTemplateStore, IFaceEvidenceRetirement
{
    private readonly IFaceTemplateStore vault;
    private readonly IFaceStorageRetentionJournal journal;
    private readonly FaceStorageRetentionOptions options;
    private readonly TimeProvider clock;
    public bool IsConfigured => ValidOptions(options) && vault.IsConfigured && journal.IsConfigured;

    public RetentionControlledFaceTemplateStore(IFaceTemplateStore vault, IFaceStorageRetentionJournal journal,
        FaceStorageRetentionOptions options, TimeProvider clock)
    {
        this.vault = vault;
        this.journal = journal;
        this.options = options with { };
        this.clock = clock;
    }

    public async Task<PrivateFileReference> WriteAsync(byte[] template, string purpose, CancellationToken ct)
    {
        RequireConfigured();
        var capture = RequirePurpose(purpose);
        if (template is not { Length: > 0 and <= 1048576 }) throw Unavailable();
        var now = clock.GetUtcNow();
        DateTimeOffset boundary;
        try { boundary = now.AddSeconds((capture ? options.CaptureUseLifetimeSeconds : options.EnrollmentUseLifetimeSeconds)!.Value); }
        catch (ArgumentOutOfRangeException) { throw Unavailable(); }
        ct.ThrowIfCancellationRequested();
        var owned = template.ToArray();
        try
        {
            var reference = await vault.WriteAsync(owned, purpose, ct);
            RequireReference(reference);
            if (reference.SizeBytes != owned.Length || reference.Sha256 != Convert.ToHexString(SHA256.HashData(owned)))
                throw Unavailable();
            return await journal.WithObjectLockAsync(reference, async token =>
            {
                if ((await journal.ReadAsync(reference, token)).Count != 0) throw Unavailable();
                await journal.AppendAsync(new(1, reference, purpose, FaceStorageRetentionEventKind.Stored,
                    now, boundary, options.ApprovalReference), token);
                token.ThrowIfCancellationRequested();
                return reference;
            }, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { ct.ThrowIfCancellationRequested(); throw Unavailable(); }
        finally { CryptographicOperations.ZeroMemory(owned); }
    }

    public async Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        RequireConfigured(); RequireReference(reference); RequirePurpose(purpose);
        try
        {
            return await journal.WithObjectLockAsync(reference, async token =>
            {
                var events = await BoundEvents(reference, purpose, token);
                var first = events[0];
                RequireUse(events, clock.GetUtcNow());
                byte[]? bytes = null;
                try
                {
                    bytes = await vault.ReadAsync(reference, purpose, token);
                    if (bytes.Length != reference.SizeBytes || Convert.ToHexString(SHA256.HashData(bytes)) != reference.Sha256)
                        throw Unavailable();
                    RequireUse(events, clock.GetUtcNow());
                    token.ThrowIfCancellationRequested();
                    var preparedAt = clock.GetUtcNow();
                    RequireUse(events, preparedAt);
                    await journal.AppendAsync(new(events.Count + 1, reference, purpose, FaceStorageRetentionEventKind.UseReleasePrepared,
                        preparedAt, first.UseUntilUtc, first.ApprovalReference), token);
                    // Metadata persistence can itself outlive the boundary or cancellation.
                    var releasedAt = clock.GetUtcNow();
                    if (releasedAt < preparedAt) throw Unavailable();
                    RequireUse(events, releasedAt);
                    token.ThrowIfCancellationRequested();
                    return bytes;
                }
                catch
                {
                    if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
                    throw;
                }
            }, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (AuthenticationFailure error) when (error.Code is "face_evidence_retired" or "face_evidence_use_expired") { throw; }
        catch (Exception) { ct.ThrowIfCancellationRequested(); throw Unavailable(); }
    }

    /// <summary>Call only with a server-owned, committed terminal operation. This closes storage use;
    /// it is not a database commit, physical deletion, actor authorization or recall of released bytes.</summary>
    public async Task RetireAsync(PrivateFileReference reference, string purpose,
        FaceStorageRetentionReason reason, string operationKey, CancellationToken ct)
    {
        RequireConfigured(); RequireReference(reference);
        var capture = RequirePurpose(purpose);
        if (!Enum.IsDefined(reason) || capture != (reason == FaceStorageRetentionReason.CaptureCompleted) ||
            operationKey is not { Length: >= 8 and <= 100 } ||
            !operationKey.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or ':' or '-')) throw Unavailable();
        try
        {
            await journal.WithObjectLockAsync(reference, async token =>
            {
                var events = await BoundEvents(reference, purpose, token);
                if (events[^1].Kind == FaceStorageRetentionEventKind.Retired) return true;
                var first = events[0];
                var now = clock.GetUtcNow();
                if (now < events[^1].OccurredAtUtc) throw Unavailable();
                await journal.AppendAsync(new(events.Count + 1, reference, purpose, FaceStorageRetentionEventKind.Retired,
                    now, first.UseUntilUtc, first.ApprovalReference, reason, operationKey), token);
                return true;
            }, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (AuthenticationFailure error) when (error.Code is "face_evidence_retired" or "face_evidence_use_expired") { throw; }
        catch (Exception) { ct.ThrowIfCancellationRequested(); throw Unavailable(); }
    }

    private async Task<IReadOnlyList<FaceStorageRetentionEvent>> BoundEvents(PrivateFileReference reference,
        string purpose, CancellationToken ct)
    {
        var events = await journal.ReadAsync(reference, ct);
        if (events.Count is < 1 or > 10000) throw Unavailable();
        var first = events[0];
        var lifetime = RequirePurpose(purpose) ? options.CaptureUseLifetimeSeconds : options.EnrollmentUseLifetimeSeconds;
        if (first.Kind != FaceStorageRetentionEventKind.Stored || first.UseUntilUtc <= first.OccurredAtUtc ||
            first.UseUntilUtc - first.OccurredAtUtc > TimeSpan.FromSeconds(lifetime!.Value) ||
            first.ApprovalReference != options.ApprovalReference) throw Unavailable();
        var previousAt = first.OccurredAtUtc;
        var retired = false;
        for (var i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Sequence != i + 1 || e.Reference != reference || e.Purpose != purpose ||
                e.UseUntilUtc != first.UseUntilUtc || e.ApprovalReference != first.ApprovalReference ||
                e.OccurredAtUtc < previousAt || !Enum.IsDefined(e.Kind) || retired ||
                e.Kind == FaceStorageRetentionEventKind.UseReleasePrepared && e.OccurredAtUtc >= e.UseUntilUtc ||
                i > 0 && e.Kind == FaceStorageRetentionEventKind.Stored) throw Unavailable();
            if (e.Kind == FaceStorageRetentionEventKind.Retired)
            {
                if (e.RetirementReason is null || !Enum.IsDefined(e.RetirementReason.Value) ||
                    RequirePurpose(purpose) != (e.RetirementReason == FaceStorageRetentionReason.CaptureCompleted) ||
                    e.OperationKey is not { Length: >= 8 and <= 100 } ||
                    !e.OperationKey.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or ':' or '-')) throw Unavailable();
                retired = true;
            }
            else if (e.RetirementReason is not null || e.OperationKey is not null) throw Unavailable();
            previousAt = e.OccurredAtUtc;
        }
        return events;
    }

    private static void RequireUse(IReadOnlyList<FaceStorageRetentionEvent> events, DateTimeOffset now)
    {
        if (events[^1].Kind == FaceStorageRetentionEventKind.Retired)
            throw new AuthenticationFailure("face_evidence_retired", 409);
        if (now < events[^1].OccurredAtUtc) throw Unavailable();
        if (now >= events[0].UseUntilUtc) throw new AuthenticationFailure("face_evidence_use_expired", 409);
    }
    private void RequireConfigured() { if (!IsConfigured) throw Unavailable(); }
    private static bool ValidOptions(FaceStorageRetentionOptions value) => value.Enabled && value.AuthorityConfirmed &&
        value.ApprovalReference is { Length: >= 1 and <= 1000 } && !string.IsNullOrWhiteSpace(value.ApprovalReference) &&
        !value.ApprovalReference.Any(char.IsControl) && value.CaptureUseLifetimeSeconds is > 0 && value.EnrollmentUseLifetimeSeconds is > 0;

    internal static bool RequirePurpose(string purpose)
    {
        var parts = purpose?.Split(':');
        if (parts is null || parts.Length is not (4 or 6) || parts[0] != "attendance" || parts[1] != "face" ||
            parts[2] != "template" || !Id(parts[3]) || parts.Length == 6 && (parts[4] != "capture" || !Id(parts[5])))
            throw Unavailable();
        return parts.Length == 6;
    }
    private static bool Id(string? value) => Guid.TryParseExact(value, "N", out var id) && id != Guid.Empty && value == id.ToString("N");
    private static void RequireReference(PrivateFileReference reference)
    {
        if (reference is null || reference.StorageKey is not { Length: 36 } ||
            !reference.StorageKey.EndsWith(".enc", StringComparison.Ordinal) || !Id(reference.StorageKey[..32]) ||
            !Id(reference.ObjectVersion) || reference.Sha256 is not { Length: 64 } ||
            !reference.Sha256.All(char.IsAsciiHexDigitUpper) || reference.KeyReference is not { Length: >= 1 and <= 80 } ||
            !reference.KeyReference.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-') ||
            reference.SizeBytes is < 1 or > 1048576) throw Unavailable();
    }
    private static AuthenticationFailure Unavailable() => new("face_retention_unavailable", 503);
}
