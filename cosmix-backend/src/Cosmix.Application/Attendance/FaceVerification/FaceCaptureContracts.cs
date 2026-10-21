using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;

namespace Cosmix.Application.Attendance.FaceVerification;

// Internal server bindings, not values accepted from a mobile request.
public sealed record FaceCaptureBinding(Guid AccountId, Guid SessionId, Guid EmployeeId, Guid DeviceId,
    Guid? AttemptId, string? ChallengeSha256);
public sealed record FaceCaptureEnvelope(int Version, Guid CaptureId, FaceCaptureBinding Binding,
    PrivateFileReference Image, string MediaType, DateTimeOffset IssuedAtUtc, DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<AccessResource> OriginalResources);
public interface IFaceCaptureProtector
{
    string Protect(FaceCaptureEnvelope envelope);
    FaceCaptureEnvelope Unprotect(string token, FaceCaptureBinding binding);
}
public interface IFaceCaptureEvidenceReader
{
    Task<byte[]> ReadAsync(string token, FaceCaptureBinding binding, string operationKey, CancellationToken ct);
}
public sealed class FaceCaptureOptions
{
    public const string SectionName = "FaceCapture";
    public bool Enabled { get; set; }
    public int? LifetimeSeconds { get; set; }
    public string? ApprovalReference { get; set; }
    public bool IsConfigured => Enabled && LifetimeSeconds is > 0 and <= 300 &&
        !string.IsNullOrWhiteSpace(ApprovalReference) && ApprovalReference.Length <= 1000;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UploadVerificationFaceCaptureRequest(
    [property: JsonRequired] [Required, StringLength(128)] string Challenge,
    [property: JsonRequired] [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    [property: JsonRequired] [Required, StringLength(699052)] string ImageBase64);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UploadEnrollmentFaceCaptureRequest([property: JsonRequired] Guid RegisteredDeviceId,
    [property: JsonRequired] [Required, StringLength(699052)] string ImageBase64,
    [property: JsonRequired] [Required, StringLength(1000)] string Reason);
public sealed record FaceCaptureResponse(string ProviderEvidenceToken, DateTimeOffset ExpiresAtUtc);
