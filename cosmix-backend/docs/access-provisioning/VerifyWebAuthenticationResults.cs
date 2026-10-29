#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyWebAuthenticationResults.packages.lock.json
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Api.Controllers.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Application.Authentication.Web;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Notifications;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

// Real controller/service/domain behavior; only SQL, signing and secret/provider IO are replaced.
// A rejected browser login/refresh must return its committed result without throwing an
// AuthenticationFailure (including one caught by a controller or middleware).
var checks = 0;
var failures = new List<string>();
void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    checks++;
}
async Task Run(string name, Func<Task> action)
{
    try { await action(); Console.WriteLine("PASS: " + name); }
    catch (Exception error) { failures.Add(name + ": " + error.GetType().Name + " " + error.Message); }
}
async Task<ActionResult<BrowserSessionResponse>> NoAuthenticationThrow(Func<Task<ActionResult<BrowserSessionResponse>>> action)
{
    var thrown = 0;
    void Observe(object? _, FirstChanceExceptionEventArgs args)
    {
        if (args.Exception is AuthenticationFailure) Interlocked.Increment(ref thrown);
    }
    AppDomain.CurrentDomain.FirstChanceException += Observe;
    try
    {
        ActionResult<BrowserSessionResponse> result;
        try { result = await action(); }
        catch (AuthenticationFailure error)
        {
            throw new Exception($"expected an HTTP result, but {error.Code} was thrown ({thrown} first-chance exceptions)");
        }
        Check(thrown == 0, "expected rejection threw a first-chance AuthenticationFailure");
        return result;
    }
    finally { AppDomain.CurrentDomain.FirstChanceException -= Observe; }
}
void Denied(ActionResult<BrowserSessionResponse> result, int status, string title, WebResultFixture fixture,
    bool clearCookies, int expectedCommits = 1)
{
    Check(result.Result is ObjectResult { Value: ProblemDetails }, "rejection returns ProblemDetails");
    var response = (ObjectResult)result.Result!;
    var problem = (ProblemDetails)response.Value!;
    Check(response.StatusCode == status && problem.Status == status && problem.Title == title,
        "rejection preserves status and public error code");
    var json = JsonSerializer.Serialize(response.Value);
    Check(!json.Contains(WebResultFixture.AccessSecret) && !json.Contains(WebResultFixture.RefreshSecret) &&
        !json.Contains("AccessToken") && !json.Contains("RefreshToken"), "rejection contains no credentials");
    var cookies = fixture.Context.Response.Headers.SetCookie.ToArray();
    Check(clearCookies ? cookies.Length == 3 && new[] { "Access", "Refresh", "Browser" }.All(name =>
        cookies.Any(cookie => cookie!.StartsWith("__Host-Cosmix." + name + "=;") && cookie.Contains("expires="))) :
        cookies.Length == 0, "only refresh 401 clears all browser authentication cookies");
    Check(fixture.Repository.Commits == expectedCommits && fixture.Repository.Rollbacks == 0,
        "rejection returns after the authentication transaction commits");
}

foreach (var token in new string?[] { null, new('U', 43) })
await Run(token is null ? "refresh without cookie" : "unknown refresh token", async () =>
{
    var fixture = new WebResultFixture(token);
    Denied(await NoAuthenticationThrow(() => fixture.Controller.Refresh(default)), 401,
        "invalid_authentication", fixture, clearCookies: true);
    Check(fixture.Repository.CommittedRateCount == 2 && fixture.Repository.Sessions.Count == 0,
        "anonymous refresh consumes edge budget without issuing a session");
});

foreach (var denial in new[] { "unknown", "wrong", "expired" })
await Run(denial + " OTP", async () =>
{
    var fixture = new WebResultFixture();
    if (denial == "expired") fixture.Clock.Now = fixture.Challenge.ExpiresAt;
    var id = denial == "unknown" ? Guid.NewGuid() : fixture.Challenge.Id;
    var code = denial == "wrong" ? "000000" : "123456";
    Denied(await NoAuthenticationThrow(() => fixture.Controller.Verify(new(id, code), default)), 401,
        "invalid_authentication", fixture, clearCookies: false);
    Check(fixture.Repository.CommittedFailedAttempts == (denial == "wrong" ? 1 : 0),
        "only an attempted usable OTP consumes a challenge attempt");
    Check(fixture.Repository.Sessions.Count == 0 && fixture.Challenge.ConsumedAt is null,
        "denied OTP never issues a session or consumes proof");
    Check(fixture.Repository.CommittedAuditCount == (denial == "unknown" ? 0 : 1),
        "known rejected OTP audit commits with the result");
    Check(fixture.Repository.CommittedRateCount == (denial == "unknown" ? 2 : 4),
        "verification budgets commit for known and unknown challenge rejections");
});

foreach (var refresh in new[] { false, true })
await Run((refresh ? "refresh" : "OTP") + " edge throttling", async () =>
{
    var fixture = new WebResultFixture();
    var bucket = new AuthRateBucket("web-verify-global", fixture.Clock.Now, fixture.Clock.Now.AddMinutes(1));
    for (var i = 0; i < 100; i++) bucket.TryConsume(100, fixture.Clock.Now);
    fixture.Repository.Rates.Add(bucket.Key, bucket);
    var result = await NoAuthenticationThrow(() => refresh ? fixture.Controller.Refresh(default) :
        fixture.Controller.Verify(new(fixture.Challenge.Id, "123456"), default));
    Denied(result, 429, "temporarily_limited", fixture, clearCookies: false);
    Check(fixture.Repository.CommittedRateCount == 100 && fixture.Repository.Sessions.Count == 0 &&
        fixture.Challenge.FailedAttempts == 0, "throttling leaves challenge and sessions untouched");
});

await Run("successful OTP and refresh use secure cookie adapter", async () =>
{
    var fixture = new WebResultFixture();
    var verified = await NoAuthenticationThrow(() => fixture.Controller.Verify(new(fixture.Challenge.Id, "123456"), default));
    void Complete(ActionResult<BrowserSessionResponse> result, string expectedRefresh)
    {
        Check(result.Result is OkObjectResult { Value: BrowserSessionResponse }, "success returns browser-safe session metadata");
        var response = (BrowserSessionResponse)((OkObjectResult)result.Result!).Value!;
        Check(response.SessionId == fixture.Repository.Sessions.Single().Id && response.NextStep == "authenticated",
            "success identifies the persisted authenticated session");
        Check(response.AccessTokenExpiresAt == fixture.Clock.Now.AddMinutes(5) &&
            response.RefreshTokenExpiresAt == fixture.Clock.Now.AddHours(1), "browser metadata preserves credential expiry");
        var json = JsonSerializer.Serialize(response);
        Check(!json.Contains(WebResultFixture.AccessSecret) && !json.Contains(WebResultFixture.RefreshSecret),
            "successful JSON does not expose credentials");
        var cookies = fixture.Context.Response.Headers.SetCookie.ToArray();
        Check(cookies.Length == 2 && cookies.Any(c => c!.StartsWith("__Host-Cosmix.Access=" + WebResultFixture.AccessSecret)) &&
            cookies.Any(c => c!.StartsWith("__Host-Cosmix.Refresh=" + expectedRefresh)),
            "success sends access and refresh credentials in cookies");
        Check(cookies.All(c => c!.Contains("; secure") && c.Contains("; httponly") &&
            c.Contains("; samesite=strict") && c.Contains("; path=/") && c.Contains("expires=") &&
            !c.Contains("domain=", StringComparison.OrdinalIgnoreCase)), "cookies retain required security attributes");
        Check(fixture.Context.Response.Headers.CacheControl == "no-store", "credentials disable response caching");
    }
    Complete(verified, WebResultFixture.RefreshSecret);
    Check(fixture.Repository.Commits == 1 && fixture.Repository.CommittedConsumedAt == fixture.Clock.Now &&
        fixture.Repository.CommittedAuditCount == 1, "OTP proof, session and audit commit before success");
    var previous = fixture.Repository.RefreshTokens.Single();
    fixture.Context.Response.Headers.Clear();
    fixture.SetRefreshCookie(WebResultFixture.RefreshSecret);
    var refreshed = await NoAuthenticationThrow(() => fixture.Controller.Refresh(default));
    Complete(refreshed, new string('N', 43));
    Check(fixture.Repository.Commits == 2 && previous.ConsumedAt == fixture.Clock.Now &&
        previous.ReplacedByTokenId == fixture.Repository.RefreshTokens.Last().Id &&
        fixture.Repository.CommittedAuditCount == 2, "refresh rotation and audit commit together");
    fixture.Context.Response.Headers.Clear();
    Denied(await NoAuthenticationThrow(() => fixture.Controller.Refresh(default)), 401,
        "invalid_authentication", fixture, clearCookies: true, expectedCommits: 3);
    Check(fixture.Repository.CommittedSessionRevoked && fixture.Repository.CommittedAuditCount == 3,
        "refresh replay commits family revocation and audit before rejection");
});

await Run("direct service callers retain throwing compatibility", async () =>
{
    foreach (var refresh in new[] { false, true })
    {
        var fixture = new WebResultFixture();
        try
        {
            var context = new AuthRequestContext("synthetic-ip", "synthetic-compatibility");
            await (refresh ? fixture.Service.RefreshAsync(new("", WebResultFixture.BrowserSecret), context, default) :
                fixture.Service.VerifyAsync(new(Guid.NewGuid(), "123456", WebResultFixture.BrowserSecret), context, default));
            throw new Exception("existing service API silently changed its rejection contract");
        }
        catch (AuthenticationFailure error)
        {
            Check(error.StatusCode == 401 && error.Code == "invalid_authentication" && fixture.Repository.Commits == 1,
                "existing throwing API rejects only after commit");
        }
    }
});

foreach (var refresh in new[] { false, true })
await Run((refresh ? "refresh" : "OTP") + " cancellation and repository failure propagate", async () =>
{
    foreach (var cancelled in new[] { true, false })
    {
        var fixture = new WebResultFixture();
        using var cancellation = new CancellationTokenSource();
        if (cancelled) cancellation.Cancel();
        else fixture.Repository.Failure = new IOException("synthetic repository failure");
        try
        {
            await (refresh ? fixture.Controller.Refresh(cancellation.Token) :
                fixture.Controller.Verify(new(fixture.Challenge.Id, "123456"), cancellation.Token));
            throw new Exception("cancellation or infrastructure failure was converted to an HTTP authentication result");
        }
        catch (OperationCanceledException error) when (cancelled)
        { Check(error.CancellationToken == cancellation.Token, "original cancellation token propagates"); }
        catch (IOException error) when (!cancelled)
        { Check(ReferenceEquals(error, fixture.Repository.Failure), "original infrastructure failure propagates"); }
        Check(fixture.Repository.Commits == 0 && fixture.Context.Response.Headers.SetCookie.Count == 0,
            "unsuccessful transaction cannot issue or clear cookies");
    }
});

foreach (var failure in failures) Console.Error.WriteLine("FAIL: " + failure);
if (failures.Count > 0) throw new Exception($"{failures.Count} web authentication regression cases failed; {checks} checks passed.");
Console.WriteLine($"PASS: {checks} web authentication result checks. In-memory component verification; no SQL, provider or browser acceptance.");

sealed class WebResultFixture
{
    public const string BrowserSecret = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
    public const string AccessSecret = "synthetic-access-credential";
    public const string RefreshSecret = "RRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR";
    public ResultClock Clock { get; } = new();
    public ResultRepository Repository { get; }
    public MobileOtpChallenge Challenge { get; }
    public DefaultHttpContext Context { get; } = new();
    public WebAuthenticationController Controller { get; }
    public WebAuthenticationService Service { get; }

    public WebResultFixture(string? refreshCookie = null)
    {
        var created = Clock.Now.AddDays(-1);
        var employee = new Employee(created, "synthetic-creator");
        employee.UpdateDraft(new EmployeeDraftDetails
        {
            FullLegalName = "Synthetic Browser Result Check", IdentityDocumentType = Enum.GetValues<IdentityDocumentType>()[0],
            IdentityDocumentNumber = "synthetic-id", PersonalPhone = "+15555550123",
            JoinedOn = DateOnly.FromDateTime(created.UtcDateTime)
        });
        employee.SelectLoginPhone("+15555550123", false);
        var submission = employee.SubmitForApproval(Guid.NewGuid(), created, "synthetic-no-file", "v1",
            new string('A', 64), "synthetic-key-reference", 1);
        var decision = employee.DecideSubmission(submission, OnboardingDecision.Approve, Guid.NewGuid(),
            Guid.NewGuid(), "Synthetic approval", created);
        var account = new UserAccount(employee, decision, "synthetic@example.invalid", created);
        var role = new Role("WEB_RESULT_CHECK", "Browser result check", Guid.NewGuid(), created);
        var assignment = new UserRoleAssignment(account, role, Guid.NewGuid(), created, null);
        var scope = new UserAccessScope(assignment, AccessScopeKind.Self);
        account.ConfigureInitialAccess(assignment, scope, created);
        var phone = new LoginPhoneBinding(account, "+15555550123", submission.Id, created);
        account.ProvisionInitialWebCredentials("synthetic@example.invalid", "synthetic-unused-hash",
            WebPasswordFormats.IdentityV3, phone, assignment, scope, created);
        Challenge = MobileOtpChallenge.CreateWebLogin(Guid.NewGuid(), phone, account.SecurityVersion,
            Digest("browser:" + BrowserSecret), "synthetic-verifier", Clock.Now.AddMinutes(-1), Clock.Now.AddMinutes(4), 3);
        Repository = new(new(account, phone, [new(assignment, scope)], EmployeeStatus.Active, false), Challenge);
        var protector = ResultProxy.Create<IOtpProtector>((method, args) => method.Name switch
        {
            "get_IsConfigured" => true,
            nameof(IOtpProtector.Digest) => Digest((string)args[0]!),
            nameof(IOtpProtector.Verify) => (Guid)args[0]! == Challenge.Id && (string)args[1]! == "123456" &&
                (string)args[2]! == "synthetic-verifier",
            nameof(IOtpProtector.NewRefreshToken) => Repository.RefreshTokens.Count == 0 ? RefreshSecret : new string('N', 43),
            _ => throw new InvalidOperationException("Unexpected secret operation: " + method.Name)
        });
        var common = new MobileAuthenticationOptions
        {
            Enabled = true, OtpLifetimeSeconds = 300, ResendCooldownSeconds = 30, MaximumOtpAttempts = 3,
            RateWindowSeconds = 60, RequestsPerPhone = 3, RequestsPerIp = 10, RequestsGlobal = 100,
            VerificationAttemptsPerPhone = 3, VerificationAttemptsPerIp = 10, VerificationAttemptsGlobal = 100,
            AccessTokenLifetimeSeconds = 300, RefreshTokenLifetimeSeconds = 3600, SessionLifetimeSeconds = 86400,
            MinimumRequestDurationMilliseconds = 100, BusinessTimeZoneId = "UTC"
        };
        var web = new WebAuthenticationOptions
        {
            Enabled = true, MaximumPasswordFailures = 3, PasswordLockoutSeconds = 60,
            PasswordAttemptsPerAccount = 3, PasswordAttemptsPerEmail = 3,
            PasswordAttemptsPerIp = 10, PasswordAttemptsGlobal = 100
        };
        var configuredSender = ResultProxy.Create<IOtpSmsSender>((method, _) => method.Name == "get_IsConfigured" ? true :
            throw new InvalidOperationException("Unexpected SMS operation"));
        var tokens = ResultProxy.Create<IAccessTokenIssuer>((method, _) => method.Name switch
        { "get_IsConfigured" => true, nameof(IAccessTokenIssuer.Issue) => AccessSecret, _ => throw new InvalidOperationException() });
        Service = new WebAuthenticationService(Repository, ResultProxy.Unused<IWebAuthenticationRepository>(),
            ResultProxy.Unused<IWebPasswordVerifier>(), protector, configuredSender, tokens, web, common, Clock);
        Context.Request.Scheme = "https";
        Context.TraceIdentifier = "synthetic-web-result";
        SetRefreshCookie(refreshCookie);
        Controller = new(Service, null!, null!) { ControllerContext = new ControllerContext { HttpContext = Context } };
    }
    public void SetRefreshCookie(string? secret) => Context.Request.Headers.Cookie = "__Host-Cosmix.Browser=" + BrowserSecret +
        (secret is null ? "" : "; __Host-Cosmix.Refresh=" + secret);
    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

sealed class ResultRepository(AuthenticationAccount account, MobileOtpChallenge challenge) : IAuthenticationRepository
{
    public Dictionary<string, AuthRateBucket> Rates { get; } = [];
    public List<UserSession> Sessions { get; } = [];
    public List<RefreshToken> RefreshTokens { get; } = [];
    private readonly List<AuditEvent> audits = [];
    public Exception? Failure { get; set; }
    public int Commits { get; private set; }
    public int Rollbacks { get; private set; }
    public int CommittedRateCount { get; private set; }
    public int CommittedFailedAttempts { get; private set; }
    public int CommittedAuditCount { get; private set; }
    public DateTimeOffset? CommittedConsumedAt { get; private set; }
    public bool CommittedSessionRevoked { get; private set; }
    public async Task<T> AtomicAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            if (Failure is not null) throw Failure;
            var result = await action(ct);
            ct.ThrowIfCancellationRequested();
            Commits++;
            CommittedRateCount = Rates.Values.Sum(bucket => bucket.Count);
            CommittedFailedAttempts = challenge.FailedAttempts;
            CommittedConsumedAt = challenge.ConsumedAt;
            CommittedAuditCount = audits.Count;
            CommittedSessionRevoked = Sessions.Any(session => session.RevokedAt.HasValue);
            return result;
        }
        catch { Rollbacks++; throw; }
    }
    public Task<AuthenticationAccount?> GetAccountAsync(Guid id, DateTimeOffset now, CancellationToken ct) => Task.FromResult(id == account.Account.Id ? account : null);
    public Task<MobileOtpChallenge?> GetChallengeAsync(Guid id, CancellationToken ct) => Task.FromResult(id == challenge.Id ? challenge : null);
    public Task<AuthRateBucket?> GetRateBucketAsync(string key, CancellationToken ct) => Task.FromResult(Rates.GetValueOrDefault(key));
    public Task<RefreshToken?> FindRefreshTokenAsync(string hash, CancellationToken ct) => Task.FromResult(RefreshTokens.SingleOrDefault(token => token.TokenHash == hash));
    public void AddRateBucket(AuthRateBucket bucket) => Rates.Add(bucket.Key, bucket);
    public void AddAudit(AuditEvent audit) => audits.Add(audit);
    public void AddSession(UserSession session, RefreshToken token) { Sessions.Add(session); AddRefreshToken(token); }
    public void AddRefreshToken(RefreshToken token)
    {
        // EF normally fixes up this navigation; the controlled in-memory persistence adapter must do so too.
        typeof(RefreshToken).GetProperty(nameof(RefreshToken.UserSession))!.SetValue(token, Sessions.Single(s => s.Id == token.UserSessionId));
        RefreshTokens.Add(token);
    }
    public Task<AuthenticationAccount?> FindByPhoneAsync(string phone, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<MobileOtpChallenge>> GetOpenChallengesAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task<MobileOtpChallenge?> GetLatestChallengeAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task<RegisteredDevice?> FindDeviceAsync(Guid id, string hash, CancellationToken ct) => throw new NotSupportedException();
    public Task<RegisteredDevice?> GetDeviceAsync(Guid id, Guid deviceId, CancellationToken ct) => throw new NotSupportedException();
    public Task<UserSession?> GetSessionAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<UserSession>> GetSessionsAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<UserSession>> GetActiveSessionsPageAsync(Guid id, DateTimeOffset now, int page, int size, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<RegisteredDevice>> GetDevicesAsync(Guid id, int page, int size, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<RegisteredDevice>> GetNonRevokedDevicesAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    public void AddChallenge(MobileOtpChallenge value, OtpDelivery delivery) => throw new NotSupportedException();
    public void AddDevice(RegisteredDevice device) => throw new NotSupportedException();
    public void AddNotification(Notification notification) => throw new NotSupportedException();
}

sealed class ResultClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public class ResultProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;
    public static T Create<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    {
        var proxy = Create<T, ResultProxy>();
        ((ResultProxy)(object)proxy).Handler = handler;
        return proxy;
    }
    public static T Unused<T>() where T : class => Create<T>((method, _) =>
        throw new InvalidOperationException("Unexpected collaborator: " + typeof(T).Name + "." + method.Name));
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
}
