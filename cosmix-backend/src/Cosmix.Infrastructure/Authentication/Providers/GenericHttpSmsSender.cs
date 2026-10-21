using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Providers;
using Microsoft.Extensions.Options;

namespace Cosmix.Infrastructure.Authentication.Providers;

/// <summary>One HTTP attempt to the exact gateway contract documented on SmsGatewayOptions.</summary>
public sealed class GenericHttpSmsSender : IOtpSmsSender
{
    public const string HttpClientName = "Cosmix.Authentication.SmsGateway";
    private readonly IHttpClientFactory _clients;
    private readonly IPhoneNumberNormalizer _normalizer;
    private readonly Uri? _endpoint;
    private readonly string _bearerToken;
    private readonly int _timeoutSeconds;

    public GenericHttpSmsSender(IHttpClientFactory clients, IOptions<SmsGatewayOptions> options, IPhoneNumberNormalizer normalizer)
    {
        _clients = clients;
        _normalizer = normalizer;
        var settings = options.Value;
        _bearerToken = settings.BearerToken ?? string.Empty;
        _timeoutSeconds = settings.TimeoutSeconds;
        var tokenValue = _bearerToken.TrimEnd('=');
        IsConfigured = settings.Enabled && settings.Provider == SmsGatewayOptions.GenericHttpProvider &&
            Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out _endpoint) &&
            _endpoint.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(_endpoint.Host) &&
            _endpoint.UserInfo.Length == 0 && _endpoint.Query.Length == 0 && _endpoint.Fragment.Length == 0 &&
            _bearerToken.Length is >= 1 and <= 4096 &&
            tokenValue.Length > 0 &&
            tokenValue.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~' or '+' or '/') &&
            _timeoutSeconds is >= 1 and <= 60;
    }

    public bool IsConfigured { get; }

    public async Task<SmsSendResult> SendAsync(string phone, string code, string idempotencyKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsConfigured)
            return SmsSendResult.Unavailable;
        var recipient = _normalizer.Normalize(phone);
        if (!OtpProtector.IsCode(code))
            throw new ArgumentException("A six-digit code is required.", nameof(code));
        if (string.IsNullOrEmpty(idempotencyKey) || idempotencyKey.Length > 128 ||
            !idempotencyKey.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            throw new ArgumentException("A bounded opaque idempotency key is required.", nameof(idempotencyKey));

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(new GatewayRequest(recipient, $"Your Cosmix verification code is {code}.", idempotencyKey));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));
        using var client = _clients.CreateClient(HttpClientName);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // Read headers only: no untrusted/raw response body is buffered, parsed or logged.
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Accepted)
                return SmsSendResult.Accepted;
            if ((int)response.StatusCode is >= 400 and <= 499 && response.StatusCode != HttpStatusCode.RequestTimeout)
                return SmsSendResult.Rejected;
            return SmsSendResult.Unknown;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or System.IO.IOException)
        {
            // Cancellation/transport failure may happen after acceptance; caller owns reconciliation.
            return SmsSendResult.Unknown;
        }
    }

    private sealed record GatewayRequest(
        [property: JsonPropertyName("recipient")] string Recipient,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("idempotencyKey")] string IdempotencyKey);
}
