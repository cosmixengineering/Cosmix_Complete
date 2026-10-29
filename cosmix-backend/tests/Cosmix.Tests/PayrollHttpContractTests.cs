using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Cosmix.Api.Authentication;
using Cosmix.Api.Attendance;
using Cosmix.Api.Controllers.Attendance;
using Cosmix.Api.Controllers.HR;
using Cosmix.Api.Middleware;
using Cosmix.Application;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.PayrollImports;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Application.HR.Payslips;
using Cosmix.Application.HR.StatutoryContributions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Infrastructure;
using Cosmix.Infrastructure.Database;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollHttpContractTests
{
    private static readonly Guid SyntheticActor = Guid.Parse("118f8080-1ee1-4ea6-aee8-3e025a07514d");
    [Theory]
    [InlineData("ledger")]
    [InlineData("trial-balance")]
    [InlineData("bank-reconciliation")]
    public async Task Finance_ledger_requires_login_and_current_financial_authority(string endpoint)
    {
        await using var server = await Host.CreateAsync();
        var path = $"/api/v1/finance/{endpoint}?from=2026-10-01&untilExclusive=2026-11-01";
        using var anonymous = await server.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        server.Authenticate();
        using var denied = await server.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
    [Fact]
    public async Task Payroll_return_route_requires_login_and_current_hr_authority()
    {
        await using var server = await Host.CreateAsync();
        var path = $"/api/v1/payroll-handoff/batches/{Guid.NewGuid()}/return-for-correction";
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new { expectedVersion = "AAAAAAAAAAA=", reason = "Correct source before lock" });
        using var anonymous = await server.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        server.Authenticate();
        using var second = new HttpRequestMessage(HttpMethod.Post, path);
        second.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        second.Content = JsonContent.Create(new { expectedVersion = "AAAAAAAAAAA=", reason = "Correct source before lock" });
        using var denied = await server.Client.SendAsync(second);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
    [Fact]
    public async Task Salary_route_requires_authentication_then_current_salary_authority()
    {
        await using var server = await Host.CreateAsync();
        using var anonymous = await server.Client.GetAsync("/api/v1/hr/payroll/salary/policies");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        server.Authenticate();
        using var denied = await server.Client.GetAsync("/api/v1/hr/payroll/salary/policies");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("access_denied", await TitleAsync(denied));
    }

    [Theory]
    [InlineData("mobile", "true")]
    [InlineData("web", "false")]
    public async Task Sensitive_salary_route_rejects_mobile_or_missing_mfa_even_with_a_grant(string client, string mfa)
    {
        await using var server = await Host.CreateAsync(grant: true);
        server.Authenticate(client, mfa);
        using var response = await server.Client.GetAsync("/api/v1/hr/payroll/salary/policies");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("1", HttpStatusCode.Forbidden)]
    [InlineData("3", HttpStatusCode.Forbidden)]
    [InlineData("2", HttpStatusCode.BadRequest)]
    [InlineData("99", HttpStatusCode.BadRequest)]
    [InlineData("\"Approved\"", HttpStatusCode.BadRequest)]
    public async Task Hr_review_route_binds_numeric_approved_and_rejected_but_rejects_other_decisions(string decision, HttpStatusCode expected)
    {
        await using var server = await Host.CreateAsync(); server.Authenticate();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/payroll-handoff/batches/{Guid.NewGuid()}/hr-review")
        {
            Content = new StringContent("{\"expectedVersion\":\"AAAAAAAAAAA=\",\"decision\":" + decision + ",\"reason\":\"Synthetic HTTP review\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Idempotency-Key", "synthetic-http-review");
        using var response = await server.Client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Review_requires_idempotency_header_and_disallows_unknown_body_fields()
    {
        await using var server = await Host.CreateAsync(); server.Authenticate();
        var route = $"/api/v1/payroll-handoff/batches/{Guid.NewGuid()}/hr-review";
        using var missingHeader = await server.Client.PostAsJsonAsync(route, new { expectedVersion = "AAAAAAAAAAA=", decision = 1, reason = "Synthetic HTTP review" });
        Assert.Equal(HttpStatusCode.BadRequest, missingHeader.StatusCode);
        server.Client.DefaultRequestHeaders.Add("Idempotency-Key", "synthetic-unknown-field");
        using var unknownField = await server.Client.PostAsJsonAsync(route, new { expectedVersion = "AAAAAAAAAAA=", decision = 1, reason = "Synthetic HTTP review", skipApproval = true });
        Assert.Equal(HttpStatusCode.BadRequest, unknownField.StatusCode);
    }

    [Fact]
    public async Task Finance_route_policy_denies_an_authenticated_caller_without_its_grant()
    {
        await using var server = await Host.CreateAsync(); server.Authenticate();
        using var response = await server.AccountAsync(1);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(99, HttpStatusCode.BadRequest, "payroll_finance_invalid_request")]
    [InlineData(1, HttpStatusCode.Conflict, "payroll_finance_conflict")]
    public async Task Actual_finance_controller_maps_only_typed_validation_and_conflict(int kind, HttpStatusCode expected, string title)
    {
        await using var server = await Host.CreateAsync(grant: true); server.Authenticate();
        using var response = kind == 99 ? await server.AccountAsync(kind) : await server.ApproveOwnPeriodAsync();
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(title, await TitleAsync(response));
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unexpected_finance_failures_remain_500_and_do_not_expose_internal_details(bool argumentFailure)
    {
        await using var server = await Host.CreateAsync(grant: true, failure: argumentFailure ? "argument" : "operation"); server.Authenticate();
        using var response = await server.AccountAsync(1);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("An unexpected error occurred", await TitleAsync(response));
        Assert.DoesNotContain("SYNTHETIC_PRIVATE_FAILURE", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void Production_registrations_resolve_payroll_services_without_connecting_to_a_database()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:CosmixDatabase"] = "Server=127.0.0.1,1;Database=__SyntheticDiOnly;User ID=synthetic;Password=synthetic-not-a-credential;Encrypt=false;Connect Timeout=1",
            ["MobileAuthentication:Enabled"] = "false", ["WebAuthentication:Enabled"] = "false",
            ["Jwt:Issuer"] = "Synthetic.Payroll.Tests", ["Jwt:Audience"] = "Synthetic.Payroll.Tests",
            ["Jwt:SigningKey"] = "Synthetic-only-DI-validation-key-not-used-for-tokens"
        }).Build();
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication(); services.AddInfrastructure(configuration);
        services.AddApiAuthentication(configuration, new SyntheticEnvironment());
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton<IAttendanceCursorProtector, AttendanceCursorProtector>();
        services.AddSingleton<Cosmix.Application.Attendance.FaceVerification.IFaceCaptureProtector, FaceCaptureProtector>();
        services.AddWorkTreatmentSourceProtection();
        services.RemoveAll<IPrivateFileStore>(); services.AddSingleton<IPrivateFileStore, NoPrivateIo>();
        services.AddSingleton(Proxy<ICurrentAuthentication>((_, _) => throw new InvalidOperationException("DI resolution must not authenticate or perform I/O.")));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope(); var resolved = scope.ServiceProvider;
        Assert.IsType<PayrollSalaryService>(resolved.GetRequiredService<PayrollSalaryService>());
        Assert.IsType<PayrollImportService>(resolved.GetRequiredService<PayrollImportService>());
        Assert.IsType<PayslipService>(resolved.GetRequiredService<PayslipService>());
        Assert.IsType<PayrollFrozenBatchReader>(resolved.GetRequiredService<IPayrollFrozenBatchReader>());
        Assert.IsType<PayrollAccountingService>(resolved.GetRequiredService<PayrollAccountingService>());
        Assert.IsType<PayrollFinanceSetupService>(resolved.GetRequiredService<PayrollFinanceSetupService>());
        Assert.IsType<StatutoryContributionService>(resolved.GetRequiredService<StatutoryContributionService>());
        Assert.NotNull(resolved.GetRequiredService<IPayrollStatutorySourceReader>());
        Assert.IsType<PayrollHandoffService>(resolved.GetRequiredService<PayrollHandoffService>());
        Assert.Equal(ConnectionState.Closed, resolved.GetRequiredService<CosmixDbContext>().Database.GetDbConnection().State);
    }

    private static async Task<string?> TitleAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("title").GetString();
    }

    private sealed class Host(WebApplication application, HttpClient client) : IAsyncDisposable
    {
        internal HttpClient Client { get; } = client;
        internal static async Task<Host> CreateAsync(bool grant = false, string? failure = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
            builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
            builder.Services.AddControllers().AddApplicationPart(typeof(PayrollSalaryController).Assembly);
            builder.Services.AddProblemDetails(); builder.Services.AddExceptionHandler<ApiExceptionHandler>();
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "Synthetic.Payroll.Tests", ["Jwt:Audience"] = "Synthetic.Payroll.Tests",
                ["Jwt:SigningKey"] = "Synthetic-only-HTTP-validation-key-not-used-for-tokens"
            });
            builder.Services.AddApiAuthentication(builder.Configuration, builder.Environment);
            builder.Services.AddAuthentication("synthetic").AddScheme<AuthenticationSchemeOptions, SyntheticAuthentication>("synthetic", _ => { });
            builder.Services.AddSingleton<TimeProvider>(new Clock());
            IReadOnlyList<PermissionGrant> grants = grant ? new[] { PayrollSalaryPermissions.Read, PayrollFinanceSetupService.Configure, PayrollFinanceSetupService.Approve }
                .Select(p => new PermissionGrant(p, true, Guid.NewGuid(), AccessScopeKind.All, null)
                { AuthorityTimingKnown = true, ValidFromUtc = Clock.Now.AddHours(-1), ValidUntilUtc = Clock.Now.AddHours(1) }).ToArray() : [];
            builder.Services.AddSingleton(Proxy<IAccountAccessReader>((_, _) => Task.FromResult(grants)));
            builder.Services.AddSingleton(Proxy<IAccessManagementRepository>((method, args) => method.Name switch
            {
                "InTransactionAsync" => typeof(Host).GetMethod(nameof(Transaction), BindingFlags.Static | BindingFlags.NonPublic)!
                    .MakeGenericMethod(method.GetGenericArguments()).Invoke(null, args),
                "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticFinance"),
                "IsCurrentAdministratorAsync" => Task.FromResult(true),
                _ => throw new InvalidOperationException(method.Name)
            }));
            builder.Services.AddSingleton(Proxy<ISchedulingJournalRepository>((method, _) => method.Name switch
            {
                "IsCurrentSessionAsync" => Task.FromResult(true),
                "FindReceiptAsync" => Task.FromResult<IdempotencyReceipt?>(null),
                _ => throw new InvalidOperationException("An error-only HTTP fixture must not persist business or audit data.")
            }));
            builder.Services.AddSingleton<IPrivateFileStore, NoPrivateIo>();
            builder.Services.AddScoped(provider => new AttendanceOperation(provider.GetRequiredService<ICurrentAuthentication>(),
                provider.GetRequiredService<IAccountAccessReader>(), provider.GetRequiredService<IAccessManagementRepository>(),
                provider.GetRequiredService<ISchedulingJournalRepository>(), null!, provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<IPrivateFileStore>()));
            builder.Services.AddScoped(provider => new PayrollSalaryService(provider.GetRequiredService<AttendanceOperation>(), null!, provider.GetRequiredService<IPrivateFileStore>()));
            builder.Services.AddScoped(provider => new PayrollHandoffService(provider.GetRequiredService<AttendanceOperation>(), null!, null!, null!, null!, null!,
                provider.GetRequiredService<IAccountAccessReader>(), provider.GetRequiredService<IPrivateFileStore>()));
            builder.Services.AddScoped<SensitiveComplianceOperation>(); builder.Services.AddScoped<PayrollFinanceOperation>();
            builder.Services.AddScoped(provider => new Cosmix.Application.Finance.Reconciliation.BankReconciliationService(
                provider.GetRequiredService<SensitiveComplianceOperation>(), provider.GetRequiredService<PayrollFinanceOperation>(),
                null!, null!, provider.GetRequiredService<IPrivateFileStore>(), TimeProvider.System));
            builder.Services.AddScoped(provider => new Cosmix.Application.Finance.Ledger.FinanceLedgerService(
                provider.GetRequiredService<SensitiveComplianceOperation>(),
                Proxy<Cosmix.Application.Finance.Ledger.IFinanceLedgerRepository>((_, _) =>
                    throw new InvalidOperationException("Denied ledger requests must not read records."))));
            builder.Services.AddSingleton(Proxy<IPayrollFinanceRepository>((method, args) =>
            {
                if (method.Name == "PeriodAsync")
                {
                    var period = new PayrollAccountingPeriod(new(2026, 10, 1), new(2026, 11, 1), "PKR", SyntheticActor.ToString(), Clock.Now.AddDays(-1));
                    typeof(PayrollAccountingPeriod).GetProperty(nameof(PayrollAccountingPeriod.Id))!.SetValue(period, (Guid)args[0]!);
                    typeof(PayrollAccountingPeriod).GetProperty(nameof(PayrollAccountingPeriod.RowVersion))!.SetValue(period, new byte[8]);
                    return Task.FromResult<PayrollAccountingPeriod?>(period);
                }
                if (method.Name == "ExpectVersion") return null;
                if (method.Name != "AccountAsync") throw new InvalidOperationException("An error-only HTTP fixture cannot mutate finance.");
                if (failure == "argument") throw new ArgumentException("SYNTHETIC_PRIVATE_FAILURE");
                if (failure == "operation") throw new InvalidOperationException("SYNTHETIC_PRIVATE_FAILURE");
                return Task.FromResult<PayrollLedgerAccount?>(new("SYNTHETIC", "Existing synthetic expense", PayrollAccountKind.Expense));
            }));
            builder.Services.AddScoped<PayrollFinanceSetupService>();
            builder.Services.AddScoped(provider => new PayrollAccountingService(null!, provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<SensitiveComplianceOperation>(), provider.GetRequiredService<PayrollFinanceSetupService>()));
            var app = builder.Build(); app.UseExceptionHandler(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            await app.StartAsync(); return new(app, app.GetTestClient());
        }

        internal void Authenticate(string client = "web", string mfa = "true")
        {
            Client.DefaultRequestHeaders.Add("X-Synthetic-Identity", "present");
            Client.DefaultRequestHeaders.Add("X-Synthetic-Client", client); Client.DefaultRequestHeaders.Add("X-Synthetic-Mfa", mfa);
        }
        internal Task<HttpResponseMessage> AccountAsync(int kind)
        {
            Client.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", "synthetic-account-contract");
            return Client.PostAsJsonAsync("/api/v1/payroll-accounting/accounts", new { code = "SYNTHETIC", name = "Synthetic expense", kind, reason = "Synthetic HTTP validation" });
        }
        internal Task<HttpResponseMessage> ApproveOwnPeriodAsync()
        {
            Client.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", "synthetic-period-contract");
            return Client.PostAsJsonAsync($"/api/v1/payroll-accounting/periods/{Guid.NewGuid()}/approve",
                new { expectedVersion = "AAAAAAAAAAA=", reason = "Synthetic independent approval guard" });
        }
        private static Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
        public async ValueTask DisposeAsync() { Client.Dispose(); await application.DisposeAsync(); }
    }

    public sealed class SyntheticAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("X-Synthetic-Identity")) return Task.FromResult(AuthenticateResult.NoResult());
            var actor = new AuthenticatedAccount(SyntheticActor, Guid.NewGuid(), Guid.NewGuid(), 1,
                Request.Headers["X-Synthetic-Client"] == "mobile" ? SessionClient.Mobile : SessionClient.Web,
                Request.Headers["X-Synthetic-Mfa"] == "true");
            Context.Items[CurrentAuthentication.ItemKey] = actor;
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.AccountId.ToString())], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
    private sealed class Clock : TimeProvider
    {
        internal static readonly DateTimeOffset Now = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class SyntheticEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = typeof(PayrollHttpContractTests).Assembly.GetName().Name!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class NoPrivateIo : IPrivateFileStore
    {
        public NoPrivateIo() { }
        public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct) => throw new InvalidOperationException("Unexpected private-file I/O.");
        public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct) => throw new InvalidOperationException("Unexpected private-file I/O.");
    }
    private static T Proxy<T>(Func<MethodInfo, object?[], object?> callback) where T : class
    {
        var value = DispatchProxy.Create<T, CallbackProxy>(); ((CallbackProxy)(object)value).Callback = callback; return value;
    }
    public class CallbackProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Callback = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Callback(method!, args ?? []);
    }
}
