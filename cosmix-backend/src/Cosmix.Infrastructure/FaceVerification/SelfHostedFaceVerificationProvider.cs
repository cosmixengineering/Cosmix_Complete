using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Authentication;

namespace Cosmix.Infrastructure.FaceVerification;

public sealed record SelfHostedFaceOptions
{
    public const string SectionName = "SelfHostedFace";
    public bool Enabled { get; init; }
    public double? MinimumSimilarity { get; init; }
}

/// <summary>Scoped adapter. Evidence authority belongs to the server reader, not to an HTTP image or client flag.</summary>
public sealed class SelfHostedFaceVerificationProvider : IFaceVerificationProvider
{
    public const string ProviderName = "CompreFace+SilentFace";
    private readonly CompreFaceClient client;
    private readonly SilentFaceLivenessService liveness;
    private readonly IFaceCaptureEvidenceReader evidence;
    private readonly double minimumSimilarity;
    public FaceProviderCapability Capability { get; }

    public SelfHostedFaceVerificationProvider(SelfHostedFaceOptions options, CompreFaceClient client,
        SilentFaceLivenessService liveness, IFaceCaptureEvidenceReader evidence)
    {
        this.client = client;
        this.liveness = liveness;
        this.evidence = evidence;
        var configured = options.Enabled &&
            options.MinimumSimilarity is double threshold && double.IsFinite(threshold) && threshold is > 0 and <= 1 &&
            liveness.IsConfigured;
        minimumSimilarity = options.MinimumSimilarity ?? double.NaN;
        // Enrollment compatibility changes with model/export semantics, thresholds or deployed face engine.
        var version = "self-hosted-unconfigured-v1";
        if (configured)
        {
            var identity = JsonSerializer.SerializeToUtf8Bytes(new
            {
                Contract = "reference-image-v1", client.DeploymentVersion, client.MinimumDetectionProbability,
                liveness.ContractDigest, options.MinimumSimilarity
            });
            version = "self-hosted-v1-" + Convert.ToHexString(SHA256.HashData(identity));
        }
        Capability = new(configured, ProviderName, version, configured ? "configured" : "face_provider_not_configured");
    }

    public async Task<FaceProviderEnrollmentResult> EnrollAsync(FaceProviderEnrollmentInput input, CancellationToken ct)
    {
        if (!Capability.Available) throw new AuthenticationFailure("face_provider_not_configured", 503);
        if (!ValidActor(input.ActorAccountId, input.ActorSessionId, input.EmployeeId, input.DeviceId) ||
            input.IdempotencyKey is not { Length: 43 } || !input.IdempotencyKey.StartsWith("enrollment:", StringComparison.Ordinal) ||
            !Guid.TryParseExact(input.IdempotencyKey[11..], "N", out var receipt) || receipt == Guid.Empty)
            return EnrollmentFailure(input, "face_capture_binding_invalid");
        byte[]? image = null;
        try
        {
            var binding = new FaceCaptureBinding(input.ActorAccountId!.Value, input.ActorSessionId!.Value,
                input.EmployeeId, input.DeviceId, null, null);
            image = await evidence.ReadAsync(input.EvidenceToken, binding, input.IdempotencyKey, ct);
            _ = SelfHostedFaceImage.Inspect(image);
            var detected = await client.DetectAsync(image, ct);
            var live = await liveness.VerifyAsync(image, detected.Bounds, ct);
            ct.ThrowIfCancellationRequested();
            if (!live.Passed) return EnrollmentFailure(input, "face_liveness_failed");
            // This is an original reference image, not an embedding. FaceService encrypts it in the dedicated vault.
            var referenceImage = image.ToArray();
            return new(true, Capability.Provider, Capability.Version, input.EmployeeId, input.DeviceId,
                referenceImage, "reference_image_enrolled");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new AuthenticationFailure("face_provider_timeout", 503); }
        catch (AuthenticationFailure error) when (error.StatusCode == 503) { throw new AuthenticationFailure("face_provider_unavailable", 503); }
        catch (AuthenticationFailure) { return EnrollmentFailure(input, "face_provider_rejected"); }
        catch (HttpRequestException) { throw new AuthenticationFailure("face_provider_unavailable", 503); }
        catch (JsonException) { throw new AuthenticationFailure("face_provider_invalid_result", 503); }
        finally { if (image is not null) CryptographicOperations.ZeroMemory(image); }
    }

    public async Task<FaceProviderVerificationResult> VerifyAsync(FaceProviderVerificationInput input, CancellationToken ct)
    {
        var challengeDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.Challenge ?? "")));
        FaceProviderVerificationResult Failure(string reason, bool available = false, bool live = false) =>
            new(available, live, false, Capability.Provider, Capability.Version, input.AttemptId, input.EmployeeId,
                input.DeviceId, challengeDigest, reason);
        if (!Capability.Available) return Failure("face_provider_not_configured");
        if (!ValidActor(input.ActorAccountId, input.ActorSessionId, input.EmployeeId, input.DeviceId) || input.AttemptId == Guid.Empty ||
            !CompreFaceClient.ValidText(input.Challenge, 128)) return Failure("face_capture_binding_invalid");
        byte[]? image = null;
        try
        {
            _ = SelfHostedFaceImage.Inspect(input.Template);
            var binding = new FaceCaptureBinding(input.ActorAccountId!.Value, input.ActorSessionId!.Value,
                input.EmployeeId, input.DeviceId, input.AttemptId, challengeDigest);
            image = await evidence.ReadAsync(input.EvidenceToken, binding, $"verification:{input.AttemptId:N}", ct);
            _ = SelfHostedFaceImage.Inspect(image);
            var detected = await client.DetectAsync(image, ct);
            var live = await liveness.VerifyAsync(image, detected.Bounds, ct);
            if (!live.Passed) return Failure("face_liveness_failed", available: true);
            var comparison = await client.VerifyAsync(image, input.Template, ct);
            // Bind the face passed to liveness to the complete verification response from the same bytes.
            if (comparison.SourceBounds != detected.Bounds) return Failure("face_provider_invalid_result");
            ct.ThrowIfCancellationRequested();
            var matches = comparison.Similarity >= minimumSimilarity;
            return new(true, true, matches, Capability.Provider, Capability.Version, input.AttemptId, input.EmployeeId,
                input.DeviceId, challengeDigest, matches ? "verified" : "face_match_failed");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Failure("face_provider_unavailable"); }
        catch (AuthenticationFailure error) when (error.StatusCode == 409) { return Failure(error.Code, available: true); }
        catch (AuthenticationFailure error) when (error.StatusCode == 503) { return Failure("face_provider_unavailable"); }
        catch (AuthenticationFailure) { return Failure("face_provider_rejected"); }
        catch (HttpRequestException) { return Failure("face_provider_unavailable"); }
        catch (JsonException) { return Failure("face_provider_invalid_result"); }
        finally { if (image is not null) CryptographicOperations.ZeroMemory(image); }
    }

    private FaceProviderEnrollmentResult EnrollmentFailure(FaceProviderEnrollmentInput input, string reason) =>
        new(false, Capability.Provider, Capability.Version, input.EmployeeId, input.DeviceId, null, reason);
    private static bool ValidActor(Guid? account, Guid? session, Guid employee, Guid device) =>
        account is Guid a && a != Guid.Empty && session is Guid s && s != Guid.Empty && employee != Guid.Empty && device != Guid.Empty;
}
