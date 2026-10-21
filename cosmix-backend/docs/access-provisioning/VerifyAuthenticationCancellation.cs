#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyAuthenticationCancellation.packages.lock.json
using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Cosmix.Api.Authentication;
using Cosmix.Api.Middleware;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Application.Authentication.Web;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Real web session validator and configured JWT callbacks. Persisted reads are replaced with
// in-memory domain objects and controllable cancellable tasks; no host/SQL/provider/file IO.
// Catching cancellation as success, replacing RequestAborted, or skipping MFA must fail these checks.
var checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    checks++;
}
async Task ExpectCancellation(Task pending, CancellationToken token, string label)
{
    try
    {
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        throw new Exception(label + ": cancellation was swallowed");
    }
    catch (OperationCanceledException error)
    {
        Check(error.CancellationToken == token && token.IsCancellationRequested,
            label + ": original request token propagated");
        Check(pending.IsCanceled, label + ": operation did not complete successfully");
    }
}
void IdentityDenied(DefaultHttpContext context, string label)
{
    Check(!context.Items.ContainsKey(CurrentAuthentication.ItemKey), label + ": no validated identity installed");
    var current = new CurrentAuthentication(new HttpContextAccessor { HttpContext = context });
    try { current.Require(); throw new Exception(label + ": authenticated access was granted"); }
    catch (AuthenticationFailure error)
    {
        Check(error.StatusCode == 401 && error.Code == "invalid_authentication", label + ": current actor denied");
    }
}

var stages = new[] { "session", "account", "proof" };
foreach (var browser in new[] { true, false })
foreach (var stage in stages)
{
    using var cancellation = new CancellationTokenSource();
    var fixture = new CancellationFixture { BlockAt = stage, ExpectedToken = cancellation.Token };
    var originalVersion = fixture.Account.SecurityVersion;
    var originalConsumedAt = fixture.Proof.ConsumedAt;
    var pending = fixture.Validate(browser, cancellation.Token);
    await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    cancellation.Cancel();
    var label = (browser ? "browser" : "server actor") + " cancellation at " + stage;
    await ExpectCancellation(pending, cancellation.Token, label);
    Check(fixture.Reads.SequenceEqual(stages.Take(Array.IndexOf(stages, stage) + 1)),
        label + ": no reads beyond cancelled boundary");
    Check(fixture.Account.SecurityVersion == originalVersion && fixture.Session.RevokedAt is null &&
        fixture.Proof.ConsumedAt == originalConsumedAt && fixture.Proof.RevokedAt is null,
        label + ": cancellation does not mutate session or MFA evidence");
    fixture.BlockAt = null;
    fixture.ExpectedToken = default;
    fixture.Reads.Clear();
    var retry = await fixture.Validate(browser, default);
    Check(retry is { MfaSatisfied: true, Client: SessionClient.Web } && retry.SessionId == fixture.Session.Id,
        label + ": fresh request still performs valid MFA-backed validation");
    Check(fixture.Reads.SequenceEqual(stages), label + ": retry rereads all persisted evidence");
}

foreach (var stage in stages)
{
    using var cancellation = new CancellationTokenSource();
    var fixture = new CancellationFixture { BlockAt = stage, ExpectedToken = cancellation.Token };
    using var boundary = new CancellationBoundary(fixture);
    var context = boundary.Context(cancellation.Token);
    var validated = await boundary.TokenContext(context);
    var pending = boundary.Options.Events.TokenValidated(validated);
    await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    cancellation.Cancel();
    await ExpectCancellation(pending, cancellation.Token, "JWT callback cancellation at " + stage);
    Check(validated.Result?.Succeeded != true, stage + ": callback never reports authentication success");
    IdentityDenied(context, stage);
}

// A valid control proves the cancellation scenarios reach actual session/account/MFA validation.
{
    var fixture = new CancellationFixture();
    using var boundary = new CancellationBoundary(fixture);
    var context = boundary.Context(default);
    await boundary.Options.Events.TokenValidated(await boundary.TokenContext(context));
    var identity = new CurrentAuthentication(new HttpContextAccessor { HttpContext = context }).Require();
    Check(identity.AccountId == fixture.Account.Id && identity.EmployeeId == fixture.Account.EmployeeId &&
        identity.SessionId == fixture.Session.Id && identity.MfaSatisfied, "valid callback installs real service result");
    Check(fixture.Reads.SequenceEqual(stages), "valid callback checks session, account and consumed MFA proof");
}

foreach (var denial in new[] { "revoked session", "stale security version", "revoked proof", "missing proof", "wrong browser" })
{
    var fixture = new CancellationFixture();
    switch (denial)
    {
        case "revoked session": fixture.Session.Revoke("synthetic revocation", fixture.Clock.GetUtcNow()); break;
        case "stale security version": fixture.Account.InvalidateSecurityVersion(); break;
        case "revoked proof": fixture.Proof.Revoke(fixture.Clock.GetUtcNow()); break;
        case "missing proof": fixture.MissingProof = true; break;
    }
    using var boundary = new CancellationBoundary(fixture);
    var context = boundary.Context(default, denial == "wrong browser" ? new string('W', 43) : null);
    var validated = await boundary.TokenContext(context);
    await boundary.Options.Events.TokenValidated(validated);
    Check(validated.Result?.Failure is not null, denial + ": callback rejects invalid state");
    IdentityDenied(context, denial);
}

// Characterize the existing .NET 10 exception boundary: an aborted auth read is handled before
// ApiExceptionHandler, without adding a cancellation catch or creating an authenticated actor.
{
    using var cancellation = new CancellationTokenSource();
    var fixture = new CancellationFixture { BlockAt = "account", ExpectedToken = cancellation.Token };
    using var boundary = new CancellationBoundary(fixture);
    var context = boundary.Context(cancellation.Token);
    var validated = await boundary.TokenContext(context);
    var application = new ApplicationBuilder(boundary.Services);
    application.UseExceptionHandler();
    application.Run(_ => boundary.Options.Events.TokenValidated(validated));
    var pending = application.Build()(context);
    await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    cancellation.Cancel();
    await pending.WaitAsync(TimeSpan.FromSeconds(5));
    Check(context.Response.StatusCode == StatusCodes.Status499ClientClosedRequest,
        "aborted auth request uses framework client-closed status");
    Check(context.Response.Body.Length == 0, "aborted request has no problem body written to a disconnected client");
    IdentityDenied(context, "framework abort handling");
}

Console.WriteLine($"PASS: {checks} authentication cancellation checks. Synthetic reads and HTTP components only; no signed-token, SQL or browser acceptance.");

sealed class CancellationFixture
{
    public const string BrowserSecret = "synthetic-browser-context-01234567890123456789";
    public CancellationClock Clock { get; } = new();
    public UserAccount Account { get; }
    public UserSession Session { get; }
    public MobileOtpChallenge Proof { get; }
    public WebAuthenticationService Service { get; }
    public string? BlockAt { get; set; }
    public bool MissingProof { get; set; }
    public CancellationToken ExpectedToken { get; set; }
    public List<string> Reads { get; } = [];
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public CancellationFixture()
    {
        var created = Clock.GetUtcNow().AddDays(-1);
        var employee = new Employee(created, "synthetic-creator");
        employee.UpdateDraft(new EmployeeDraftDetails
        {
            FullLegalName = "Synthetic Cancellation Check", IdentityDocumentType = Enum.GetValues<IdentityDocumentType>()[0],
            IdentityDocumentNumber = "synthetic-id", PersonalPhone = "+15555550123",
            JoinedOn = DateOnly.FromDateTime(created.UtcDateTime)
        });
        employee.SelectLoginPhone("+15555550123", false);
        var submission = employee.SubmitForApproval(Guid.NewGuid(), created, "synthetic-no-file", "v1",
            new string('A', 64), "synthetic-key-reference", 1);
        var decision = employee.DecideSubmission(submission, OnboardingDecision.Approve, Guid.NewGuid(),
            Guid.NewGuid(), "Synthetic approval", created);
        Account = new UserAccount(employee, decision, "synthetic@example.invalid", created);
        var role = new Role("CANCELLATION_CHECK", "Cancellation check", Guid.NewGuid(), created);
        var assignment = new UserRoleAssignment(Account, role, Guid.NewGuid(), created, null);
        var scope = new UserAccessScope(assignment, AccessScopeKind.Self);
        Account.ConfigureInitialAccess(assignment, scope, created);
        var phone = new LoginPhoneBinding(Account, "+15555550123", submission.Id, created);
        Account.ProvisionInitialWebCredentials("synthetic@example.invalid", "synthetic-unused-hash",
            WebPasswordFormats.IdentityV3, phone, assignment, scope, created);
        phone.Verify(created.AddMinutes(1));
        Proof = MobileOtpChallenge.CreateWebLogin(Guid.NewGuid(), phone, Account.SecurityVersion,
            Digest("browser:" + BrowserSecret), "synthetic-unused-verifier", created.AddMinutes(2),
            created.AddMinutes(7), 3);
        Session = new UserSession(Account, SessionClient.Web, null, null, created.AddMinutes(3),
            Clock.GetUtcNow().AddHours(1), "synthetic-cancellation", Proof);
        var state = new AuthenticationAccount(Account, phone, [new(assignment, scope)], EmployeeStatus.Active, false);

        var repository = CancellationProxy.Create<IAuthenticationRepository>((method, args) => method.Name switch
        {
            nameof(IAuthenticationRepository.GetSessionAsync) => Read<UserSession?>("session", (Guid)args[0]!,
                Session.Id, Session, (CancellationToken)args[1]!),
            nameof(IAuthenticationRepository.GetAccountAsync) => Read<AuthenticationAccount?>("account", (Guid)args[0]!,
                Account.Id, state, (CancellationToken)args[2]!),
            nameof(IAuthenticationRepository.GetChallengeAsync) => Read<MobileOtpChallenge?>("proof", (Guid)args[0]!,
                Proof.Id, MissingProof ? null : Proof, (CancellationToken)args[1]!),
            _ => throw new InvalidOperationException("Unexpected repository operation: " + method.Name)
        });
        var protector = CancellationProxy.Create<IOtpProtector>((method, args) => method.Name switch
        {
            "get_IsConfigured" => true,
            nameof(IOtpProtector.Digest) => Digest((string)args[0]!),
            _ => throw new InvalidOperationException("Unexpected credential operation: " + method.Name)
        });
        // Explicit synthetic options only; no deployment/company defaults are installed.
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
        Service = new WebAuthenticationService(repository, CancellationProxy.Unused<IWebAuthenticationRepository>(),
            CancellationProxy.Unused<IWebPasswordVerifier>(), protector, CancellationProxy.Unused<IOtpSmsSender>(),
            CancellationProxy.Unused<IAccessTokenIssuer>(), web, common, Clock);
    }

    public Task<AuthenticatedAccount?> Validate(bool browser, CancellationToken token) => browser
        ? Service.ValidateBrowserSessionAsync(Account.Id, Session.Id, Session.SecurityVersion, BrowserSecret, token)
        : Service.ValidateSessionAsync(Account.Id, Session.Id, Session.SecurityVersion, token);

    private async Task<T> Read<T>(string stage, Guid requestedId, Guid expectedId, T result, CancellationToken token)
    {
        if (requestedId != expectedId || token != ExpectedToken)
            throw new InvalidOperationException("Incorrect lookup identity or cancellation token at " + stage);
        Reads.Add(stage);
        token.ThrowIfCancellationRequested();
        if (BlockAt == stage)
        {
            Entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }
        return result;
    }

    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

sealed class CancellationBoundary : IDisposable
{
    private readonly CancellationFixture fixture;
    private readonly AuthenticationScheme scheme = new(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
    public ServiceProvider Services { get; }
    public JwtBearerOptions Options { get; }
    public CancellationBoundary(CancellationFixture fixture)
    {
        this.fixture = fixture;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetrics();
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddSingleton(new DiagnosticListener("Cosmix.AuthenticationCancellation.Check"));
        services.AddSingleton(fixture.Service);
        services.AddApiAuthentication(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = new string('T', 64)
        }).Build(), new CancellationEnvironment());
        Services = services.BuildServiceProvider();
        Options = Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
    }

    public DefaultHttpContext Context(CancellationToken token, string? browser = null)
    {
        var context = new DefaultHttpContext { RequestServices = Services, RequestAborted = token };
        context.Request.Scheme = "https";
        context.Request.Headers.Cookie = "__Host-Cosmix.Access=synthetic-callback-token; __Host-Cosmix.Browser=" +
            (browser ?? CancellationFixture.BrowserSecret);
        context.Response.Body = new MemoryStream();
        return context;
    }

    public async Task<TokenValidatedContext> TokenContext(DefaultHttpContext context)
    {
        await Options.Events.MessageReceived(new MessageReceivedContext(context, scheme, Options));
        return new TokenValidatedContext(context, scheme, Options)
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", fixture.Account.Id.ToString()), new Claim("sid", fixture.Session.Id.ToString()),
                new Claim("sv", fixture.Session.SecurityVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim("employee_id", fixture.Account.EmployeeId.ToString()), new Claim("client", "web")
            ], "component-callback-only"))
        };
    }
    public void Dispose() => Services.Dispose();
}

public class CancellationProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;
    public static T Create<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, CancellationProxy>();
        ((CancellationProxy)(object)proxy).Handler = handler;
        return proxy;
    }
    public static T Unused<T>() where T : class => Create<T>((method, _) =>
        throw new InvalidOperationException("Unexpected collaborator: " + typeof(T).Name + "." + method.Name));
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args ?? []);
}

sealed class CancellationClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
}

sealed class CancellationEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "Cosmix.AuthenticationCancellation.Check";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
