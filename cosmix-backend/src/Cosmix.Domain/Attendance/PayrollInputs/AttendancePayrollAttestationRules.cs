using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cosmix.Domain.Attendance.PayrollInputs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record AttendancePayrollAttestationLine(
    [property: JsonRequired] string Key,
    [property: JsonRequired] decimal Amount,
    [property: JsonRequired] string Currency);

/// <summary>Validates manual assertions; never verifies an external payroll system or releases funds.</summary>
public static class AttendancePayrollAttestationRules
{
    public const int MaximumLines = 732;
    public static int Step(string status) => status switch
    {
        "handoff" => 1, "posted" => 2, "settled" => 3,
        _ => throw new ArgumentException("Unknown attestation status.", nameof(status))
    };
    public static void RequireLines(IReadOnlyList<AttendancePayrollAttestationLine>? source,
        IReadOnlyList<AttendancePayrollAttestationLine>? claimed) => RequireExactLines(source, claimed, false);
    public static void RequireHandoffLines(IReadOnlyList<AttendancePayrollAttestationLine>? source,
        IReadOnlyList<AttendancePayrollAttestationLine>? claimed) => RequireExactLines(source, claimed, true);
    private static void RequireExactLines(IReadOnlyList<AttendancePayrollAttestationLine>? source,
        IReadOnlyList<AttendancePayrollAttestationLine>? claimed, bool allowVerifiedEmpty)
    {
        ValidateLines(source, allowVerifiedEmpty); ValidateLines(claimed, allowVerifiedEmpty);
        var expected = source!; var actual = claimed!;
        if (expected.Count != actual.Count || !expected.OrderBy(x => x.Key, StringComparer.Ordinal)
            .SequenceEqual(actual.OrderBy(x => x.Key, StringComparer.Ordinal)))
            throw new ArgumentException("The complete exact source line set is required.");
    }
    private static void ValidateLines(IReadOnlyList<AttendancePayrollAttestationLine>? lines, bool allowVerifiedEmpty = false)
    {
        if (lines is null || lines.Count > MaximumLines || !allowVerifiedEmpty && lines.Count == 0 ||
            lines.Any(x => x is null || !Text(x.Key, 256) || x.Key != x.Key.Trim() ||
                x.Amount < 0 || decimal.Round(x.Amount, 2) != x.Amount || x.Currency != "PKR") ||
            lines.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() != lines.Count)
            throw new ArgumentException("Missing, duplicate or invalid financial lines.");
    }
    public static string LineFingerprint(IReadOnlyList<AttendancePayrollAttestationLine> lines) => Fingerprint(lines, false);
    public static string HandoffLineFingerprint(IReadOnlyList<AttendancePayrollAttestationLine> lines) => Fingerprint(lines, true);
    private static string Fingerprint(IReadOnlyList<AttendancePayrollAttestationLine> lines, bool allowVerifiedEmpty)
    {
        ValidateLines(lines, allowVerifiedEmpty);
        return Digest(lines.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new { x.Key, Amount = x.Amount.ToString("G29", CultureInfo.InvariantCulture), x.Currency }).ToArray());
    }
    public static string ExternalReceiptKey(string externalSystem, string externalReference)
    {
        if (!SystemCode(externalSystem) || !Text(externalReference, 200))
            throw new ArgumentException("An explicit external system and employee/month receipt reference are required.");
        return Digest(new { System = externalSystem.Trim().ToUpperInvariant(), Reference = externalReference.Trim() });
    }
    public static void RequireReceipt(string status, DateTimeOffset occurredAtUtc, DateTimeOffset? postedAtUtc,
        string externalSystem, string externalReference, string evidenceReference, string evidenceSha256,
        string statement, DateTimeOffset now)
    {
        var step = Step(status);
        ExternalReceiptKey(externalSystem, externalReference);
        if (!Text(evidenceReference, 500) || !Hash(evidenceSha256) || !Text(statement, 4000) ||
            occurredAtUtc.Offset != TimeSpan.Zero || occurredAtUtc == default || occurredAtUtc > now ||
            (step == 1 ? postedAtUtc is not null : postedAtUtc is null || postedAtUtc.Value == default ||
                postedAtUtc.Value.Offset != TimeSpan.Zero || postedAtUtc > occurredAtUtc))
            throw new ArgumentException("Explicit, dated evidence is required; handoff is never posting proof.");
        if (step == 2 && postedAtUtc != occurredAtUtc)
            throw new ArgumentException("Posted event time must be the actual posting time.");
    }
    public static void RequireSourceTime(DateTimeOffset occurredAtUtc, DateTimeOffset sourceCreatedAtUtc)
    {
        if (occurredAtUtc < sourceCreatedAtUtc)
            throw new ArgumentException("An event cannot predate its exact saved source.");
    }
    public static void RequireTransition(AttendancePayrollAttestation? previous, Guid? expectedPreviousId,
        Guid sourceRevisionId, string status, DateTimeOffset occurredAtUtc, DateTimeOffset? postedAtUtc, string receiptKey)
    {
        var step = Step(status);
        if (sourceRevisionId == Guid.Empty || expectedPreviousId == Guid.Empty ||
            (previous is null ? expectedPreviousId is not null || step != 1 :
                expectedPreviousId != previous.Id || sourceRevisionId != previous.SourceRevisionId ||
                step != previous.Step + 1 || occurredAtUtc < previous.OccurredAtUtc ||
                step == 3 && (postedAtUtc != previous.PostedAtUtc || receiptKey != previous.ExternalReceiptKey)))
            throw new ArgumentException("Stale, replaced or finalized attestation chain.");
    }
    public static bool Hash(string? value) => value is { Length: 64 } &&
        value.All(c => c is (>= '0' and <= '9') or (>= 'A' and <= 'F'));
    public static bool Text(string? value, int limit) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= limit && !value.Any(char.IsControl);
    private static bool SystemCode(string? value) => Text(value, 100) && value!.Trim().All(c =>
        c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '-' or '.');
    private static string Digest(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
