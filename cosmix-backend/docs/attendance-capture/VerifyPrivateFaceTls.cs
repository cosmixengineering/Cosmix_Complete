// Actual TLS handshakes against ephemeral loopback Kestrel endpoints.
// No global trust-store changes, external requests, credentials or biometrics.
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var checks = 0;
var failures = 0;
void Check(bool value, string name)
{
    checks++;
    if (!value) { failures++; Console.WriteLine("FAIL: " + name); }
}
using var caKey = RSA.Create(2048);
var caRequest = new CertificateRequest("CN=Cosmix TLS harness root", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
using var ca = caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
var fixtureRoot = Path.Combine(Path.GetTempPath(), "Cosmix-Face-TLS-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureRoot);
var rootPath = Path.Combine(fixtureRoot, "root.crt");
File.WriteAllText(rootPath, ca.ExportCertificatePem(), new UTF8Encoding(false));
var rootHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(rootPath)));

async Task<bool> HandshakeAsync(bool matchingName, string? trustedPath, string? trustedHash)
{
    using var leafKey = RSA.Create(2048);
    var request = new CertificateRequest("CN=Cosmix ephemeral endpoint", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection
        { new("1.3.6.1.5.5.7.3.1") }, true));
    var names = new SubjectAlternativeNameBuilder();
    names.AddIpAddress(matchingName ? IPAddress.Loopback : IPAddress.Parse("192.0.2.1"));
    request.CertificateExtensions.Add(names.Build());
    using var issued = request.Create(ca, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1), RandomNumberGenerator.GetBytes(16));
    using var certificateWithKey = issued.CopyWithPrivateKey(leafKey);
    // Windows Schannel needs an imported PFX key for the TLS server fixture;
    // a CopyWithPrivateKey-only ephemeral CNG key can terminate the handshake.
    using var certificate = X509CertificateLoader.LoadPkcs12(certificateWithKey.Export(X509ContentType.Pfx), null,
        X509KeyStorageFlags.Exportable);
    var builder = WebApplication.CreateBuilder();
    builder.Logging.ClearProviders();
    builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, endpoint => endpoint.UseHttps(certificate)));
    await using var app = builder.Build();
    app.MapGet("/healthz", () => "ok");
    await app.StartAsync();
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    var settings = new Dictionary<string, string?>
    {
        ["SelfHostedFace:CompreFace:BaseUri"] = address + "/",
        ["SelfHostedFace:CompreFace:DetectionApiKey"] = "fixture-detection-key",
        ["SelfHostedFace:CompreFace:VerificationApiKey"] = "fixture-verification-key",
        ["SelfHostedFace:CompreFace:DeploymentVersion"] = "fixture-version",
        ["SelfHostedFace:CompreFace:MinimumDetectionProbability"] = "0.5",
        ["SelfHostedFace:CompreFace:AllowLoopbackHttpForPoc"] = "true",
        ["SelfHostedFace:CompreFace:TrustedRootCertificatePath"] = trustedPath,
        ["SelfHostedFace:CompreFace:TrustedRootCertificateSha256"] = trustedHash
    };
    var services = new ServiceCollection();
    services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    services.AddAttendanceCaptureInfrastructure();
    using var provider = services.BuildServiceProvider();
    try
    {
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("Cosmix.CompreFace");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var response = await client.GetAsync(address + "/healthz", timeout.Token);
        return response.IsSuccessStatusCode && await response.Content.ReadAsStringAsync(timeout.Token) == "ok";
    }
    catch (HttpRequestException error)
    { Console.WriteLine("TLS diagnostic: " + error.Message + " / " + error.InnerException?.Message); return false; }
    catch (Cosmix.Application.Authentication.AuthenticationFailure error)
    { Console.WriteLine("TLS configuration diagnostic: " + error.Message); return false; }
    finally { await app.StopAsync(); }
}
Check(await HandshakeAsync(true, rootPath, rootHash), "explicit pinned private CA permits a valid named endpoint");
Check(!await HandshakeAsync(true, null, null), "an untrusted private issuer fails without configured trust");
Check(!await HandshakeAsync(true, rootPath, new string('0', 64)), "a changed root certificate hash fails closed");
Check(!await HandshakeAsync(false, rootPath, rootHash), "private CA trust never bypasses endpoint-name validation");
Console.WriteLine($"Private face TLS: {checks - failures} passed, {failures} failed; no system trust changes.");
return failures == 0 ? 0 : 1;
