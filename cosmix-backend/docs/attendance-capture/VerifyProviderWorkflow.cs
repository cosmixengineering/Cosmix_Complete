// Explicit synthetic workflow checks. Mock CompreFace/evidence + real pinned ONNX.
// The low fixture threshold exercises control flow; never copy it to operational settings.
using System.Net;
using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Authentication;
using Cosmix.Infrastructure.FaceVerification;
using OpenCvSharp;

var failures = new List<string>();
var passed = 0;
void Check(bool value, string label)
{
    if (value) passed++;
    else { failures.Add(label); Console.WriteLine("FAIL: " + label); }
}
var directory = args.Single();
SilentFaceLivenessService Model(string name, string hash, double scale, double threshold) => new(new SilentFaceOptions
{
    Enabled = true, Models = new[] { new SilentFaceModel
    {
        Path = Path.Combine(directory, name), Sha256 = hash,
        SourceRevision = "c175efef505bc275f7835cc9aa2aecab305ac184",
        ExportReference = "Reviewed publisher weights release; synthetic workflow harness",
        LicenseReviewReference = "Apache-2.0; deployment source assessment 2026-10-19",
        SemanticsReviewReference = "BGR 0-255 NCHW80x80; logits; live index1; batch fixed1",
        InputName = "input", OutputName = "output", BatchDimensionName = "batch_size",
        InputWidth = 80, InputHeight = 80,
        InputContract = "float32-NCHW-BGR-0_255-OpenCV_INTER_LINEAR-CompreFaceExtent",
        CropKind = SilentFaceCropKind.UpstreamScaledBox, CropScale = scale,
        OutputKind = SilentFaceOutputKind.Logits, ClassCount = 3, LiveClassIndex = 1,
        MinimumLiveProbability = threshold
    } }
});
using var liveFixture = Model("MiniFASNetV1SE.onnx", "ebab7f90c7833fbccd46d3a555410e78d969db5438e169b6524be444862b3676", 4, 0.1);
using var spoofFixture = Model("MiniFASNetV2.onnx", "b32929adc2d9c34b9486f8c4c7bc97c1b69bc0ea9befefc380e4faae4e463907", 2.7, 0.1);
using var strictFixture = Model("MiniFASNetV1SE.onnx", "ebab7f90c7833fbccd46d3a555410e78d969db5438e169b6524be444862b3676", 4, 0.9);
Check(liveFixture.IsConfigured && spoofFixture.IsConfigured && strictFixture.IsConfigured, "real pinned models load");
using var pixels = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(127));
Cv2.ImEncode(".png", pixels, out var image);
var options = new CompreFaceOptions
{
    BaseUri = new Uri("https://10.20.30.40:8000/"), DetectionApiKey = "fixture-detection-key",
    VerificationApiKey = "fixture-verification-key", DeploymentVersion = "synthetic-protocol-only",
    MinimumDetectionProbability = 0.9
};
CompreFaceClient Client(WorkflowTransport transport) => (CompreFaceClient)typeof(CompreFaceClient)
    .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null,
        new[] { typeof(CompreFaceOptions), typeof(HttpMessageHandler) }, null)!.Invoke(new object[] { options, transport });
SelfHostedFaceVerificationProvider Provider(CompreFaceClient client, WorkflowEvidence evidence,
    SilentFaceLivenessService? model = null) => new(new() { Enabled = true, MinimumSimilarity = 0.85 }, client, model ?? liveFixture, evidence);
var enrollment = new FaceProviderEnrollmentInput(Guid.NewGuid(), Guid.NewGuid(), "fixture-capture-token", "fixture",
    "enrollment:" + Guid.NewGuid().ToString("N"), Guid.NewGuid(), Guid.NewGuid());
var input = new FaceProviderVerificationInput(Guid.NewGuid(), enrollment.EmployeeId, enrollment.DeviceId,
    new string('A', 64), "fixture-capture-token", image.ToArray(), "fixture", enrollment.ActorAccountId, enrollment.ActorSessionId);
bool Zeroed(byte[]? bytes) => bytes is not null && bytes.All(b => b == 0);
try
{
    var transport = new WorkflowTransport("success");
    var evidence = new WorkflowEvidence(image);
    using (var client = Client(transport))
    {
        var provider = Provider(client, evidence);
        var result = await provider.EnrollAsync(enrollment, default);
        Check(result.Success && result.Template!.SequenceEqual(image), "enrollment returns its own original reference-image copy");
        Check(Zeroed(evidence.LastBytes) && !ReferenceEquals(result.Template, evidence.LastBytes), "enrollment zeroes the released evidence buffer");
        Check(evidence.LastBinding == new FaceCaptureBinding(enrollment.ActorAccountId!.Value, enrollment.ActorSessionId!.Value,
            enrollment.EmployeeId, enrollment.DeviceId, null, null), "enrollment forwards exact server-owned identity/session/device binding");
        if (result.Template is not null) Array.Clear(result.Template);
    }
    foreach (var mode in new[] { "success", "wrong-person", "changed-bounds" })
    {
        transport = new WorkflowTransport(mode); evidence = new WorkflowEvidence(image);
        using var client = Client(transport);
        var result = await Provider(client, evidence).VerifyAsync(input, default);
        if (mode == "changed-bounds") Check(!result.MatchPassed, "changed remote box cannot substitute the face used for liveness");
        else Check(result.Available && result.LivenessPassed && result.MatchPassed == (mode == "success"), "mock comparison outcome: " + mode);
        Check(transport.Calls == 4 && transport.Comparisons == 1, "verification makes one comparison after all single-face checks: " + mode);
        Check(Zeroed(evidence.LastBytes) && input.Template.SequenceEqual(image), "verification zeroes capture and preserves caller-owned template: " + mode);
        Check(evidence.LastBinding?.ChallengeSha256 == Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.Challenge))) &&
            evidence.LastOperation == "verification:" + input.AttemptId.ToString("N"), "challenge-bound evidence claim: " + mode);
    }
    foreach (var model in new[] { spoofFixture, strictFixture })
    {
        transport = new WorkflowTransport("success"); evidence = new WorkflowEvidence(image);
        using var client = Client(transport);
        var result = await Provider(client, evidence, model).VerifyAsync(input, default);
        Check(result.Available && !result.LivenessPassed && !result.MatchPassed && transport.Comparisons == 0,
            "real rejected liveness verdict never reaches identity comparison");
        Check(Zeroed(evidence.LastBytes), "liveness rejection zeroes capture memory");
    }
    foreach (var mode in new[] { "http-no-face", "http-many-faces", "empty-faces", "many-faces", "low-confidence" })
    {
        transport = new WorkflowTransport(mode); evidence = new WorkflowEvidence(image);
        using var client = Client(transport);
        var recapture = false;
        try { await client.DetectAsync(image); }
        catch (AuthenticationFailure error) { recapture = error.StatusCode == 409 && !error.Message.Contains("do-not-echo"); }
        Check(recapture, "normal face rejection is a sanitized recapture response: " + mode);
        var result = await Provider(client, evidence).VerifyAsync(input, default);
        Check(result.Available && !result.LivenessPassed && !result.MatchPassed && transport.Comparisons == 0,
            "normal face rejection keeps provider available: " + mode);
        try
        {
            var enrolled = await Provider(client, evidence).EnrollAsync(enrollment, default);
            Check(!enrolled.Success && enrolled.Template is null, "enrollment rejects capture without an outage: " + mode);
        }
        catch (AuthenticationFailure) { Check(false, "enrollment rejects capture without an outage: " + mode); }
        Check(Zeroed(evidence.LastBytes), "image rejection zeroes memory: " + mode);
    }
    foreach (var mode in new[] { "wrong-key", "missing-key", "internal", "unknown-error", "duplicate-error", "text-error", "oversized-error", "invalid-coordinate", "network",
        "string-error-code", "fractional-error-code", "missing-error-message", "null-error-message", "extra-error-field", "array-error", "ok-error-body", "wrong-status-no-face", "malformed-many-faces" })
    {
        transport = new WorkflowTransport(mode); evidence = new WorkflowEvidence(image);
        using var client = Client(transport);
        var unavailable = false;
        try { await client.DetectAsync(image); }
        catch (AuthenticationFailure error) { unavailable = error.StatusCode == 503 && !error.Message.Contains("do-not-echo"); }
        Check(unavailable, "credential/outage/malformed protocol remains unavailable: " + mode);
        var result = await Provider(client, evidence).VerifyAsync(input, default);
        Check(!result.Available && !result.MatchPassed && !result.LivenessPassed, "unavailable provider cannot verify: " + mode);
        Check(Zeroed(evidence.LastBytes), "provider failure zeroes capture memory: " + mode);
    }
    foreach (var mode in new[] { "comparison-low-source", "comparison-low-target", "comparison-malformed-matches", "comparison-malformed-score", "comparison-changed-low-box" })
    {
        transport = new WorkflowTransport(mode); evidence = new WorkflowEvidence(image);
        using var client = Client(transport);
        var ordinary = mode is "comparison-low-source" or "comparison-low-target";
        var correctlyClassified = false;
        try { await client.VerifyAsync(image, image); }
        catch (AuthenticationFailure error) { correctlyClassified = error.StatusCode == (ordinary ? 409 : 503); }
        Check(correctlyClassified, "comparison validates full protocol before confidence rejection: " + mode);
        var result = await Provider(client, evidence).VerifyAsync(input, default);
        Check(result.Available == ordinary && !result.MatchPassed && !result.LivenessPassed,
            "comparison classification survives provider adapter: " + mode);
        Check(Zeroed(evidence.LastBytes), "comparison rejection zeroes capture memory: " + mode);
    }
    transport = new WorkflowTransport("success"); evidence = new WorkflowEvidence(image);
    using (var client = Client(transport))
    {
        var result = await Provider(client, evidence).VerifyAsync(input with { ActorSessionId = null }, default);
        Check(!result.MatchPassed && evidence.Calls == 0 && transport.Calls == 0, "missing actor session cannot read evidence or contact provider");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var cancelled = false;
        try { await Provider(client, evidence).EnrollAsync(enrollment, cancellation.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && transport.Calls == 0, "caller cancellation propagates without a provider submission");
    }
    // Preserve the complete bounded PNG envelope, but corrupt its compressed payload.
    // This exercises the actual native decoder after the structural input guard.
    var corrupt = image.ToArray();
    try
    {
        for (var offset = 8; offset < corrupt.Length - 12;)
        {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(corrupt.AsSpan(offset)));
            if (corrupt.AsSpan(offset + 4, 4).SequenceEqual("IDAT"u8)) Array.Clear(corrupt, offset + 8, length);
            offset += length + 12;
        }
        Check(SelfHostedFaceImage.Inspect(corrupt).Width == 100, "corrupt-codec fixture reaches past structural PNG inspection");
        transport = new WorkflowTransport("success"); evidence = new WorkflowEvidence(corrupt);
        using var client = Client(transport);
        var result = await Provider(client, evidence).VerifyAsync(input, default);
        Check(!result.Available && !result.MatchPassed && !result.LivenessPassed && transport.Comparisons == 0,
            "actual decoder rejects corrupt compressed pixels before comparison");
        Check(Zeroed(evidence.LastBytes), "corrupt decoded image path zeroes capture memory");
    }
    finally { Array.Clear(corrupt); }

    using var concurrencyFixture = Model("MiniFASNetV1SE.onnx", "ebab7f90c7833fbccd46d3a555410e78d969db5438e169b6524be444862b3676", 4, .1);
    var gate = (SemaphoreSlim)typeof(SilentFaceLivenessService).GetField("gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(concurrencyFixture)!;
    await gate.WaitAsync();
    try
    {
        using var cancellation = new CancellationTokenSource();
        var queued = concurrencyFixture.VerifyAsync(image, new(20, 20, 80, 80), cancellation.Token);
        Check(!queued.IsCompleted, "queued inference waits for its bounded native worker");
        cancellation.Cancel();
        var cancelled = false;
        try { await queued.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "queued inference cancellation completes without starting a native run");
    }
    finally { gate.Release(); }
    var concurrent = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => concurrencyFixture.VerifyAsync(image, new(20, 20, 80, 80))))
        .WaitAsync(TimeSpan.FromSeconds(10));
    Check(concurrent.All(r => r.Passed && r.Models.Count == 1), "subsequent concurrent native requests remain usable after queue cancellation");
    concurrencyFixture.Dispose();
    var disposed = false;
    try { await concurrencyFixture.VerifyAsync(image, new(20, 20, 80, 80)); }
    catch (ObjectDisposedException) { disposed = true; }
    Check(disposed, "disposed liveness sessions cannot accept new inference");
}
finally { Array.Clear(image); Array.Clear(input.Template); }
Console.WriteLine($"Provider workflow: {passed} passed, {failures.Count} failed. Mock identity/face responses; real ONNX on synthetic pixels. No real-person acceptance.");
return failures.Count == 0 ? 0 : 1;

sealed class WorkflowEvidence(byte[] original) : IFaceCaptureEvidenceReader
{
    public byte[]? LastBytes;
    public FaceCaptureBinding? LastBinding;
    public string? LastOperation;
    public int Calls;
    public Task<byte[]> ReadAsync(string token, FaceCaptureBinding binding, string operationKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Calls++; LastBinding = binding; LastOperation = operationKey;
        LastBytes = original.ToArray(); return Task.FromResult(LastBytes);
    }
}
sealed class WorkflowTransport(string mode) : HttpMessageHandler
{
    public int Calls;
    public int Comparisons;
    private static string Face(int shift = 0, double probability = .99) =>
        $"{{\"box\":{{\"x_min\":{20 + shift},\"y_min\":20,\"x_max\":{80 + shift},\"y_max\":80,\"probability\":{probability.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}}}";
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var call = Interlocked.Increment(ref Calls);
        if (mode == "network") throw new HttpRequestException("fixture outage");
        var status = mode switch { "wrong-key" => HttpStatusCode.NotFound, "internal" or "wrong-status-no-face" => HttpStatusCode.InternalServerError,
            "http-no-face" or "http-many-faces" or "missing-key" or "unknown-error" or "duplicate-error" or "text-error" or "oversized-error" or
            "string-error-code" or "fractional-error-code" or "missing-error-message" or "null-error-message" or "extra-error-field" or "array-error" => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.OK };
        string json;
        if (status != HttpStatusCode.OK)
        {
            var code = mode switch { "http-many-faces" => 31, "wrong-key" => 10, "missing-key" => 20, "internal" => 41, "unknown-error" => 0, _ => 28 };
            json = mode switch
            {
                "duplicate-error" => "{\"code\":0,\"code\":28,\"message\":\"do-not-echo\"}",
                "string-error-code" => "{\"code\":\"28\",\"message\":\"do-not-echo\"}",
                "fractional-error-code" => "{\"code\":28.5,\"message\":\"do-not-echo\"}",
                "missing-error-message" => "{\"code\":28}",
                "null-error-message" => "{\"code\":28,\"message\":null}",
                "extra-error-field" => "{\"code\":28,\"message\":\"do-not-echo\",\"result\":[]}",
                "array-error" => "[{\"code\":28,\"message\":\"do-not-echo\"}]",
                _ => $"{{\"code\":{code},\"message\":\"{(mode == "oversized-error" ? new string('x', 70000) : "do-not-echo")}\"}}"
            };
        }
        else if (request.RequestUri!.AbsolutePath.EndsWith("/verify"))
        {
            Interlocked.Increment(ref Comparisons);
            var source = Face(mode == "changed-bounds" ? 1 : 0, mode is "comparison-low-source" or "comparison-malformed-matches" ? .1 : .99);
            var target = Face(mode == "comparison-changed-low-box" ? 1 : mode == "changed-bounds" ? 1 : 0,
                mode is "comparison-low-target" or "comparison-malformed-score" or "comparison-changed-low-box" ? .1 : .99);
            var matches = mode == "comparison-malformed-matches" ? "null" :
                $"[{{\"box\":{target[7..^1]},\"similarity\":{(mode == "wrong-person" ? "0.2" : mode == "comparison-malformed-score" ? "\"invalid\"" : "0.95")}}}]";
            json = $"{{\"result\":[{{\"source_image_face\":{source},\"face_matches\":{matches}}}]}}";
        }
        else
        {
            var face = Face(mode == "changed-bounds" && call > 1 ? 1 : 0, mode == "low-confidence" ? .5 : .99);
            json = mode switch { "empty-faces" => "{\"result\":[]}", "many-faces" => $"{{\"result\":[{face},{face}]}}",
                "malformed-many-faces" => $"{{\"result\":[{face},{{\"box\":null}}]}}",
                "ok-error-body" => "{\"code\":28,\"message\":\"do-not-echo\"}",
                "invalid-coordinate" => $"{{\"result\":[{face.Replace("\"x_min\":20", "\"x_min\":-1")}]}}", _ => $"{{\"result\":[{face}]}}" };
        }
        return Task.FromResult(new HttpResponseMessage(status)
            { Content = new StringContent(json, Encoding.UTF8, mode == "text-error" ? "text/plain" : "application/json") });
    }
}
