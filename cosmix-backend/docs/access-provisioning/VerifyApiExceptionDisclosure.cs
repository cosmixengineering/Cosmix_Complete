#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyApiExceptionDisclosure.packages.lock.json
using System.Text.Json;
using Cosmix.Api.Middleware;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Real handler, JSON problem writer and correlation middleware; only the response transport and
// private logger are in memory. No host, database, provider, persisted keys or account writes.
// Restoring unrestricted exception detail must fail the raw-message sentinel checks below.
const string sentinel = "PRIVATE_EXCEPTION_SENTINEL_auth-database-secret";
var checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    checks++;
}

var privateLog = new CapturedExceptionLog();
var registrations = new ServiceCollection();
registrations.AddLogging();
// Same customization as Program.cs; this is a component check, not execution of the composition root.
registrations.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
using var services = registrations.BuildServiceProvider();
var handler = new ApiExceptionHandler(services.GetRequiredService<IProblemDetailsService>(), privateLog);

var cases = new (string Name, Exception Error, int Status, string Title, string? Detail)[]
{
    ("unexpected failure", new InvalidOperationException(sentinel), 500, "An unexpected error occurred", null),
    ("database update failure", new DbUpdateException(sentinel, new InvalidOperationException(sentinel)),
        500, "An unexpected error occurred", null),
    ("database concurrency failure", new DbUpdateConcurrencyException(sentinel), 409, "concurrent_change_retry", null),
    ("oversized request", new BadHttpRequestException(sentinel, StatusCodes.Status413PayloadTooLarge),
        413, "Request body too large", null),
    ("known authentication failure", new AuthenticationFailure("invalid_authentication", 401),
        401, "invalid_authentication", null),
    ("known validation detail", new RequestValidationException("An effective date is required."),
        400, "Invalid request", "An effective date is required."),
    ("known missing resource detail", new ResourceNotFoundException("Requested item was not found."),
        404, "Resource not found", "Requested item was not found."),
    ("cancellation without an aborted request", new OperationCanceledException(sentinel),
        500, "An unexpected error occurred", null)
};

foreach (var item in cases)
{
    var responseFeature = new MemoryResponseFeature();
    var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "server-trace-" + checks };
    context.Features.Set<IHttpResponseFeature>(responseFeature);
    context.Response.Body = responseFeature.Body;
    context.Request.Headers.Accept = "application/problem+json";
    context.Request.Headers["X-Correlation-ID"] = "untrusted-client-trace";
    var beforeLogs = privateLog.Entries.Count;
    var handled = false;
    await new CorrelationIdMiddleware(async current =>
    {
        handled = await handler.TryHandleAsync(current, item.Error, current.RequestAborted);
    }).InvokeAsync(context);
    // DefaultHttpContext has no server transport; dispatch its recorded OnStarting callbacks explicitly.
    await responseFeature.StartAsync();
    responseFeature.Body.Position = 0;
    using var reader = new StreamReader(responseFeature.Body, leaveOpen: true);
    var body = await reader.ReadToEndAsync();
    using var document = JsonDocument.Parse(body);
    var problem = document.RootElement;

    Check(handled && context.Response.StatusCode == item.Status, item.Name + ": handled HTTP status");
    Check(context.Response.ContentType?.StartsWith("application/problem+json", StringComparison.Ordinal) == true,
        item.Name + ": real problem JSON content type");
    Check(problem.GetProperty("status").GetInt32() == item.Status &&
        problem.GetProperty("title").GetString() == item.Title, item.Name + ": problem status and title");
    Check(!body.Contains(sentinel, StringComparison.Ordinal), item.Name + ": no raw exception secret in response");
    var detail = problem.TryGetProperty("detail", out var property) && property.ValueKind != JsonValueKind.Null
        ? property.GetString() : null;
    Check(detail == item.Detail, item.Name + ": only explicitly safe detail is disclosed");
    Check(problem.GetProperty("traceId").GetString() == context.TraceIdentifier &&
        context.Response.Headers["X-Correlation-ID"].ToString() == context.TraceIdentifier,
        item.Name + ": body and header retain server trace correlation");
    Check(!body.Contains("untrusted-client-trace", StringComparison.Ordinal), item.Name + ": client cannot choose trace");
    if (item.Status == 500)
    {
        Check(privateLog.Entries.Count == beforeLogs + 1, item.Name + ": unexpected error logged once");
        var entry = privateLog.Entries[^1];
        Check(entry.Level == LogLevel.Error && ReferenceEquals(entry.Error, item.Error),
            item.Name + ": private server diagnostics retain original exception");
        Check(entry.TraceId == context.TraceIdentifier, item.Name + ": private log correlates with public trace");
    }
    else
    {
        Check(privateLog.Entries.Count == beforeLogs, item.Name + ": known failure is not logged as unexpected");
    }
    Check(!context.RequestAborted.IsCancellationRequested, item.Name + ": no simulated client abort");
    responseFeature.Body.Dispose();
}

Console.WriteLine($"PASS: {checks} handler disclosure/correlation checks. In-memory HTTP components only; no host or SQL acceptance.");

sealed class CapturedExceptionLog : ILogger<ApiExceptionHandler>
{
    public List<(LogLevel Level, Exception? Error, string? TraceId)> Entries { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var fields = state as IEnumerable<KeyValuePair<string, object?>>;
        var trace = fields?.FirstOrDefault(pair => pair.Key == "TraceId").Value?.ToString();
        Entries.Add((logLevel, exception, trace));
    }
}

sealed class MemoryResponseFeature : IHttpResponseFeature
{
    private readonly List<(Func<object, Task> Callback, object State)> starting = [];
    public int StatusCode { get; set; } = 200;
    public string? ReasonPhrase { get; set; }
    public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
    public Stream Body { get; set; } = new MemoryStream();
    public bool HasStarted { get; private set; }
    public void OnStarting(Func<object, Task> callback, object state) => starting.Add((callback, state));
    public void OnCompleted(Func<object, Task> callback, object state) { }
    public async Task StartAsync()
    {
        for (var index = starting.Count - 1; index >= 0; index--)
            await starting[index].Callback(starting[index].State);
        HasStarted = true;
    }
}
