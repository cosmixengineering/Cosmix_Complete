using System.Text.Json;
using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Punches;

public sealed class CaptureValidationPolicyVersion : IAppendOnlyEntity
{
    private CaptureValidationPolicyVersion() { }
    public CaptureValidationPolicyVersion(Guid id, int revision, Guid? supersedesVersionId, DateTimeOffset effectiveFromUtc,
        DateTimeOffset publishedAtUtc, string publishedBy, string approvalReference, string settingsJson,
        string reason, string sourceFingerprint, string protectedEvidenceReference)
    {
        if ((revision == 1) != !supersedesVersionId.HasValue || effectiveFromUtc < publishedAtUtc)
            throw new ArgumentException("A dated policy requires a matching predecessor and cannot predate publication.");
        using var settings = JsonDocument.Parse(settingsJson);
        if (settings.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Policy settings must be a JSON object.");
        Id = ModelGuard.Id(id, nameof(id)); Revision = AttendanceModelGuard.Revision(revision);
        SupersedesVersionId = AttendanceModelGuard.OptionalId(supersedesVersionId, nameof(supersedesVersionId));
        if (SupersedesVersionId == Id) throw new ArgumentException("A policy cannot supersede itself.");
        EffectiveFromUtc = ModelGuard.Utc(effectiveFromUtc); PublishedAtUtc = ModelGuard.Utc(publishedAtUtc);
        PublishedBy = ModelGuard.Required(publishedBy, 200, nameof(publishedBy));
        ApprovalReference = ModelGuard.Required(approvalReference, 200, nameof(approvalReference));
        SettingsJson = ModelGuard.Required(settingsJson, 32768, nameof(settingsJson));
        Reason = ModelGuard.Required(reason, 1000, nameof(reason));
        SourceFingerprint = CaptureValidationModelGuard.Hash(sourceFingerprint);
        ProtectedEvidenceReference = ModelGuard.Required(protectedEvidenceReference, 500, nameof(protectedEvidenceReference));
    }
    public Guid Id { get; private set; }
    public int Revision { get; private set; }
    public Guid? SupersedesVersionId { get; private set; }
    public DateTimeOffset EffectiveFromUtc { get; private set; }
    public DateTimeOffset PublishedAtUtc { get; private set; }
    public string PublishedBy { get; private set; } = "";
    public string ApprovalReference { get; private set; } = "";
    public string SettingsJson { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public string SourceFingerprint { get; private set; } = "";
    public string ProtectedEvidenceReference { get; private set; } = "";
}

internal static class CaptureValidationModelGuard
{
    public static string Hash(string value)
    {
        value = ModelGuard.Required(value, 64, nameof(value));
        if (value.Length != 64 || value != value.ToUpperInvariant() || !value.All(Uri.IsHexDigit))
            throw new ArgumentException("An uppercase SHA-256 fingerprint is required.");
        return value;
    }
}
