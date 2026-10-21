// Real hash-pinned model and DI transport checks. Run from the external
// deployment harness with a model-directory argument; no SQL or employee data.
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Infrastructure.Files;
using Cosmix.Infrastructure.FaceVerification;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using OpenCvSharp;

var modelDirectory = args.Single();
var failures = new List<string>();
var passed = 0;
void Check(bool condition, string name)
{
    if (condition) passed++;
    else { failures.Add(name); Console.WriteLine("FAIL: " + name); }
}
var serializer = new JsonSerializerOptions();
serializer.Converters.Add(new JsonStringEnumConverter());
var artifacts = new (string Name, string Sha, double Scale)[]
{
    ("MiniFASNetV1SE.onnx", "ebab7f90c7833fbccd46d3a555410e78d969db5438e169b6524be444862b3676", 4.0),
    ("MiniFASNetV2.onnx", "b32929adc2d9c34b9486f8c4c7bc97c1b69bc0ea9befefc380e4faae4e463907", 2.7)
};
SilentFaceModel Contract((string Name, string Sha, double Scale) artifact, string? batchName)
{
    // BatchDimensionName is intentionally supplied via actual JSON binding so
    // the pre-change implementation compiles but fails the runtime assertion.
    var json = JsonSerializer.Serialize(new
    {
        Path = Path.Combine(modelDirectory, artifact.Name), Sha256 = artifact.Sha,
        SourceRevision = "c175efef505bc275f7835cc9aa2aecab305ac184",
        ExportReference = "yakhyo/face-anti-spoofing:weights; release digest verified",
        LicenseReviewReference = "Cosmix deployment model assessment 2026-10-19; Apache-2.0",
        SemanticsReviewReference = "80x80 float BGR 0-255; class 1 live; logits; batch_size bound to 1",
        InputName = "input", OutputName = "output", InputWidth = 80, InputHeight = 80,
        InputContract = "float32-NCHW-BGR-0_255-OpenCV_INTER_LINEAR-CompreFaceExtent",
        CropKind = "UpstreamScaledBox", CropScale = artifact.Scale,
        OutputKind = "Logits", ClassCount = 3, LiveClassIndex = 1,
        // Harness boundary only. This is not an approved attendance threshold.
        MinimumLiveProbability = 0.9999, BatchDimensionName = batchName
    });
    return JsonSerializer.Deserialize<SilentFaceModel>(json, serializer)!;
}
foreach (var artifact in artifacts)
{
    using var withoutBinding = new SilentFaceLivenessService(new SilentFaceOptions
        { Enabled = true, Models = new[] { Contract(artifact, null) } });
    Check(!withoutBinding.IsConfigured, artifact.Name + " rejects an unreviewed dynamic batch");
    using var reviewed = new SilentFaceLivenessService(new SilentFaceOptions
        { Enabled = true, Models = new[] { Contract(artifact, "batch_size") } });
    Check(reviewed.IsConfigured, artifact.Name + " loads with explicit reviewed batch-one binding");
    if (reviewed.IsConfigured)
    {
        using var flat = new Mat(100, 100, MatType.CV_8UC3, new Scalar(10, 80, 200));
        Cv2.ImEncode(".png", flat, out var encoded);
        var result = await reviewed.VerifyAsync(encoded, new FaceBounds(20, 20, 80, 80));
        Check(result.Models.Count == 1 && double.IsFinite(result.LiveProbability) &&
            result.LiveProbability is >= 0 and <= 1, artifact.Name + " runs real ONNX on synthetic pixels");
        var resultType = result.GetType();
        Check(resultType.GetProperty("ModelVersion")?.GetValue(result) is string version && version.Length == 64 &&
            resultType.GetProperty("ProcessingTimeMilliseconds")?.GetValue(result) is double milliseconds &&
            milliseconds >= 0 && double.IsFinite(milliseconds), artifact.Name + " reports model identity and processing time");
        Array.Clear(encoded);
    }
}
using (var unsupported = new SilentFaceLivenessService(new SilentFaceOptions
    { Enabled = true, Models = new[] { Contract(artifacts[0], "arbitrary_dimension") } }))
    Check(!unsupported.IsConfigured, "unsupported dynamic dimension stays unavailable");
using (var badHash = new SilentFaceLivenessService(new SilentFaceOptions
    { Enabled = true, Models = new[] { Contract(artifacts[0], "batch_size") with { Sha256 = new string('0', 64) } } }))
    Check(!badHash.IsConfigured, "tampered model hash stays unavailable");

var settings = new Dictionary<string, string?>
{
    ["SelfHostedFace:CompreFace:BaseUri"] = "http://127.0.0.1:18000/",
    ["SelfHostedFace:CompreFace:DetectionApiKey"] = "fixture-detection-key",
    ["SelfHostedFace:CompreFace:VerificationApiKey"] = "fixture-verification-key",
    ["SelfHostedFace:CompreFace:DeploymentVersion"] = "fixture-version",
    ["SelfHostedFace:CompreFace:MinimumDetectionProbability"] = "0.5",
    ["SelfHostedFace:CompreFace:AllowLoopbackHttpForPoc"] = "true"
};
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
services.AddAttendanceCaptureInfrastructure();
using (var provider = services.BuildServiceProvider())
{
    var factory = provider.GetService<IHttpClientFactory>();
    Check(factory is not null, "CompreFace composition provides HttpClientFactory");
    if (factory is not null)
    {
        using var client = factory.CreateClient("Cosmix.CompreFace");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var denied = false;
        try { using var unexpected = await client.GetAsync("http://8.8.8.8/", timeout.Token); }
        catch (HttpRequestException error) { denied = error.ToString().Contains("face_destination_denied", StringComparison.Ordinal); }
        catch (OperationCanceledException) { }
        Check(denied, "factory transport rejects a changed/public destination before connecting");
    }
}
foreach (var environment in new[] { Environments.Production, Environments.Staging })
{
    var deployedSettings = new Dictionary<string, string?>(settings)
    {
        ["SelfHostedFace:Enabled"] = "true",
        ["SelfHostedFace:MinimumSimilarity"] = "0.9",
        ["FaceCapture:Enabled"] = "true",
        ["FaceCapture:LifetimeSeconds"] = "60",
        ["FaceCapture:ApprovalReference"] = "synthetic capture policy"
    };
    var deployed = new ServiceCollection();
    deployed.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(deployedSettings).Build());
    deployed.AddSingleton<IHostEnvironment>(new DeploymentEnvironment(environment));
    deployed.AddSingleton(new PrivateFileStorageOptions());
    deployed.AddSingleton(TimeProvider.System);
    deployed.AddSingleton<IFaceCaptureEvidenceReader, NoCaptureEvidence>();
    using var models = new SilentFaceLivenessService(new SilentFaceOptions
        { Enabled = true, Models = artifacts.Select(a => Contract(a, "batch_size")).ToArray() });
    deployed.AddSingleton(models);
    deployed.AddAttendanceCaptureInfrastructure();
    using var container = deployed.BuildServiceProvider();
    using var scope = container.CreateScope();
    var face = scope.ServiceProvider.GetRequiredService<IFaceVerificationProvider>();
    Check(face.Capability.Available && face is SelfHostedFaceVerificationProvider,
        environment + " selects the configured real adapter without a PoC approval switch");
    Check(!face.Capability.Provider.Contains("PoC", StringComparison.OrdinalIgnoreCase),
        environment + " exposes the deployed provider identity");
    Check(scope.ServiceProvider.GetRequiredService<FaceCaptureOptions>().IsConfigured,
        environment + " binds the configured capture policy");
    var store = scope.ServiceProvider.GetRequiredService<IFaceTemplateStore>();
    Check(store is RetentionControlledFaceTemplateStore && !store.IsConfigured,
        environment + " retains the encrypted-storage and retention gate");
}
Console.WriteLine($"Deployment adapter checks: {passed} passed, {failures.Count} failed. Synthetic pixels only; real anti-spoofing NOT RUN.");
return failures.Count == 0 ? 0 : 1;

sealed class DeploymentEnvironment(string name) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = name;
    public string ApplicationName { get; set; } = "Cosmix.Api";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
sealed class NoCaptureEvidence : IFaceCaptureEvidenceReader
{
    public Task<byte[]> ReadAsync(string token, FaceCaptureBinding binding, string operationKey, CancellationToken ct) =>
        throw new InvalidOperationException("This composition check must never read biometric evidence.");
}
