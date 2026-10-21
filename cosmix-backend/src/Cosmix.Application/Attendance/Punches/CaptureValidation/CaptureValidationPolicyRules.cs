using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;

namespace Cosmix.Application.Attendance.Punches.CaptureValidation;

public static class CaptureValidationPolicyRules
{
    public const int MaximumTimelineRows = 512;
    public const int MaximumRecentRows = 512;

    public static void Validate(CaptureValidationSettings settings)
    {
        if (settings is null || settings.Duplicate?.Enabled is null || settings.ActiveDuration?.Enabled is null ||
            settings.Travel?.Enabled is null || settings.Device?.Enabled is null)
            throw AttendanceOperation.Invalid("capture_policy_groups_must_be_explicit");
        Limit(settings.Duplicate.Enabled.Value, settings.Duplicate.ToleranceSeconds);
        Limit(settings.ActiveDuration.Enabled.Value, settings.ActiveDuration.MaximumMinutes);
        Limit(settings.Travel.Enabled.Value, settings.Travel.LookbackSeconds);
        Limit(settings.Travel.Enabled.Value, settings.Travel.MaximumMetersPerSecond);
        Limit(settings.Travel.Enabled.Value, settings.Travel.MaximumAccuracyMeters);
        if (settings.Duplicate.ToleranceSeconds > TimeSpan.MaxValue.Ticks / (decimal)TimeSpan.TicksPerSecond ||
            settings.Travel.LookbackSeconds > TimeSpan.MaxValue.Ticks / (decimal)TimeSpan.TicksPerSecond)
            throw AttendanceOperation.Invalid("capture_policy_interval_out_of_range");
        var device = settings.Device;
        if (device.Enabled == false)
        {
            if (device.ApprovedCodeSetReference is not null || device.RiskMappings is { Count: > 0 })
                throw AttendanceOperation.Invalid("capture_policy_disabled_group_has_settings");
        }
        else if (!Text(device.ApprovedCodeSetReference, 200) || device.RiskMappings is not { Count: > 0 and <= 100 } ||
            device.RiskMappings.Any(x => x is null || !Text(x.ObservationCode, 100) ||
                x.RiskKind is not ("device_integrity" or "mock_location")) ||
            device.RiskMappings.Select(x => x.ObservationCode).Distinct(StringComparer.Ordinal).Count() != device.RiskMappings.Count)
            throw AttendanceOperation.Invalid("capture_policy_approved_device_mapping_required");
    }
    private static void Limit(bool enabled, decimal? value)
    {
        if (enabled ? value is null or <= 0 : value is not null)
            throw AttendanceOperation.Invalid("capture_policy_explicit_numeric_limit_required");
    }
    public static bool Text(string? value, int length) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= length && value.Trim() == value && !value.Any(char.IsControl);
    public static bool Hash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    public static CaptureValidationSettings Freeze(CaptureValidationSettings settings)
    {
        Validate(settings);
        return settings with { Device = settings.Device with { RiskMappings = settings.Device.RiskMappings is null ? null :
            Array.AsReadOnly(settings.Device.RiskMappings.OrderBy(x => x.ObservationCode, StringComparer.Ordinal).ToArray()) } };
    }
    public static void ValidateEntry(CaptureValidationPolicyEntry entry)
    {
        if (entry is null || entry.Id == Guid.Empty || entry.Revision < 1 ||
            (entry.Revision == 1 ? entry.SupersedesVersionId is not null : !entry.SupersedesVersionId.HasValue || entry.SupersedesVersionId == Guid.Empty) ||
            entry.EffectiveFromUtc.Offset != TimeSpan.Zero || entry.PublishedAtUtc.Offset != TimeSpan.Zero ||
            entry.EffectiveFromUtc < entry.PublishedAtUtc || !Text(entry.PublishedBy, 200) ||
            !Text(entry.ApprovalReference, 200) || !Text(entry.Reason, 1000) ||
            !Hash(entry.SourceFingerprint) || SourceFingerprint(entry) != entry.SourceFingerprint)
            throw AttendanceOperation.Conflict("capture_policy_source_conflict");
        Validate(entry.Settings);
    }
    public static void ValidateTimeline(IReadOnlyList<CaptureValidationPolicyEntry> rows)
    {
        if (rows is null || rows.Count > MaximumTimelineRows || rows.Any(x => x is null) ||
            rows.Select(x => x.Id).Distinct().Count() != rows.Count)
            throw AttendanceOperation.Conflict("capture_policy_history_conflict");
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; ValidateEntry(row);
            if (row.Revision != i + 1 || row.SupersedesVersionId != (i == 0 ? null : rows[i - 1].Id) ||
                i > 0 && (row.PublishedAtUtc < rows[i - 1].PublishedAtUtc || row.EffectiveFromUtc <= rows[i - 1].EffectiveFromUtc))
                throw AttendanceOperation.Conflict("capture_policy_history_conflict");
        }
    }
    public static string SourceFingerprint(CaptureValidationPolicyEntry entry) => Digest(new
    {
        SchemaVersion = 1, entry.Id, entry.Revision, entry.SupersedesVersionId, entry.EffectiveFromUtc,
        entry.PublishedAtUtc, entry.PublishedBy, entry.ApprovalReference,
        Settings = Freeze(entry.Settings), entry.Reason
    });
    public static string TimelineFingerprint(IReadOnlyList<CaptureValidationPolicyEntry> rows) =>
        Digest(rows.Select(x => new { x.Id, x.Revision, x.SupersedesVersionId, x.SourceFingerprint }).ToArray());
    public static CaptureValidationPolicyEntry? Effective(IReadOnlyList<CaptureValidationPolicyEntry> rows,
        DateTimeOffset atUtc)
    {
        ValidateTimeline(rows);
        return rows.LastOrDefault(x => x.EffectiveFromUtc <= atUtc && x.PublishedAtUtc <= atUtc);
    }
    private static string Digest(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
