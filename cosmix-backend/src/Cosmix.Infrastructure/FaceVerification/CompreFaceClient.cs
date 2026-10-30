using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Cosmix.Application.Authentication;

namespace Cosmix.Infrastructure.FaceVerification;

public sealed record CompreFaceOptions
{
    public const string SectionName = "SelfHostedFace:CompreFace";
    public Uri? BaseUri { get; init; }
    public string DetectionApiKey { get; init; } = "";
    public string VerificationApiKey { get; init; } = "";
    public string DeploymentVersion { get; init; } = "";
    public bool AllowLoopbackHttpForPoc { get; init; }
    public string? TrustedRootCertificatePath { get; init; }
    public string? TrustedRootCertificateSha256 { get; init; }
    public double? MinimumDetectionProbability { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public int MaximumResponseBytes { get; init; } = 65536;
    public override string ToString() => "CompreFaceOptions(redacted)";
}

public sealed record CompreFaceDetectionResult(FaceBounds Bounds, double Probability);
public sealed record CompreFaceVerificationResult(FaceBounds SourceBounds, double SourceProbability,
    FaceBounds TargetBounds, double TargetProbability, double Similarity);

/// <summary>Stateless image-only APIs. Owns a private transport with no redirect, proxy or retry pipeline.</summary>
public sealed class CompreFaceClient : IDisposable
{
    private readonly CompreFaceOptions options;
    private readonly HttpClient http;
    private readonly ILogger<CompreFaceClient> logger = NullLogger<CompreFaceClient>.Instance;
    public string DeploymentVersion => options.DeploymentVersion;
    public double MinimumDetectionProbability => options.MinimumDetectionProbability!.Value;

    public CompreFaceClient(CompreFaceOptions options) : this(options, CreateTransport(options)) { }

    /// <summary>Composition supplies a named factory client with this adapter's private transport.</summary>
    public CompreFaceClient(CompreFaceOptions options, HttpClient client, ILogger<CompreFaceClient>? logger = null)
    {
        ValidateOptions(options);
        this.options = options;
        http = client ?? throw new ArgumentNullException(nameof(client));
        http.Timeout = Timeout.InfiniteTimeSpan;
        this.logger = logger ?? NullLogger<CompreFaceClient>.Instance;
    }

    // Harness uses reflection to inject transport doubles. Production composition uses the public constructor.
    internal CompreFaceClient(CompreFaceOptions options, HttpMessageHandler transport)
    {
        ValidateOptions(options);
        this.options = options;
        http = new HttpClient(transport, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<CompreFaceDetectionResult> DetectAsync(byte[] image, CancellationToken ct = default)
    {
        var info = SelfHostedFaceImage.Inspect(image);
        using var body = new MultipartFormDataContent();
        AddImage(body, image, info, "file");
        using var response = await SendAsync("api/v1/detection/detect", options.DetectionApiKey, body, ct);
        RequireObject(response.RootElement, "result");
        var faces = Required(response.RootElement, "result");
        if (faces.ValueKind != JsonValueKind.Array) throw Invalid();
        if (faces.GetArrayLength() == 0) throw Rejected("face_not_detected");
        if (faces.GetArrayLength() > 1)
        {
            // Distinguish a valid multi-face image from malformed provider data.
            foreach (var face in faces.EnumerateArray()) _ = ParseFace(face, info, enforceProbability: false);
            throw Rejected("face_multiple_faces");
        }
        return ParseFace(faces[0], info);
    }

    public async Task<CompreFaceVerificationResult> VerifyAsync(byte[] source, byte[] target, CancellationToken ct = default)
    {
        var sourceInfo = SelfHostedFaceImage.Inspect(source);
        var targetInfo = SelfHostedFaceImage.Inspect(target);
        // Do not rely on a provider selecting just the largest source or target face.
        var sourceDetection = await DetectAsync(source, ct);
        var targetDetection = await DetectAsync(target, ct);
        using var body = new MultipartFormDataContent();
        AddImage(body, source, sourceInfo, "source_image");
        AddImage(body, target, targetInfo, "target_image");
        using var response = await SendAsync("api/v1/verification/verify", options.VerificationApiKey, body, ct);
        var result = SingleResult(response.RootElement);
        RequireObject(result, "source_image_face", "face_matches");
        var sourceFace = ParseFace(Required(result, "source_image_face"), sourceInfo, enforceProbability: false);
        var targetMatches = Required(result, "face_matches");
        if (targetMatches.ValueKind != JsonValueKind.Array || targetMatches.GetArrayLength() != 1) throw Invalid();
        var targetFace = ParseFace(targetMatches[0], targetInfo, similarity: true, enforceProbability: false);
        var similarity = Score(Required(targetMatches[0], "similarity"));
        if (sourceFace.Bounds != sourceDetection.Bounds || targetFace.Bounds != targetDetection.Bounds) throw Invalid();
        if (sourceFace.Probability < MinimumDetectionProbability || targetFace.Probability < MinimumDetectionProbability)
            throw Rejected("face_detection_low_confidence");
        return new(sourceFace.Bounds, sourceFace.Probability, targetFace.Bounds, targetFace.Probability,
            similarity);
    }

    private async Task<JsonDocument> SendAsync(string path, string key, MultipartFormDataContent body, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var outcome = "invalid-response";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        var uri = new Uri(options.BaseUri!, path + "?limit=0&det_prob_threshold=0&face_plugins=&status=false");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = body, Version = HttpVersion.Version11, VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        request.Headers.Add("x-api-key", key);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
            if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.BadRequest) || response.Headers.Location is not null ||
                response.Content.Headers.ContentType?.MediaType != "application/json" ||
                response.Content.Headers.ContentEncoding.Count != 0 ||
                response.Content.Headers.ContentLength > options.MaximumResponseBytes) throw Invalid();
            using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            var buffer = new byte[options.MaximumResponseBytes + 1];
            try
            {
                var count = 0;
                while (count < buffer.Length)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    var read = await stream.ReadAsync(buffer.AsMemory(count), deadline.Token);
                    deadline.Token.ThrowIfCancellationRequested();
                    if (read == 0) break;
                    count += read;
                }
                if (count == 0 || count > options.MaximumResponseBytes ||
                    response.Content.Headers.ContentLength is long declared && declared != count) throw Invalid();
                using var jsonStream = new MemoryStream(buffer, 0, count, writable: false);
                var json = JsonDocument.Parse(jsonStream, new JsonDocumentOptions { MaxDepth = 16 });
                try
                {
                    RejectDuplicateKeys(json.RootElement);
                    deadline.Token.ThrowIfCancellationRequested();
                    if (response.StatusCode != HttpStatusCode.OK) throw ServiceFailure(json.RootElement);
                    outcome = "response-received";
                    return json;
                }
                catch { json.Dispose(); throw; }
            }
            finally { CryptographicOperations.ZeroMemory(buffer); }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { outcome = "timeout"; throw new AuthenticationFailure("face_provider_timeout", 503); }
        catch (OperationCanceledException) { outcome = "cancelled"; throw; }
        catch (HttpRequestException) { outcome = "unavailable"; throw new AuthenticationFailure("face_provider_unavailable", 503); }
        catch (IOException) { outcome = "unavailable"; throw new AuthenticationFailure("face_provider_unavailable", 503); }
        catch (JsonException) { throw Invalid(); }
        finally
        {
            // Constant operation/outcome labels only. Never log endpoint, headers, body, key or employee identifiers.
            logger.LogInformation("Face transport {Operation} completed {Outcome} in {ElapsedMs} ms.",
                path == "api/v1/detection/detect" ? "detection" : "verification", outcome,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private static AuthenticationFailure ServiceFailure(JsonElement root)
    {
        // CompreFace 1.2.0 returns HTTP400 with these codes for ordinary image
        // rejection. Parse under the same size/type/duplicate-key guards as success;
        // credentials, unknown errors and protocol failures never become recapture.
        RequireObject(root, "code", "message");
        var code = Required(root, "code");
        if (code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var value) ||
            Required(root, "message").ValueKind != JsonValueKind.String) return Invalid();
        return value switch
        {
            28 => Rejected("face_not_detected"),
            31 => Rejected("face_multiple_faces"),
            _ => Invalid()
        };
    }

    private CompreFaceDetectionResult ParseFace(JsonElement face, SelfHostedFaceImageInfo image,
        bool similarity = false, bool enforceProbability = true)
    {
        if (similarity) RequireObject(face, "box", "similarity");
        else RequireObject(face, "box");
        var box = Required(face, "box");
        RequireObject(box, "x_min", "y_min", "x_max", "y_max", "probability");
        var bounds = new FaceBounds(Coordinate(box, "x_min"), Coordinate(box, "y_min"),
            Coordinate(box, "x_max"), Coordinate(box, "y_max"));
        var probability = Score(Required(box, "probability"));
        if (!bounds.IsWithin(image.Width, image.Height)) throw Invalid();
        if (enforceProbability && probability < MinimumDetectionProbability) throw Rejected("face_detection_low_confidence");
        return new(bounds, probability);
    }

    private static JsonElement SingleResult(JsonElement root)
    {
        RequireObject(root, "result");
        var results = Required(root, "result");
        if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() != 1) throw Invalid();
        return results[0];
    }

    // With all optional plugins/status disabled, extra/error fields are protocol failures.
    private static void RequireObject(JsonElement element, params string[] fields)
    {
        if (element.ValueKind != JsonValueKind.Object) throw Invalid();
        foreach (var property in element.EnumerateObject())
            if (!fields.Contains(property.Name, StringComparer.Ordinal)) throw Invalid();
        foreach (var field in fields) _ = Required(element, field);
    }

    private static JsonElement Required(JsonElement element, string field) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(field, out var value) ? value : throw Invalid();
    private static int Coordinate(JsonElement box, string field)
    {
        var coordinate = Required(box, field);
        return coordinate.ValueKind == JsonValueKind.Number && coordinate.TryGetInt32(out var value) ? value : throw Invalid();
    }
    private static double Score(JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var score) &&
        double.IsFinite(score) && score is >= 0 and <= 1 ? score : throw Invalid();

    private static void RejectDuplicateKeys(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            { if (!keys.Add(property.Name)) throw Invalid(); RejectDuplicateKeys(property.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateKeys(item);
    }

    private static void AddImage(MultipartFormDataContent body, byte[] image, SelfHostedFaceImageInfo info, string name)
    {
        var content = new ByteArrayContent(image);
        content.Headers.ContentType = new MediaTypeHeaderValue(info.MediaType);
        body.Add(content, name, "capture" + info.Extension);
    }

    public static void ValidateOptions(CompreFaceOptions options)
    {
        PrivateFaceCertificateTrust.ValidateOptions(options);
        var uri = options.BaseUri;
        if (uri is null || !uri.IsAbsoluteUri || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            uri.AbsolutePath != "/" || uri.Scheme != "https" && !(uri.Scheme == "http" && options.AllowLoopbackHttpForPoc &&
                IPAddress.TryParse(uri.IdnHost.Trim('[', ']'), out var loopback) && IPAddress.IsLoopback(loopback)) ||
            options.MinimumDetectionProbability is not double p || !double.IsFinite(p) || p is <= 0 or > 1 ||
            options.TimeoutSeconds is < 1 or > 60 || options.MaximumResponseBytes is < 256 or > 1048576 ||
            !ValidText(options.DeploymentVersion, 100) || !ValidKey(options.DetectionApiKey) || !ValidKey(options.VerificationApiKey))
            throw new AuthenticationFailure("face_provider_not_configured", 503);
        if (IPAddress.TryParse(uri.IdnHost.Trim('[', ']'), out var address) && !IsPrivateAddress(address, options.AllowLoopbackHttpForPoc))
            throw new AuthenticationFailure("face_provider_not_configured", 503);
    }

    internal static SocketsHttpHandler CreateTransport(CompreFaceOptions options)
    {
        ValidateOptions(options);
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseProxy = false, UseCookies = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
            MaxResponseHeadersLength = 16, MaxConnectionsPerServer = 4,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            SslOptions = new() { CertificateChainPolicy = PrivateFaceCertificateTrust.LoadPolicy(options) },
            ConnectCallback = async (context, ct) =>
            {
                if (!context.DnsEndPoint.Host.Equals(options.BaseUri!.IdnHost.Trim('[', ']'), StringComparison.OrdinalIgnoreCase) ||
                    context.DnsEndPoint.Port != options.BaseUri.Port) throw new HttpRequestException("face_destination_denied");
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
                if (addresses.Length == 0 || addresses.Any(a => !IsPrivateAddress(a, options.AllowLoopbackHttpForPoc)))
                    throw new HttpRequestException("face_destination_denied");
                // A single resolved address and single connection attempt: no fallback/retry of a submission.
                var socket = new Socket(addresses[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(addresses[0], context.DnsEndPoint.Port), ct);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch { socket.Dispose(); throw; }
            }
        };
    }

    public static bool IsPrivateAddress(IPAddress address, bool allowLoopback)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return allowLoopback;
        var bytes = address.GetAddressBytes();
        return address.AddressFamily == AddressFamily.InterNetwork ?
            bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 || bytes[0] == 192 && bytes[1] == 168 :
            address.AddressFamily == AddressFamily.InterNetworkV6 && (bytes[0] & 254) == 252 && address.ScopeId == 0;
    }

    internal static bool ValidText(string? text, int max) => !string.IsNullOrWhiteSpace(text) && text.Length <= max && !text.Any(char.IsControl);
    private static bool ValidKey(string? key) => ValidText(key, 256) && key!.All(c => c is >= '!' and <= '~');
    private static AuthenticationFailure Invalid() => new("face_provider_invalid_result", 503);
    private static AuthenticationFailure Rejected(string code) => new(code, 409);
    public void Dispose() => http.Dispose();
}
