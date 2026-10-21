#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyRequestCancellationBoundary.packages.lock.json
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Cosmix.Api.Authentication;
using Cosmix.Api.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

// Execute the production user-code HTTP cancellation boundary. The authentication-stage
// delegate below only supplies a cancellable operation; real service/JWT propagation is
// separately exercised by VerifyAuthenticationCancellation.cs. No host, SQL or private IO.
// Missing handling, swallowing a live-request cancellation, changing a started response,
// or resuming the pipeline after cancellation must fail these checks.
var checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL: " + label);
    checks++;
}

using var services = new ServiceCollection().BuildServiceProvider();
var boundaryType = typeof(ApiExceptionHandler).Assembly.GetType("Cosmix.Api.Middleware.RequestCancellationMiddleware");
RequestDelegate Pipeline(RequestDelegate next)
{
    var application = new ApplicationBuilder(services);
    application.Use(downstream =>
    {
        // Pass through before the production type exists so the TDD baseline compiles
        // and fails on the actual missing cancellation behavior, not a missing symbol.
        if (boundaryType is null) return downstream;
        var instance = ActivatorUtilities.CreateInstance(services, boundaryType, downstream);
        var method = boundaryType.GetMethod("InvokeAsync", [typeof(HttpContext)])
            ?? throw new InvalidOperationException("Request cancellation boundary lacks InvokeAsync(HttpContext).");
        return context =>
        {
            try { return (Task)method.Invoke(instance, [context])!; }
            catch (TargetInvocationException error) when (error.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        };
    });
    application.Run(next);
    return application.Build();
}

async Task CheckAbortedRequest(bool taskCanceled, bool started)
{
    using var cancellation = new CancellationTokenSource();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var protectedSuccess = false;
    var authenticationCalls = 0;
    var context = new DefaultHttpContext { RequestServices = services, RequestAborted = cancellation.Token };
    if (started) context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
    context.Response.StatusCode = StatusCodes.Status202Accepted;
    context.Response.Body = new MemoryStream();
    var originalBody = started ? Encoding.UTF8.GetBytes("already started") : [];
    context.Response.Body.Write(originalBody);
    var pipeline = Pipeline(async current =>
    {
        authenticationCalls++;
        entered.TrySetResult();
        if (taskCanceled)
            await Task.Delay(Timeout.InfiniteTimeSpan, current.RequestAborted);
        else
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, current.RequestAborted)
                .ContinueWith(_ => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw new OperationCanceledException("Synthetic aborted authentication read.", current.RequestAborted);
        }
        // A cancellation handler must terminate the HTTP request, not resume protected work.
        protectedSuccess = true;
        current.Response.StatusCode = StatusCodes.Status200OK;
    });
    var pending = pipeline(context);
    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    cancellation.Cancel();
    var label = (taskCanceled ? "TaskCanceledException" : "OperationCanceledException") +
        (started ? " after response start" : " before response start");
    try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
    catch (OperationCanceledException error)
    {
        throw new Exception("FAIL: " + label + " escaped the user-code HTTP boundary.", error);
    }
    Check(context.Response.StatusCode == (started ? StatusCodes.Status202Accepted : StatusCodes.Status499ClientClosedRequest),
        label + ": correct response status");
    Check(((MemoryStream)context.Response.Body).ToArray().SequenceEqual(originalBody), label + ": no response body written or cleared");
    Check(authenticationCalls == 1 && !protectedSuccess, label + ": protected success never executes or replays");
    Check(!context.Items.ContainsKey(CurrentAuthentication.ItemKey) && context.User.Identity?.IsAuthenticated != true,
        label + ": no authenticated identity installed");
}

foreach (var taskCanceled in new[] { true, false })
foreach (var started in new[] { false, true })
    await CheckAbortedRequest(taskCanceled, started);

async Task CheckPropagation(Exception original, bool abortRequest, string label)
{
    using var cancellation = new CancellationTokenSource();
    if (abortRequest) cancellation.Cancel();
    var context = new DefaultHttpContext { RequestServices = services, RequestAborted = cancellation.Token };
    context.Response.Body = new MemoryStream();
    var reached = 0;
    var pipeline = Pipeline(_ => { reached++; return Task.FromException(original); });
    Exception? observed = null;
    try { await pipeline(context); }
    catch (Exception error) { observed = error; }
    Check(ReferenceEquals(observed, original), label + ": original exception propagates unchanged");
    Check(reached == 1 && context.Response.StatusCode == StatusCodes.Status200OK && context.Response.Body.Length == 0,
        label + ": pipeline is not replayed and response is unchanged");
}

using (var unrelated = new CancellationTokenSource())
{
    unrelated.Cancel();
    await CheckPropagation(new OperationCanceledException("Synthetic unrelated cancellation.", unrelated.Token), false,
        "live request with unrelated OperationCanceledException");
    await CheckPropagation(new TaskCanceledException("Synthetic unrelated task cancellation.", null, unrelated.Token), false,
        "live request with unrelated TaskCanceledException");
}
await CheckPropagation(new InvalidOperationException("Synthetic unexpected failure."), false, "unexpected live-request error");
await CheckPropagation(new InvalidOperationException("Synthetic unexpected failure after disconnect."), true,
    "unexpected error with aborted request");
await CheckPropagation(new IOException("Synthetic transport failure outside the scoped cancellation handler."), true,
    "non-cancellation error with aborted request");

{
    var context = new DefaultHttpContext { RequestServices = services };
    context.Response.Body = new MemoryStream();
    var calls = 0;
    var pipeline = Pipeline(current =>
    {
        calls++;
        current.Response.StatusCode = StatusCodes.Status204NoContent;
        return Task.CompletedTask;
    });
    await pipeline(context);
    Check(calls == 1 && context.Response.StatusCode == StatusCodes.Status204NoContent && context.Response.Body.Length == 0,
        "successful request passes through once unchanged");
}

Check(boundaryType is not null, "production request cancellation middleware was exercised");
Console.WriteLine($"PASS: {checks} request cancellation boundary checks. In-memory HTTP only; no debugger, signed JWT, SQL or browser acceptance.");

sealed class StartedResponseFeature : HttpResponseFeature
{
    public override bool HasStarted => true;
}
