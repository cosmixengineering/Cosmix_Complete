#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifySelfHostedFaceProvider.packages.lock.json
using System.Net;
using System.Reflection;
using System.Text;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Authentication;
using Cosmix.Infrastructure.FaceVerification;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

// Offline transport/semantic checks only. Never sends a request, loads weights or accepts attendance.
var checks = 0;
void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
async Task Reject(Func<Task> action, string label, int statusCode = 503)
{
    try { await action(); }
    catch (AuthenticationFailure error) when (error.StatusCode == statusCode) { checks++; return; }
    throw new Exception(label);
}
var options = new CompreFaceOptions
{
    BaseUri = new Uri("https://10.20.30.40:8000/"), DetectionApiKey = "synthetic-detection-key",
    VerificationApiKey = "synthetic-verification-key", DeploymentVersion = "synthetic-protocol-only",
    MinimumDetectionProbability = 0.9
};
CompreFaceClient Client(TestTransport transport, CompreFaceOptions? setting = null) =>
    (CompreFaceClient)typeof(CompreFaceClient).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance,
        null, new[] { typeof(CompreFaceOptions), typeof(HttpMessageHandler) }, null)!.Invoke(new object[] { setting ?? options, transport });
HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
const string box = "{\"probability\":0.99,\"x_min\":1,\"y_min\":1,\"x_max\":6,\"y_max\":6}";
var face = "{\"box\":" + box + "}";
var detection = "{\"result\":[" + face + "]}";
var comparison = "{\"result\":[{\"source_image_face\":" + face + ",\"face_matches\":[{\"box\":" + box + ",\"similarity\":0.97}]}]}";
using var pixels = new Mat(8, 8, MatType.CV_8UC3, new Scalar(7, 123, 250));
Check(Cv2.ImEncode(".png", pixels, out var image), "encode synthetic non-face RGB PNG");
var imageInfo = SelfHostedFaceImage.Inspect(image);
Check(imageInfo.Width == 8 && imageInfo.Height == 8, "bounded header dimensions");
Check(CompreFaceClient.IsPrivateAddress(IPAddress.Parse("10.0.0.1"), false), "RFC1918 private address");
Check(!CompreFaceClient.IsPrivateAddress(IPAddress.Parse("8.8.8.8"), true), "public address rejected");
Check(!CompreFaceClient.IsPrivateAddress(IPAddress.Parse("169.254.169.254"), true), "metadata/link-local rejected");
Check(!CompreFaceClient.IsPrivateAddress(IPAddress.Parse("::ffff:8.8.8.8"), true), "mapped public IPv4 rejected");
Check(!CompreFaceClient.IsPrivateAddress(IPAddress.Loopback, false), "loopback requires explicit PoC opt-in");
Check(CompreFaceClient.IsPrivateAddress(IPAddress.Parse("fd00::1"), false), "ULA private address");
foreach (var uri in new[] { "https://8.8.8.8/", "http://10.0.0.1/", "https://10.0.0.1/path/", "https://user:secret@10.0.0.1/", "https://10.0.0.1/?target=x" })
    await Reject(() => { CompreFaceClient.ValidateOptions(options with { BaseUri = new Uri(uri) }); return Task.CompletedTask; }, "unsafe server origin");
CompreFaceClient.ValidateOptions(options with { BaseUri = new Uri("http://127.0.0.1:8000/"), AllowLoopbackHttpForPoc = true });
Check(!options.ToString().Contains(options.DetectionApiKey), "configuration string redacts keys");

var transport = new TestTransport(async (request, ct) =>
{
    Check(request.Method == HttpMethod.Post && request.Version == HttpVersion.Version11, "POST over exact HTTP/1.1");
    Check(request.RequestUri!.Query == "?limit=0&det_prob_threshold=0&face_plugins=&status=false", "no face truncation or optional plugins");
    var verify = request.RequestUri.AbsolutePath == "/api/v1/verification/verify";
    Check(request.Headers.GetValues("x-api-key").Single() == (verify ? options.VerificationApiKey : options.DetectionApiKey), "service-specific API key");
    var parts = ((MultipartFormDataContent)request.Content!).ToArray();
    Check(parts.Length == (verify ? 2 : 1), "actual multipart part count");
    Check(parts.Select(p => p.Headers.ContentDisposition!.Name!.Trim('"')).SequenceEqual(
        verify ? new[] { "source_image", "target_image" } : new[] { "file" }), "official multipart field names");
    foreach (var part in parts)
        Check((await part.ReadAsByteArrayAsync(ct)).SequenceEqual(image) && part.Headers.ContentType!.MediaType == "image/png", "original image bytes, fixed MIME");
    return Json(verify ? comparison : detection);
});
using (var client = Client(transport))
{
    var detected = await client.DetectAsync(image);
    Check(detected.Bounds == new FaceBounds(1, 1, 6, 6) && detected.Probability == 0.99, "detect parses full single face");
    var verified = await client.VerifyAsync(image, image);
    Check(verified.SourceBounds == detected.Bounds && verified.TargetBounds == detected.Bounds && verified.Similarity == 0.97, "official stateless 1:1 shape");
    Check(transport.Calls == 4, "verification pre-detects both images then compares exactly once");
}

foreach (var ordinaryRejection in new[] { "{\"result\":[]}", "{\"result\":[" + face + "," + face + "]}", detection.Replace("0.99", "0.1") })
{
    var scripted = new TestTransport((_, _) => Task.FromResult(Json(ordinaryRejection)));
    using var client = Client(scripted);
    await Reject(async () => { _ = await client.DetectAsync(image); }, "normal face rejection requests a new capture", 409);
    Check(scripted.Calls == 1, "normal face rejection is never retried automatically");
}
foreach (var invalid in new[]
{
    "{}", "{\"result\":null}",
    "{\"result\":[" + face + "],\"error\":\"remote-secret\"}", "{\"result\":[],\"result\":[" + face + "]}",
    detection.Replace("0.99", "1.01"), detection.Replace("0.99", "-0.1"), detection.Replace("0.99", "1e309"),
    detection.Replace("0.99", "\"0.99\""),
    detection.Replace("\"x_min\":1", "\"x_min\":\"1\""), detection.Replace("\"x_min\":1", "\"x_min\":1.5"),
    detection.Replace("\"x_min\":1", "\"x_min\":-1"), detection.Replace("\"x_max\":6", "\"x_max\":9"),
    detection.Replace("\"x_max\":6", "\"x_max\":1"), "{\"result\":[{\"box\":{}}]}"
})
{
    var scripted = new TestTransport((_, _) => Task.FromResult(Json(invalid)));
    using var client = Client(scripted);
    await Reject(async () => { _ = await client.DetectAsync(image); }, "malformed/full-result fail closed");
    Check(scripted.Calls == 1, "malformed response never retried");
}
foreach (var invalid in new[]
{
    comparison.Replace("0.97", "1.2"), comparison.Replace("0.97", "\"0.97\""),
    "{\"result\":[{\"source_image_face\":" + face + ",\"face_matches\":[]}]}",
    "{\"result\":[{\"source_image_face\":" + face + ",\"face_matches\":[{\"box\":" + box + ",\"similarity\":0.97},{\"box\":" + box + ",\"similarity\":0.98}]}]}",
    comparison.Replace("\"source_image_face\":", "\"wrong_source\":"),
    comparison.Replace("\"x_min\":1", "\"x_min\":2")
})
{
    var scripted = new TestTransport((r, _) => Task.FromResult(Json(r.RequestUri!.AbsolutePath.EndsWith("detect") ? detection : invalid)));
    using var client = Client(scripted);
    await Reject(async () => { _ = await client.VerifyAsync(image, image); }, "verification score/count/bounds fail closed");
    Check(scripted.Calls == 3, "single comparison attempt");
}
foreach (var status in new[] { HttpStatusCode.Redirect, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError })
{
    var scripted = new TestTransport((_, _) => Task.FromResult(new HttpResponseMessage(status)
    { Content = new StringContent("remote-secret"), Headers = { Location = new Uri("https://8.8.8.8/") } }));
    using var client = Client(scripted);
    await Reject(async () => { _ = await client.DetectAsync(image); }, "redirect/error response rejected");
    Check(scripted.Calls == 1, "no redirect/retry request");
}
var oversized = new TestTransport((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
{ Content = new StreamContent(new NonSeekableStream(Encoding.UTF8.GetBytes(new string(' ', 257))))
    { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") } } }));
using (var client = Client(oversized, options with { MaximumResponseBytes = 256 }))
    await Reject(async () => { _ = await client.DetectAsync(image); }, "unknown-length oversized stream rejected");
var timeout = new TestTransport(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return Json(detection); });
using (var client = Client(timeout, options with { TimeoutSeconds = 1 }))
    await Reject(async () => { _ = await client.DetectAsync(image); }, "bounded request deadline");
var cancelTransport = new TestTransport(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return Json(detection); });
using (var client = Client(cancelTransport))
using (var cancellation = new CancellationTokenSource(20))
{
    try { _ = await client.DetectAsync(image, cancellation.Token); throw new Exception("caller cancellation lost"); }
    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { checks++; }
}

var createInput = typeof(SilentFaceLivenessService).GetMethod("CreateInputTensor", BindingFlags.NonPublic | BindingFlags.Static)!;
var tensor = (DenseTensor<float>)createInput.Invoke(null, new object[] { pixels })!;
Check(tensor.Dimensions.SequenceEqual(new[] { 1, 3, 8, 8 }) && tensor[0, 0, 0, 0] == 7 &&
    tensor[0, 1, 0, 0] == 123 && tensor[0, 2, 0, 0] == 250, "actual float NCHW BGR preserves 0-255 without division");
tensor.Buffer.Span.Clear();
using (var ramp = new Mat(3, 5, MatType.CV_8UC3))
{
    for (var y = 0; y < 3; y++)
    for (var x = 0; x < 5; x++)
        ramp.Set(y, x, new Vec3b((byte)(10 * y + x), (byte)(100 + 10 * y + x), (byte)(200 + 10 * y + x)));
    var spatial = (DenseTensor<float>)createInput.Invoke(null, new object[] { ramp })!;
    try
    {
        Check(spatial.Dimensions.SequenceEqual(new[] { 1, 3, 3, 5 }), "rectangular input retains height and width in NCHW order");
        Check(spatial.ToArray().SequenceEqual(new float[]
        {
            0, 1, 2, 3, 4, 10, 11, 12, 13, 14, 20, 21, 22, 23, 24,
            100, 101, 102, 103, 104, 110, 111, 112, 113, 114, 120, 121, 122, 123, 124,
            200, 201, 202, 203, 204, 210, 211, 212, 213, 214, 220, 221, 222, 223, 224
        }), "nonuniform input preserves each row, column and BGR channel plane without scaling");
    }
    finally { spatial.Buffer.Span.Clear(); ramp.SetTo(Scalar.All(0)); }
}
var semantics = new SilentFaceModel { Sha256 = new string('A', 64), ClassCount = 3, LiveClassIndex = 1,
    MinimumLiveProbability = 0.7, OutputKind = SilentFaceOutputKind.Logits, CropKind = SilentFaceCropKind.UpstreamScaledBox, CropScale = 2.7 };
var evaluate = typeof(SilentFaceLivenessService).GetMethod("Evaluate", BindingFlags.NonPublic | BindingFlags.Static)!;
SilentFaceModelScore Evaluate(SilentFaceModel contract, float[] values) =>
    (SilentFaceModelScore)evaluate.Invoke(null, new object[] { contract, values })!;
Check(Evaluate(semantics, new[] { -1000f, 1000f, -1000f }).Passed, "stable softmax and explicit live index");
Check(!Evaluate(semantics, new[] { 1000f, -1000f, -1000f }).Passed, "spoof winner rejected");
Check(!Evaluate(semantics with { MinimumLiveProbability = 0.3 }, new[] { 0f, 0f, 0f }).Passed, "class ties rejected");
Check(Evaluate(semantics with { OutputKind = SilentFaceOutputKind.Probabilities }, new[] { 0.1f, 0.8f, 0.1f }).Passed, "explicit probabilities avoid double softmax");
foreach (var values in new[] { new[] { 0.1f, 0.8f, 0.8f }, new[] { -0.1f, 1f, 0.1f }, new[] { 0f, float.NaN, 0f } })
{
    try { _ = Evaluate(semantics with { OutputKind = SilentFaceOutputKind.Probabilities }, values); throw new Exception("invalid liveness output accepted"); }
    catch (TargetInvocationException error) when (error.InnerException is AuthenticationFailure) { checks++; }
}
var cropMethod = typeof(SilentFaceLivenessService).GetMethod("CropRectangle", BindingFlags.NonPublic | BindingFlags.Static)!;
var crop = (Rect)cropMethod.Invoke(null, new object[] { new FaceBounds(0, 0, 20, 20), 100, 100, semantics })!;
Check(crop.X == 0 && crop.Y == 0 && crop.Width == 55 && crop.Height == 55, "upstream edge shifting and inclusive crop endpoint");
// Hand-calculated from CompreFace max-minus-min extents and the reviewed upstream inclusive crop endpoint.
foreach (var example in new[]
{
    (Bounds: new FaceBounds(30, 40, 50, 60), Width: 100, Height: 100, Want: new Rect(13, 23, 55, 55), Label: "interior crop"),
    (Bounds: new FaceBounds(0, 40, 20, 60), Width: 100, Height: 100, Want: new Rect(0, 23, 55, 55), Label: "left-edge crop shifts without shrinking"),
    (Bounds: new FaceBounds(80, 80, 100, 100), Width: 100, Height: 100, Want: new Rect(45, 45, 55, 55), Label: "bottom-right crop shifts both axes"),
    (Bounds: new FaceBounds(10, 20, 50, 40), Width: 60, Height: 50, Want: new Rect(0, 15, 60, 30), Label: "image width caps scale and fractional endpoints truncate")
})
{
    var actual = (Rect)cropMethod.Invoke(null, new object[] { example.Bounds, example.Width, example.Height, semantics })!;
    Check(actual == example.Want, example.Label);
}
var wholeImage = (Rect)cropMethod.Invoke(null, new object[] { new FaceBounds(10, 20, 50, 40), 60, 50,
    semantics with { CropKind = SilentFaceCropKind.WholeImage, CropScale = null } })!;
Check(wholeImage == new Rect(0, 0, 60, 50), "reviewed whole-image crop retains all original pixels");
using var unavailable = new SilentFaceLivenessService(new SilentFaceOptions { Enabled = true, Models = new[] { semantics } });
Check(!unavailable.IsConfigured, "missing artifact and reviewed metadata unavailable");
await Reject(async () => { _ = await unavailable.VerifyAsync(image, new FaceBounds(1, 1, 6, 6)); }, "unconfigured runtime has no successful fallback");
using (var client = Client(new TestTransport((_, _) => throw new Exception("unexpected provider IO"))))
{
    var reader = new NeverReadEvidence();
    var provider = new SelfHostedFaceVerificationProvider(new SelfHostedFaceOptions
    { Enabled = true, MinimumSimilarity = 0.9 }, client, unavailable, reader);
    Check(!provider.Capability.Available, "missing real model blocks provider readiness");
    await Reject(async () => { _ = await provider.EnrollAsync(new(Guid.NewGuid(), Guid.NewGuid(), "synthetic", "synthetic",
        "enrollment:" + Guid.NewGuid().ToString("N")), default); }, "unavailable enrollment is a 503, not a rejected enrollment");
    Check(reader.Calls == 0, "unavailable enrollment never reads capture");
}

// Force only the configuration gate in the harness, so the real enrollment catch path can be exercised
// without fabricating any model artifact or invoking inference. This never exists in runtime composition.
foreach (var outage in new Exception[]
{
    new AuthenticationFailure("remote-sensitive-details", 503), new HttpRequestException("remote-sensitive-details"),
    new OperationCanceledException("remote-sensitive-details")
})
{
    using var client = Client(new TestTransport((_, _) => throw new Exception("unexpected network")));
    var reader = new ThrowingEvidence(outage);
    var provider = new SelfHostedFaceVerificationProvider(new SelfHostedFaceOptions(), client, unavailable, reader);
    typeof(SelfHostedFaceVerificationProvider).GetField("<Capability>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
        .SetValue(provider, provider.Capability with { Available = true });
    var input = new FaceProviderEnrollmentInput(Guid.NewGuid(), Guid.NewGuid(), "synthetic-token", "synthetic",
        "enrollment:" + Guid.NewGuid().ToString("N"), Guid.NewGuid(), Guid.NewGuid());
    try { _ = await provider.EnrollAsync(input, default); throw new Exception("outage converted to ProviderRejected"); }
    catch (AuthenticationFailure error) when (error.StatusCode == 503)
    { Check(!error.Message.Contains("sensitive"), "enrollment outage preserved as sanitized 503"); }
    Check(reader.Calls == 1 && reader.OperationKey == input.IdempotencyKey && reader.Binding!.AccountId == input.ActorAccountId &&
        reader.Binding.SessionId == input.ActorSessionId && reader.Binding.AttemptId is null && reader.Binding.ChallengeSha256 is null,
        "enrollment claims server-bound capture exactly once");
    var invalid = await provider.EnrollAsync(input with { ActorSessionId = null }, default);
    Check(!invalid.Success && invalid.Template is null && reader.Calls == 1, "actual invalid binding is rejected before evidence IO");
}
Console.WriteLine($"Self-hosted face transport/semantic checks passed: {checks}. No model inference, network, SQL or biometric acceptance.");

sealed class TestTransport(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> script) : HttpMessageHandler
{
    public int Calls { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    { Calls++; return script(request, ct); }
}
sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
{
    public override bool CanSeek => false;
}
sealed class NeverReadEvidence : IFaceCaptureEvidenceReader
{
    public int Calls { get; private set; }
    public Task<byte[]> ReadAsync(string token, FaceCaptureBinding binding, string operationKey, CancellationToken ct)
    { Calls++; throw new Exception("unexpected evidence read"); }
}
sealed class ThrowingEvidence(Exception outage) : IFaceCaptureEvidenceReader
{
    public int Calls { get; private set; }
    public string? OperationKey { get; private set; }
    public FaceCaptureBinding? Binding { get; private set; }
    public Task<byte[]> ReadAsync(string token, FaceCaptureBinding binding, string operationKey, CancellationToken ct)
    { Calls++; OperationKey = operationKey; Binding = binding; return Task.FromException<byte[]>(outage); }
}
