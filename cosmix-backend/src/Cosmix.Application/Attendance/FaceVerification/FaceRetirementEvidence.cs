using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;

namespace Cosmix.Application.Attendance.FaceVerification;

public static class FaceRetirementEvidence
{
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static string IntentPurpose(string type, Guid subject) => $"attendance:face:retirement:{type}:{subject:N}:intent";
    public static string OutcomePurpose(Guid intent) => $"attendance:face:retirement:{intent:N}:outcome";
    public static bool Same<T>(T left, T right) => JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);

    public static async Task<string> WriteAsync<T>(IPrivateFileStore files, T value, string purpose, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        try
        {
            if (bytes.Length is < 1 or > 65536) throw Unavailable();
            var reference = await files.WriteAsync(bytes, purpose, ct);
            if (reference.SizeBytes != bytes.Length || reference.Sha256 != Convert.ToHexString(SHA256.HashData(bytes)))
                throw Unavailable();
            var serialized = JsonSerializer.Serialize(reference);
            return serialized.Length <= 500 ? serialized : throw Unavailable();
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public static async Task<T> ReadAsync<T>(IPrivateFileStore files, string? serialized, string purpose, CancellationToken ct)
    {
        if (serialized is null || serialized.Length > 500) throw Unavailable();
        var reference = Deserialize<PrivateFileReference>(System.Text.Encoding.UTF8.GetBytes(serialized));
        if (reference.SizeBytes is < 1 or > 65536) throw Unavailable();
        var bytes = await files.ReadAsync(reference, purpose, ct);
        try
        {
            if (bytes.Length != reference.SizeBytes || Convert.ToHexString(SHA256.HashData(bytes)) != reference.Sha256)
                throw Unavailable();
            return Deserialize<T>(bytes);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public static T Deserialize<T>(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        RejectDuplicates(document.RootElement);
        return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw Unavailable();
    }
    private static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in value.EnumerateObject())
            { if (!names.Add(p.Name)) throw Unavailable(); RejectDuplicates(p.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RejectDuplicates(item);
    }

    public static void RequireIntent(FaceRetirementIntent value)
    {
        if (value.Version != 1 || value.EmployeeId == Guid.Empty || value.SubjectId == Guid.Empty ||
            !Enum.IsDefined(value.Reason)) throw Unavailable();
        var capture = RetentionControlledFaceTemplateStore.RequirePurpose(value.Purpose);
        var expectedPurpose = $"attendance:face:template:{value.EmployeeId:N}" + (capture ? $":capture:{value.SubjectId:N}" : "");
        var r = value.Reference;
        if (value.Purpose != expectedPurpose || r is null || r.StorageKey is not { Length: 36 } ||
            !r.StorageKey.EndsWith(".enc", StringComparison.Ordinal) || !Id(r.StorageKey[..32]) || !Id(r.ObjectVersion) ||
            r.Sha256 is not { Length: 64 } || !r.Sha256.All(char.IsAsciiHexDigitUpper) || r.SizeBytes is < 1 or > 1048576 ||
            r.KeyReference is not { Length: >= 1 and <= 80 } || !r.KeyReference.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
            throw Unavailable();
        if (capture)
        {
            var e = value.Capture;
            if (value.Reason != FaceStorageRetentionReason.CaptureCompleted || value.RevokedAtUtc is not null ||
                value.ReplacementEnrollmentId is not null || e is null || e.Version != 1 || e.CaptureId != value.SubjectId ||
                e.Image != r || e.Binding is null || e.Binding.EmployeeId != value.EmployeeId ||
                e.Binding.AccountId == Guid.Empty || e.Binding.SessionId == Guid.Empty || e.Binding.DeviceId == Guid.Empty ||
                e.IssuedAtUtc.Offset != TimeSpan.Zero || e.ExpiresAtUtc.Offset != TimeSpan.Zero || e.ExpiresAtUtc <= e.IssuedAtUtc ||
                e.MediaType is not ("image/png" or "image/jpeg") || value.TokenSha256 is not { Length: 64 } ||
                !value.TokenSha256.All(char.IsAsciiHexDigitUpper)) throw Unavailable();
            var prefix = e.Binding.AttemptId.HasValue ? "verification:" : "enrollment:";
            if (value.OperationKey is null || !value.OperationKey.StartsWith(prefix, StringComparison.Ordinal) ||
                !Id(value.OperationKey[prefix.Length..]) || e.Binding.AttemptId.HasValue &&
                (value.OperationKey != $"verification:{e.Binding.AttemptId:N}" || e.Binding.ChallengeSha256 is not { Length: 64 } ||
                 !e.Binding.ChallengeSha256.All(char.IsAsciiHexDigitUpper)) ||
                !e.Binding.AttemptId.HasValue && e.Binding.ChallengeSha256 is not null) throw Unavailable();
        }
        else if (value.Reason == FaceStorageRetentionReason.CaptureCompleted || value.Capture is not null || value.TokenSha256 is not null ||
            value.OperationKey != $"enrollment:{value.SubjectId:N}" || value.RevokedAtUtc is null || value.RevokedAtUtc.Value.Offset != TimeSpan.Zero ||
            (value.Reason == FaceStorageRetentionReason.EnrollmentReplaced) != value.ReplacementEnrollmentId.HasValue ||
            value.ReplacementEnrollmentId == Guid.Empty || value.ReplacementEnrollmentId == value.SubjectId) throw Unavailable();
    }
    private static bool Id(string? value) => Guid.TryParseExact(value, "N", out var id) && id != Guid.Empty && value == id.ToString("N");
    public static AuthenticationFailure Unavailable() => new("face_retirement_unavailable", 503);
}
