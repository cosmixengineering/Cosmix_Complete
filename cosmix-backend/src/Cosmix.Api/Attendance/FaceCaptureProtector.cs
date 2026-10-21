using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.FaceVerification;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Cosmix.Api.Attendance;

public sealed class FaceCaptureProtector(IDataProtectionProvider provider, TimeProvider clock) : IFaceCaptureProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("Cosmix.Attendance.FaceCapture.v1");
    private static readonly JsonSerializerOptions Json = new()
    { MaxDepth = 12, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public string Protect(FaceCaptureEnvelope envelope)
    {
        Validate(envelope, envelope.Binding);
        var token = protector.Protect(JsonSerializer.Serialize(envelope, Json));
        if (token.Length > 4096) throw AttendanceOperation.Conflict("face_capture_binding_too_large");
        return token;
    }
    public FaceCaptureEnvelope Unprotect(string token, FaceCaptureBinding binding)
    {
        FaceCaptureEnvelope envelope;
        try
        {
            if (token is null || token.Length is < 1 or > 4096 ||
                WebEncoders.Base64UrlEncode(WebEncoders.Base64UrlDecode(token)) != token)
                throw AttendanceOperation.Invalid("invalid_face_capture");
            envelope = JsonSerializer.Deserialize<FaceCaptureEnvelope>(protector.Unprotect(token), Json)
                ?? throw AttendanceOperation.Invalid("invalid_face_capture");
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException or ArgumentException)
        { throw AttendanceOperation.Invalid("invalid_face_capture"); }
        Validate(envelope, binding);
        return envelope;
    }
    private void Validate(FaceCaptureEnvelope envelope, FaceCaptureBinding binding)
    {
        var now = clock.GetUtcNow();
        if (envelope.Version != 1 || envelope.CaptureId == Guid.Empty || envelope.Binding != binding ||
            binding.AccountId == Guid.Empty || binding.SessionId == Guid.Empty || binding.EmployeeId == Guid.Empty ||
            binding.DeviceId == Guid.Empty || binding.AttemptId == Guid.Empty ||
            binding.AttemptId.HasValue != (binding.ChallengeSha256 is not null) ||
            binding.ChallengeSha256 is not null && (binding.ChallengeSha256.Length != 64 || !binding.ChallengeSha256.All(char.IsAsciiHexDigit)) ||
            envelope.IssuedAtUtc == default || envelope.IssuedAtUtc.Offset != TimeSpan.Zero || envelope.IssuedAtUtc > now ||
            envelope.ExpiresAtUtc.Offset != TimeSpan.Zero || envelope.ExpiresAtUtc <= envelope.IssuedAtUtc ||
            envelope.ExpiresAtUtc - envelope.IssuedAtUtc > TimeSpan.FromMinutes(5) ||
            envelope.Image is null || envelope.Image.SizeBytes is < 1 or > 524288 ||
            envelope.Image.Sha256 is not { Length: 64 } || !envelope.Image.Sha256.All(char.IsAsciiHexDigit) ||
            envelope.MediaType is not ("image/png" or "image/jpeg") ||
            envelope.OriginalResources is null || envelope.OriginalResources.Count > 16)
            throw AttendanceOperation.Invalid("invalid_face_capture");
        if (envelope.ExpiresAtUtc <= now) throw AttendanceOperation.Conflict("face_capture_expired");
    }
}
