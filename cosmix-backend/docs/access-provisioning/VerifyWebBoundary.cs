#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyWebBoundary.packages.lock.json
using System.Security.Claims;
using Cosmix.Api.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

// Real middleware, antiforgery, authentication option/event callbacks and current-identity adapter.
// Paths mirror the real web-login and shift routes. These direct middleware checks do not run MVC routing;
// DELETE/HEAD/OPTIONS/TRACE cases verify middleware method handling, not supported shift operations.
// All contexts and protection keys are in memory. No host, authentication bypass, SQL connection,
// account fixture, private-file write or successful authenticated SQL request exists in this check.
var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    checks++;
}
var registrations = new ServiceCollection();
registrations.AddLogging();
registrations.AddOptions();
registrations.AddProblemDetails();
registrations.AddDataProtection().UseEphemeralDataProtectionProvider();
registrations.AddAntiforgery(options =>
{
    // These are the composition-root settings; no production registration is altered.
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-Cosmix.Csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
});
registrations.AddApiAuthentication(new ConfigurationBuilder().AddInMemoryCollection(
    new Dictionary<string, string?> { ["Jwt:SigningKey"] = new string('T', 64) }).Build(), new SyntheticEnvironment());
using var services = registrations.BuildServiceProvider();
var antiforgery = services.GetRequiredService<IAntiforgery>();
DefaultHttpContext Context(string method = "POST", string path = "/api/v1/hr/shifts", string scheme = "https")
{
    var context = new DefaultHttpContext { RequestServices = services };
    context.Request.Method = method;
    context.Request.Path = path;
    context.Request.Scheme = scheme;
    context.Request.Host = new HostString("localhost");
    context.Response.Body = new MemoryStream();
    return context;
}
var issued = Context("GET", "/api/v1/auth/web/csrf");
var tokens = antiforgery.GetAndStoreTokens(issued);
Check(!string.IsNullOrEmpty(tokens.CookieToken) && !string.IsNullOrEmpty(tokens.RequestToken), "real antiforgery token pair issued");
var setCookie = issued.Response.Headers.SetCookie.ToString();
Check(setCookie.Contains("__Host-Cosmix.Csrf=") && setCookie.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
    setCookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) && setCookie.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase) &&
    setCookie.Contains("path=/", StringComparison.OrdinalIgnoreCase), "antiforgery cookie has required browser protections");

async Task MiddlewareCase(string name, string method, string path, string? cookie, string? requestToken, bool allowed,
    bool bearer = false, ClaimsPrincipal? principal = null)
{
    var context = Context(method, path);
    if (cookie is not null) context.Request.Headers.Cookie = cookie;
    if (requestToken is not null) context.Request.Headers["X-CSRF-TOKEN"] = requestToken;
    if (bearer) context.Request.Headers.Authorization = "Bearer deliberately-not-a-token";
    if (principal is not null) context.User = principal;
    var reachedNext = false;
    await new WebCsrfMiddleware(_ => { reachedNext = true; return Task.CompletedTask; }).InvokeAsync(context, antiforgery);
    Check(reachedNext == allowed, name);
    if (!allowed)
    {
        Check(context.Response.StatusCode == 400, name + " status");
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        Check((await reader.ReadToEndAsync()).Contains("invalid_csrf_token", StringComparison.Ordinal), name + " problem code");
    }
}
var validCookie = "__Host-Cosmix.Csrf=" + tokens.CookieToken;
await MiddlewareCase("web login requires CSRF without session", "POST", "/api/v1/auth/web/login", null, null, false);
await MiddlewareCase("web refresh requires CSRF", "POST", "/api/v1/auth/web/refresh", "__Host-Cosmix.Refresh=synthetic", null, false);
await MiddlewareCase("access-cookie write requires CSRF", "POST", "/api/v1/hr/shifts", "__Host-Cosmix.Access=synthetic", null, false);
await MiddlewareCase("refresh-cookie-only write requires CSRF", "PUT", "/api/v1/hr/shifts/11111111-1111-4111-8111-111111111111", "__Host-Cosmix.Refresh=synthetic", null, false);
await MiddlewareCase("DELETE cookie write requires CSRF", "DELETE", "/api/v1/hr/shifts/11111111-1111-4111-8111-111111111111", "__Host-Cosmix.Access=synthetic", null, false);
await MiddlewareCase("bearer header cannot skip cookie CSRF", "POST", "/api/v1/hr/shifts", "__Host-Cosmix.Access=synthetic", null, false, true);
await MiddlewareCase("request token alone is insufficient", "POST", "/api/v1/auth/web/login", null, tokens.RequestToken, false);
await MiddlewareCase("cookie token alone is insufficient", "POST", "/api/v1/auth/web/login", validCookie, null, false);
await MiddlewareCase("corrupt request token fails", "POST", "/api/v1/auth/web/login", validCookie, "broken", false);
await MiddlewareCase("corrupt cookie token fails", "POST", "/api/v1/auth/web/login", "__Host-Cosmix.Csrf=broken", tokens.RequestToken, false);
await MiddlewareCase("cookie and request token are not interchangeable", "POST", "/api/v1/auth/web/login", validCookie, tokens.CookieToken, false);
await MiddlewareCase("same-context valid token pair proceeds", "POST", "/api/v1/auth/web/login", validCookie, tokens.RequestToken, true);
await MiddlewareCase("valid cookie write proceeds", "POST", "/api/v1/hr/shifts", validCookie + "; __Host-Cosmix.Access=synthetic", tokens.RequestToken, true);
var otherTokens = antiforgery.GetAndStoreTokens(Context("GET", "/api/v1/auth/web/csrf"));
await MiddlewareCase("another browser token fails", "POST", "/api/v1/auth/web/login", validCookie, otherTokens.RequestToken, false);
var namedUser = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "component-only"));
await MiddlewareCase("anonymous token rejected after identity changes", "POST", "/api/v1/auth/web/login", validCookie, tokens.RequestToken, false, principal: namedUser);
await MiddlewareCase("mobile bearer transport does not require CSRF", "POST", "/api/v1/auth/mobile/otp/request", null, null, true, true);
await MiddlewareCase("bearer-only feature request reaches later authentication", "POST", "/api/v1/hr/shifts", null, null, true, true);
foreach (var method in new[] { "GET", "HEAD", "OPTIONS", "TRACE" })
    await MiddlewareCase(method + " does not cause a CSRF rejection", method, "/api/v1/hr/shifts", "__Host-Cosmix.Access=synthetic", null, true);

var options = services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
Check(!options.MapInboundClaims && !options.IncludeErrorDetails, "JWT retains explicit claim mapping and hides validation details");
var validation = options.TokenValidationParameters;
Check(validation.ValidateIssuer && validation.ValidateAudience && validation.ValidateLifetime && validation.RequireExpirationTime &&
    validation.RequireSignedTokens && validation.ValidateIssuerSigningKey, "JWT verifies issuer audience expiry signature and key");
Check(validation.ValidAlgorithms.SequenceEqual([SecurityAlgorithms.HmacSha256]) && validation.ClockSkew == TimeSpan.FromSeconds(30),
    "JWT algorithm and clock skew constrained");
var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
async Task<MessageReceivedContext> Received(string transport, bool cookie, bool header)
{
    var context = Context(scheme: transport);
    if (cookie) context.Request.Headers.Cookie = "__Host-Cosmix.Access=synthetic-cookie-token";
    if (header) context.Request.Headers.Authorization = "Bearer synthetic-header-token";
    var received = new MessageReceivedContext(context, scheme, options);
    await options.Events.MessageReceived(received);
    return received;
}
var httpCookie = await Received("http", true, false);
Check(httpCookie.Result?.Failure is not null && !httpCookie.HttpContext.Items.ContainsKey("cosmix:web-cookie"), "HTTP web cookie rejected before session lookup");
var httpsCookie = await Received("https", true, false);
Check(httpsCookie.Token == "synthetic-cookie-token" && httpsCookie.HttpContext.Items.ContainsKey("cosmix:web-cookie"), "HTTPS cookie transport explicitly marked");
var headerWins = await Received("https", true, true);
Check(headerWins.Token is null && !headerWins.HttpContext.Items.ContainsKey("cosmix:web-cookie"), "Authorization header prevents cookie reinterpretation");
var noCookie = await Received("https", false, false);
Check(noCookie.Token is null && !noCookie.HttpContext.Items.ContainsKey("cosmix:web-cookie"), "missing cookie cannot create browser transport");

async Task TokenRejected(string name, string? sub, string? sid, string? version, string? client, bool cookie)
{
    var context = Context();
    if (cookie) context.Items["cosmix:web-cookie"] = true;
    var claims = new List<Claim>();
    if (sub is not null) claims.Add(new("sub", sub));
    if (sid is not null) claims.Add(new("sid", sid));
    if (version is not null) claims.Add(new("sv", version));
    if (client is not null) claims.Add(new("client", client));
    // Deliberately no session services are registered: any accidental lookup fails this harness.
    var validated = new TokenValidatedContext(context, scheme, options) { Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "component-only")) };
    await options.Events.TokenValidated(validated);
    Check(validated.Result?.Failure is not null && !context.Items.ContainsKey(CurrentAuthentication.ItemKey), name);
}
var account = Guid.NewGuid().ToString();
var session = Guid.NewGuid().ToString();
await TokenRejected("missing subject rejected", null, session, "1", "mobile", false);
await TokenRejected("invalid subject rejected", "wrong", session, "1", "mobile", false);
await TokenRejected("missing session rejected", account, null, "1", "mobile", false);
await TokenRejected("invalid session rejected", account, "wrong", "1", "mobile", false);
await TokenRejected("missing security version rejected", account, session, null, "mobile", false);
await TokenRejected("invalid security version rejected", account, session, "wrong", "mobile", false);
await TokenRejected("web claim in bearer channel rejected", account, session, "1", "web", false);
await TokenRejected("mobile claim in cookie channel rejected", account, session, "1", "mobile", true);
await TokenRejected("missing client in cookie channel rejected", account, session, "1", null, true);
await TokenRejected("unknown client in cookie channel rejected", account, session, "1", "admin", true);

var accessor = new HttpContextAccessor();
var current = new CurrentAuthentication(accessor);
void IdentityDenied(string name)
{
    try { current.Require(); throw new Exception(name + ": expected invalid_authentication"); }
    catch (AuthenticationFailure error) { Check(error.StatusCode == 401 && error.Code == "invalid_authentication", name); }
}
IdentityDenied("missing context denied");
accessor.HttpContext = Context();
accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
    new Claim("sub", account), new Claim("employee_id", Guid.NewGuid().ToString()), new Claim("mfa", "true")], "forged-principal"));
IdentityDenied("principal claims alone never establish validated employee");
accessor.HttpContext.Items[CurrentAuthentication.ItemKey] = new { AccountId = account };
IdentityDenied("untyped item never establishes validated employee");
var serverIdentity = new AuthenticatedAccount(Guid.Parse(account), Guid.NewGuid(), Guid.Parse(session), 3, SessionClient.Mobile, false);
accessor.HttpContext.Items[CurrentAuthentication.ItemKey] = serverIdentity;
Check(ReferenceEquals(current.Require(), serverIdentity), "identity comes from server-validated account item, not claims");
Console.WriteLine($"PASS: {checks} real web-boundary component checks. No SQL, persisted keys, account fixtures or successful authenticated HTTP acceptance.");

sealed class SyntheticEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "Cosmix.WebBoundary.ComponentChecks";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
