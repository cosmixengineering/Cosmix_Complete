using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Cosmix.Api.Attendance;

/// <summary>Uses the application's existing ASP.NET protection keys; never creates its own key ring,
/// writes files, substitutes plaintext or treats a protected cursor as a permission grant.</summary>
public sealed class AttendanceCursorProtector(IDataProtectionProvider provider, TimeProvider clock) : IAttendanceCursorProtector
{
    private const int MaximumTokenLength = 4096;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions Json = new()
    { MaxDepth = 8, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private readonly IDataProtector protector = provider.CreateProtector("Cosmix.Attendance.Cursor.v1");

    public string Protect(Guid accountId, string purpose, string filterFingerprint, AttendanceCursorPosition position)
    {
        ValidateBinding(accountId, purpose, filterFingerprint);
        var now = clock.GetUtcNow();
        ValidatePosition(position, now);
        var envelope = new CursorEnvelope(1, accountId, purpose, filterFingerprint.ToUpperInvariant(),
            position, now, now.Add(Lifetime));
        // A missing/unusable key ring propagates failure; there is no unprotected fallback.
        var token = protector.Protect(JsonSerializer.Serialize(envelope, Json));
        if (token.Length > MaximumTokenLength) throw new InvalidOperationException("Attendance cursor exceeds its output bound.");
        return token;
    }

    public AttendanceCursorPosition Unprotect(string token, Guid accountId, string purpose, string filterFingerprint)
    {
        ValidateBinding(accountId, purpose, filterFingerprint);
        if (token is null || token.Length is < 1 or > MaximumTokenLength ||
            token.Any(c => !(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')))
            throw AttendanceOperation.Invalid("invalid_attendance_cursor");
        CursorEnvelope envelope;
        try
        {
            // Reject non-canonical aliases, including altered unused base64 bits or appended padding.
            if (WebEncoders.Base64UrlEncode(WebEncoders.Base64UrlDecode(token)) != token)
                throw AttendanceOperation.Invalid("invalid_attendance_cursor");
            envelope = JsonSerializer.Deserialize<CursorEnvelope>(protector.Unprotect(token), Json)
                ?? throw AttendanceOperation.Invalid("invalid_attendance_cursor");
        }
        catch (Exception failure) when (failure is CryptographicException or FormatException or JsonException or ArgumentException)
        { throw AttendanceOperation.Invalid("invalid_attendance_cursor"); }

        var now = clock.GetUtcNow();
        if (envelope.Version != 1 || envelope.AccountId != accountId ||
            !string.Equals(envelope.Purpose, purpose, StringComparison.Ordinal) ||
            !string.Equals(envelope.FilterFingerprint, filterFingerprint.ToUpperInvariant(), StringComparison.Ordinal) ||
            envelope.IssuedAtUtc == default || envelope.IssuedAtUtc.Offset != TimeSpan.Zero ||
            envelope.ExpiresAtUtc.Offset != TimeSpan.Zero || envelope.IssuedAtUtc > now ||
            envelope.ExpiresAtUtc - envelope.IssuedAtUtc != Lifetime)
            throw AttendanceOperation.Invalid("invalid_attendance_cursor");
        ValidatePosition(envelope.Position, envelope.IssuedAtUtc);
        // Check binding before expiry so another account cannot probe a token's lifetime.
        if (envelope.ExpiresAtUtc <= now) throw AttendanceOperation.Conflict("attendance_cursor_expired");
        return envelope.Position;
    }

    private static void ValidateBinding(Guid accountId, string purpose, string filterFingerprint)
    {
        if (accountId == Guid.Empty || purpose is null || purpose.Length is < 1 or > 100 ||
            purpose.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-' or '_')) ||
            filterFingerprint is not { Length: 64 } || !filterFingerprint.All(char.IsAsciiHexDigit))
            throw AttendanceOperation.Invalid("invalid_attendance_cursor");
    }

    private static void ValidatePosition(AttendanceCursorPosition? position, DateTimeOffset issuedAt)
    {
        if (position is null || position.Id == Guid.Empty || position.CreatedAtUtc == default ||
            position.CreatedAtUtc.Offset != TimeSpan.Zero || position.CreatedAtUtc > issuedAt)
            throw AttendanceOperation.Invalid("invalid_attendance_cursor");
    }

    private sealed record CursorEnvelope(int Version, Guid AccountId, string Purpose, string FilterFingerprint,
        AttendanceCursorPosition Position, DateTimeOffset IssuedAtUtc, DateTimeOffset ExpiresAtUtc);
}
