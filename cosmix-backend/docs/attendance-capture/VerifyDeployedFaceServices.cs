// Opt-in LAN deployment diagnostics. Argument: protected flattened configuration JSON.
// Uses synthetic pixels only. Never enables attendance or writes a policy threshold.
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Infrastructure.FaceVerification;
using Cosmix.Infrastructure.Files;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;

var configuration = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(args.Single())).Build();
var options = configuration.GetSection(CompreFaceOptions.SectionName).Get<CompreFaceOptions>()!;
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddSingleton(TimeProvider.System);
services.AddSingleton(configuration.GetSection(PrivateFileStorageOptions.SectionName).Get<PrivateFileStorageOptions>() ?? new());
services.AddAttendanceCaptureInfrastructure();
using var provider = services.BuildServiceProvider();
using var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient("Cosmix.CompreFace");
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
var failed = 0;
var passed = 0;
void Check(bool condition, string name)
{
    Console.WriteLine((condition ? "PASS: " : "FAIL: ") + name);
    if (condition) passed++; else failed++;
}
using var health = await http.GetAsync(new Uri(options.BaseUri!, "healthz"), deadline.Token);
Check(health.StatusCode == HttpStatusCode.OK && (await health.Content.ReadAsStringAsync(deadline.Token)).Trim() == "ok",
    "named backend factory reaches the real LAN endpoint with pinned private CA");
using var login = await http.GetAsync(new Uri(options.BaseUri!, "login"), deadline.Token);
Check(login.StatusCode == HttpStatusCode.OK, "CompreFace web interface HTTP response loads");
using var coreStatus = await http.GetAsync(new Uri(options.BaseUri!, "core/status"), deadline.Token);
var coreReady = false;
if (coreStatus.StatusCode == HttpStatusCode.OK && coreStatus.Content.Headers.ContentType?.MediaType == "application/json")
{
    using var status = JsonDocument.Parse(await coreStatus.Content.ReadAsStringAsync(deadline.Token));
    coreReady = status.RootElement.TryGetProperty("status", out var value) && value.GetString() == "OK" &&
        status.RootElement.TryGetProperty("available_plugins", out var plugins) && plugins.ValueKind == JsonValueKind.Object;
}
Check(coreReady, "login bootstrap receives the real ready core status through the proxy");
using var coreInference = await http.GetAsync(new Uri(options.BaseUri!, "core/recognize"), deadline.Token);
Check(coreInference.StatusCode == HttpStatusCode.NotFound, "raw core inference routes remain inaccessible");
using var statusWrite = await http.PostAsync(new Uri(options.BaseUri!, "core/status"), new StringContent(""), deadline.Token);
Check(statusWrite.StatusCode == HttpStatusCode.Forbidden, "core status bootstrap is read-only");
using var invalidRegistration = await http.PostAsync(new Uri(options.BaseUri!, "admin/user/register"),
    new StringContent("{}", System.Text.Encoding.UTF8, "application/json"), deadline.Token);
Check(invalidRegistration.StatusCode == HttpStatusCode.BadRequest &&
    invalidRegistration.Content.Headers.ContentType?.MediaType == "application/json",
    "sign-up reaches native validation instead of a proxy registration block");
using var demo = await http.GetAsync(new Uri(options.BaseUri!, "admin/user/demo/model"), deadline.Token);
var demoUnavailable = false;
if (demo.StatusCode == HttpStatusCode.NotFound && demo.Content.Headers.ContentType?.MediaType == "application/json")
{
    using var demoResult = JsonDocument.Parse(await demo.Content.ReadAsStringAsync(deadline.Token));
    demoUnavailable = demoResult.RootElement.TryGetProperty("code", out var code) && code.GetInt32() == 404 &&
        !demoResult.RootElement.TryGetProperty("apiKey", out _);
}
Check(demoUnavailable, "vendor first-run demo is absent after owner provisioning and exposes no service key");
using var blank = new Mat(160, 160, MatType.CV_8UC3, new Scalar(127, 127, 127));
Cv2.ImEncode(".png", blank, out var image);
try
{
    async Task<(HttpStatusCode Status, int Code)> SubmitAsync(string path, string? key, bool verification = false)
    {
        using var form = new MultipartFormDataContent();
        void Add(string name)
        {
            var part = new ByteArrayContent(image);
            part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(part, name, "synthetic.png");
        }
        Add(verification ? "source_image" : "file");
        if (verification) Add("target_image");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(options.BaseUri!, path)) { Content = form };
        if (key is not null) request.Headers.Add("x-api-key", key);
        using var response = await http.SendAsync(request, deadline.Token);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(deadline.Token));
        var code = json.RootElement.GetProperty("code").GetInt32();
        Console.WriteLine($"Service diagnostic HTTP {(int)response.StatusCode}, error code {code}");
        return (response.StatusCode, code);
    }
    var detect = await SubmitAsync("api/v1/detection/detect", options.DetectionApiKey);
    Check(detect == (HttpStatusCode.BadRequest, 28), "valid detection credential reaches no-face rejection for synthetic pixels");
    var verify = await SubmitAsync("api/v1/verification/verify", options.VerificationApiKey, verification: true);
    Check(verify == (HttpStatusCode.BadRequest, 28), "valid verification credential reaches no-face rejection for synthetic pixels");
    var noKey = await SubmitAsync("api/v1/detection/detect", null);
    // CompreFace 1.2.0 CommonExceptionCode defines these precise error contracts.
    Check(noKey == (HttpStatusCode.BadRequest, 20), "missing service key is rejected");
    var wrongKey = await SubmitAsync("api/v1/detection/detect", Guid.NewGuid().ToString());
    Check(wrongKey == (HttpStatusCode.NotFound, 10), "incorrect service key is rejected");
    var adapter = provider.GetRequiredService<CompreFaceClient>();
    var rejected = false;
    try { await adapter.DetectAsync(image, deadline.Token); }
    catch (AuthenticationFailure error) { rejected = error.StatusCode == 409 && error.Code == "face_not_detected"; }
    Check(rejected, "actual backend adapter classifies no-face as recapture, not an outage");
    rejected = false;
    try { await adapter.DetectAsync(new byte[] { 1, 2, 3 }, deadline.Token); }
    catch (AuthenticationFailure) { rejected = true; }
    Check(rejected, "backend rejects invalid image bytes");
    Check(provider.GetRequiredService<SilentFaceLivenessService>().IsConfigured,
        "both pinned liveness models load from the saved operational configuration");
    var store = provider.GetRequiredService<IFaceTemplateStore>();
    Check(store is RetentionControlledFaceTemplateStore && store.IsConfigured,
        "real encrypted biometric vault and lifecycle journal are configured");
    var readiness = SelfHostedFaceConfigurationReadiness.Evaluate(configuration, checkLocalArtifacts: true);
    foreach (var issue in readiness.Issues) Console.WriteLine($"Configuration issue: {issue.Setting} / {issue.Code}");
    Check(readiness.Passed && readiness.DisabledSections.Count == 0,
        "all persisted face settings and local artifacts pass configuration readiness");
}
finally { Array.Clear(image); }
Console.WriteLine($"Deployed services: {passed} passed, {failed} failed. Real-person attendance acceptance NOT RUN.");
return failed == 0 ? 0 : 1;
