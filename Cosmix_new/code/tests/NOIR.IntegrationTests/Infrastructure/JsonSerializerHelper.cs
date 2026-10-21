namespace NOIR.IntegrationTests.Infrastructure;

using System.Text.Json.Serialization;

/// <summary>
/// Provides shared JSON serializer options for integration tests.
/// Matches the API's JSON configuration which serializes enums as strings.
/// </summary>
public static class JsonSerializerHelper
{
    /// <summary>
    /// JSON serializer options matching the API configuration.
    /// Uses JsonStringEnumConverter for enum serialization as strings.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

/// <summary>
/// Extension methods for HTTP content deserialization in integration tests.
/// </summary>
public static class HttpContentExtensions
{
    /// <summary>
    /// Reads HTTP content as JSON using the shared serializer options.
    /// This ensures enums are deserialized correctly from string values.
    /// </summary>
    public static async Task<T?> ReadFromJsonWithEnumsAsync<T>(
        this HttpContent content,
        CancellationToken cancellationToken = default)
    {
        return await content.ReadFromJsonAsync<T>(
            JsonSerializerHelper.Options,
            cancellationToken);
    }
}

/// <summary>
/// Extension methods for HttpClient to send JSON with enum string serialization.
/// The API expects enums serialized as strings. Default PostAsJsonAsync sends enums as integers.
/// </summary>
public static class HttpClientJsonExtensions
{
    /// <summary>
    /// Sends a POST request with JSON body using enum-aware serializer options.
    /// </summary>
    public static Task<HttpResponseMessage> PostAsJsonWithEnumsAsync<T>(
        this HttpClient client,
        string requestUri,
        T value,
        CancellationToken cancellationToken = default)
    {
        return client.PostAsJsonAsync(requestUri, value, JsonSerializerHelper.Options, cancellationToken);
    }

    /// <summary>
    /// Sends a PUT request with JSON body using enum-aware serializer options.
    /// </summary>
    public static Task<HttpResponseMessage> PutAsJsonWithEnumsAsync<T>(
        this HttpClient client,
        string requestUri,
        T value,
        CancellationToken cancellationToken = default)
    {
        return client.PutAsJsonAsync(requestUri, value, JsonSerializerHelper.Options, cancellationToken);
    }
}
