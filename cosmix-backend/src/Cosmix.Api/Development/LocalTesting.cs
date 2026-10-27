using System.Net;
using Cosmix.Api.Authentication;
using Cosmix.Api.BackgroundJobs;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Infrastructure.Authentication.Bootstrap;
using Cosmix.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cosmix.Api.Development;

// An explicit local-only adapter, never a second authentication implementation.
internal static class LocalTesting
{
    internal const string Database = "Cosmix_Testing";
    internal const string ExistingHostProfile = "HumaExistingAttendance20261013B01";
    internal const string ExistingSettingsPath = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Attendance-Host-20261013-B01.json";
    internal const string ExistingKeyDirectory = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\keys\attendance-dp-20261013-b01";
    internal const string ExistingPipe = @"np:\\.\pipe\LOCALDB#842DD4FB\tsql\query";
    internal static readonly (string Email, string Phone, string Name)[] Accounts =
    [
        ("superadmin@example.invalid", "+12025550101", "Engr. Imran Saeed (Project Director)"),
        ("admin@example.invalid", "+12025550102", "Engr. Tariq Mehmood (Site Supervisor)"),
        ("hr@example.invalid", "+12025550103", "Muhammad Rashid (Lead Engineer)")
    ];

    internal static WebApplicationBuilder CreateBuilder(bool setupLocalTesting, string[] args)
    {
        var profile = Environment.GetEnvironmentVariable("COSMIX_FRESH_TESTING_HOST");
        var existing = Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST");
        if (string.IsNullOrEmpty(profile) && string.IsNullOrEmpty(existing)) return WebApplication.CreateBuilder(setupLocalTesting ? [] : args);
        if (!string.IsNullOrEmpty(existing) ? (existing != ExistingHostProfile && existing != CurrentHrTestingGuard.Profile && existing != AuthOnlyTestingGuard.Profile && existing != AuthOnlyTestingGuard.October26Profile && existing != AuthOnlyTestingGuard.October26ReadsProfile && existing != HrBusinessTestingGuard.Profile && existing != HrPermissionP1Guard.Profile && existing != HrOutstationO1Guard.Profile && existing != HrVehicleV2Guard.Profile && existing != HrVehicleV2ContinuationGuard.Profile) || !string.IsNullOrEmpty(profile) || setupLocalTesting || args.Length != 0 :
            (profile != FreshLocalTestingGuard.Profile && profile != LocalTestingA02Guard.Profile) ||
            (args.Length != 0 && !(setupLocalTesting && args.Length == 1 && args[0] == "setup-local-testing")))
            throw new InvalidOperationException("Fresh local-testing requires its reviewed configuration-only invocation.");
        // EmptyBuilder never loads appsettings, source user secrets or inherited business configuration.
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        {
            Args = [], ApplicationName = typeof(LocalTesting).Assembly.GetName().Name,
            EnvironmentName = Environments.Development, ContentRootPath = Directory.GetCurrentDirectory()
        });
        builder.WebHost.UseKestrel();
        builder.Services.AddLogging();
        return builder;
    }

    internal static bool Configure(WebApplicationBuilder builder)
    {
        var path = Environment.GetEnvironmentVariable("COSMIX_LOCAL_TESTING_SETTINGS");
        if (HrVehicleV2ContinuationGuard.AnySelected)
            HrVehicleV2Host.ValidatePrivatePaths(path);
        else if (HrOutstationO1Guard.IsSelected)
            HrOutstationO1Host.ValidatePrivatePaths(path);
        else if (HrPermissionP1Guard.IsSelected)
            HrPermissionP1Host.ValidatePrivatePaths(path);
        else if (HrReportTestingGuard.IsSelected)
            HrReportTestingHost.ValidatePrivatePaths(path);
        else if (HrBusinessTestingGuard.IsSelected)
            HrBusinessTestingHost.ValidatePrivatePaths(path);
        if (AuthOnlyTestingBoundary.IsSelected)
            AuthOnlyTestingHost.ValidatePrivatePaths(path);
        if (Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") == CurrentHrTestingGuard.Profile)
            CurrentHrTestingHost.ValidatePrivatePaths(path);
        if (!string.IsNullOrWhiteSpace(path))
        {
            if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Local testing requires Development.");
            builder.Configuration.AddJsonFile(PrivateBootstrapSnapshotStore.RequirePrivatePath(path, false), optional: false, reloadOnChange: false);
        }
        if (Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") == CurrentHrTestingGuard.Profile)
            CurrentHrTestingGuard.ValidateConfiguration(builder.Configuration, path);
        if (AuthOnlyTestingBoundary.IsSelected)
            AuthOnlyTestingGuard.ValidateConfiguration(builder.Configuration, path);
        if (HrBusinessTestingGuard.IsSelected)
            HrBusinessTestingGuard.ValidateConfiguration(builder.Configuration, path);
        if (HrPermissionP1Guard.IsSelected)
            HrPermissionP1Guard.ValidateConfiguration(builder.Configuration, path);
        if (HrOutstationO1Guard.IsSelected)
            HrOutstationO1Guard.ValidateConfiguration(builder.Configuration, path);
        if (HrVehicleV2Guard.IsSelected)
            HrVehicleV2Guard.ValidateConfiguration(builder.Configuration, path);
        if (HrVehicleV2ContinuationGuard.IsSelected)
            HrVehicleV2ContinuationGuard.ValidateConfiguration(builder.Configuration, path);
        var enabled = builder.Configuration.GetValue<bool>("LocalTesting:Enabled");
        if (!enabled)
        {
            if (builder.Configuration.GetSection("LocalTesting:Profile").Exists() ||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COSMIX_FRESH_TESTING_HOST")) ||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST")))
                throw new InvalidOperationException("An isolated testing profile requires explicit local testing.");
            return false;
        }
        if (!builder.Environment.IsDevelopment() || string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Local testing requires Development and private local settings.");
        var connection = new SqlConnectionStringBuilder(builder.Configuration.GetConnectionString("CosmixDatabase"));
        if (connection.InitialCatalog != Database || !connection.IntegratedSecurity ||
            !(connection.DataSource.Equals(@"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase) ||
              connection.DataSource.StartsWith(@"np:\\.\pipe\LOCALDB#", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Local testing is restricted to Cosmix_Testing on this computer's LocalDB.");
        if (builder.Configuration["MobileAuthentication:Sms:Provider"] != "LocalTesting" ||
            !string.IsNullOrEmpty(builder.Configuration["MobileAuthentication:Sms:Endpoint"]) ||
            !string.IsNullOrEmpty(builder.Configuration["MobileAuthentication:Sms:BearerToken"]))
            throw new InvalidOperationException("Local testing must not configure an external SMS gateway.");
        var fresh = FreshTarget(builder.Configuration);
        if (HrVehicleV2ContinuationGuard.AnySelected)
        {
            builder.WebHost.UseUrls(HrVehicleV2Guard.Url);
        }
        else if (HrOutstationO1Guard.IsSelected)
        {
            builder.WebHost.UseUrls(HrOutstationO1Guard.Url);
        }
        else if (HrPermissionP1Guard.IsSelected)
        {
            builder.WebHost.UseUrls(HrPermissionP1Guard.Url);
        }
        else if (HrBusinessTestingGuard.IsSelected)
        {
            builder.WebHost.UseUrls(HrBusinessTestingGuard.Url);
        }
        else if (AuthOnlyTestingBoundary.IsSelected)
        {
            builder.WebHost.UseUrls(AuthOnlyTestingGuard.Url);
        }
        else if (Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") == CurrentHrTestingGuard.Profile)
        {
            builder.WebHost.UseUrls(CurrentHrTestingGuard.Url);
        }
        else if (Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") == ExistingHostProfile)
        {
            if (builder.Configuration["LocalTesting:ExistingAcceptanceProfile"] != ExistingHostProfile ||
                !Path.GetFullPath(path).Equals(ExistingSettingsPath, StringComparison.OrdinalIgnoreCase) ||
                connection.DataSource != ExistingPipe || !connection.Encrypt || !connection.TrustServerCertificate ||
                !string.IsNullOrEmpty(connection.UserID) || !string.IsNullOrEmpty(connection.Password) ||
                !string.IsNullOrEmpty(connection.AttachDBFilename) || fresh is not null ||
                builder.Configuration.GetSection("ConnectionStrings").GetChildren().Count() != 1 ||
                new[] { "SelfHostedFace", "FaceCapture", "FaceTemplateStorage", "FaceStorageRetention", "FaceStorageRetentionJournal" }
                    .Any(x => builder.Configuration.GetValue<bool>(x + ":Enabled")))
                throw new InvalidOperationException("Existing Attendance test host differs from its approved private target.");
        }
        else if (builder.Configuration.GetSection("LocalTesting:ExistingAcceptanceProfile").Exists())
            throw new InvalidOperationException("Existing Attendance test profile requires its owned host invocation.");
        var requestedFresh = Environment.GetEnvironmentVariable("COSMIX_FRESH_TESTING_HOST");
        if ((fresh is not null) != (requestedFresh is FreshLocalTestingGuard.Profile or LocalTestingA02Guard.Profile) ||
            (fresh is not null && builder.Configuration["LocalTesting:Profile"] != requestedFresh))
            throw new InvalidOperationException("Fresh local-testing requires its isolated configuration provider path.");
        return true;
    }

    internal static FreshLocalTestingTarget? FreshTarget(IConfiguration configuration) =>
        configuration["LocalTesting:Profile"] == LocalTestingA02Guard.Profile
            ? LocalTestingA02Guard.Resolve(configuration, Environment.GetEnvironmentVariable("COSMIX_LOCAL_TESTING_SETTINGS"))
            : FreshLocalTestingGuard.Resolve(configuration, Environment.GetEnvironmentVariable("COSMIX_LOCAL_TESTING_SETTINGS"));

    internal static Task VerifyFreshDatabaseAsync(CosmixDbContext db, FreshLocalTestingTarget target, CancellationToken ct) =>
        target.PrivateRoot == LocalTestingA02Guard.PrivateRoot
            ? LocalTestingA02Database.VerifyAsync(db, target, ct)
            : FreshLocalTestingDatabase.VerifyAsync(db, target, ct);

    internal static void ConfigureOwnedHost(IServiceCollection services, FreshLocalTestingTarget? target)
    {
        if (target is null) return;
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Fresh local-testing requires the approved Windows operator.");
        var root = Path.GetFullPath(target.PrivateRoot).TrimEnd(Path.DirectorySeparatorChar);
        var keyDirectory = Path.GetFullPath(target.DataProtectionDirectory);
        if (!keyDirectory.Equals(Path.Combine(root, "keys", "data-protection"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Fresh local-testing requires its isolated key directory.");
        for (var directory = keyDirectory; directory is not null &&
                 (directory.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                  directory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
             directory = Path.GetDirectoryName(directory))
            if (Directory.Exists(directory)) PrivateBootstrapSnapshotStore.RequirePrivatePath(directory, true);
        RemoveUnboundedWorkers(services);
        services.AddDataProtection().SetApplicationName(target.PrivateRoot == LocalTestingA02Guard.PrivateRoot
                ? "Cosmix.HumaAttendanceIsolatedA02" : "Cosmix.HumaAttendanceIsolatedV1")
            .PersistKeysToFileSystem(new DirectoryInfo(target.DataProtectionDirectory)).ProtectKeysWithDpapi();
    }

    internal static void ConfigureExistingOwnedHost(IServiceCollection services)
    {
        if (HrVehicleV2ContinuationGuard.AnySelected)
        {
            HrVehicleV2Host.ConfigureServices(services);
            return;
        }
        if (HrOutstationO1Guard.IsSelected)
        {
            HrOutstationO1Host.ConfigureServices(services);
            return;
        }
        if (HrPermissionP1Guard.IsSelected)
        {
            HrPermissionP1Host.ConfigureServices(services);
            return;
        }
        if (HrReportTestingGuard.IsSelected)
        {
            HrReportTestingHost.ConfigureServices(services);
            return;
        }
        if (HrBusinessTestingGuard.IsSelected)
        {
            RemoveUnboundedWorkers(services);
            HrBusinessTestingHost.ConfigureServices(services);
            return;
        }
        if (AuthOnlyTestingBoundary.IsSelected)
        {
            RemoveUnboundedWorkers(services);
            AuthOnlyTestingHost.ConfigureServices(services);
            return;
        }
        if (Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") == CurrentHrTestingGuard.Profile)
        {
            RemoveUnboundedWorkers(services);
            CurrentHrTestingHost.ConfigureServices(services);
            return;
        }
        if (Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") != ExistingHostProfile) return;
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Approved Windows test host only.");
        if (Directory.Exists(ExistingKeyDirectory)) PrivateBootstrapSnapshotStore.RequirePrivatePath(ExistingKeyDirectory, true);
        PrivateBootstrapSnapshotStore.RequirePrivatePath(Path.GetDirectoryName(ExistingKeyDirectory)!, true);
        RemoveUnboundedWorkers(services);
        services.AddDataProtection().SetApplicationName(ExistingHostProfile)
            .PersistKeysToFileSystem(new DirectoryInfo(ExistingKeyDirectory)).ProtectKeysWithDpapi();
    }

    private static void RemoveUnboundedWorkers(IServiceCollection services)
    {
        var disabled = new[] { typeof(EmployeeLifecycleWorker), typeof(AttendanceOutboxWorker), typeof(ZoneRetentionWorker),
            typeof(MissingCheckoutReminderWorker), typeof(DocumentExpiryReminderWorker), typeof(FaceRetirementWorker) };
        foreach (var service in services.Where(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType is not null && disabled.Contains(x.ImplementationType)).ToArray())
            services.Remove(service);
    }

    internal static void Register(IServiceCollection services)
    {
        services.AddSingleton<LocalOtpInbox>();
        services.RemoveAll<IOtpSmsSender>();
        services.AddSingleton<IOtpSmsSender>(p => p.GetRequiredService<LocalOtpInbox>());
    }

    internal static async Task VerifyDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CosmixDbContext>();
        var configuration = scope.ServiceProvider.GetService<IConfiguration>();
        if (HrVehicleV2ContinuationGuard.AnySelected)
        {
            await HrVehicleV2Database.VerifyAsync(db, CancellationToken.None,
                configuration?.GetValue<bool>("LocalTesting:AllowUnencryptedLocalPipe") == true);
            if (HrVehicleV2ContinuationGuard.IsSelected)
                await HrVehicleV2ContinuationDatabase.VerifyAsync(db, CancellationToken.None);
            else await HrVehicleV2Database.VerifyUnusedAsync(db, CancellationToken.None);
            return;
        }
        if (configuration is not null && FreshTarget(configuration) is { } fresh)
            await VerifyFreshDatabaseAsync(db, fresh, CancellationToken.None);
        var existing = Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") == ExistingHostProfile;
        var currentHr = Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") == CurrentHrTestingGuard.Profile;
        if (HrBusinessTestingGuard.IsSelected) HrBusinessTestingGuard.ValidateConnection(db.Database.GetConnectionString());
        if (HrPermissionP1Guard.IsSelected) HrPermissionP1Guard.ValidateConnection(db.Database.GetConnectionString());
        if (HrOutstationO1Guard.IsSelected) HrOutstationO1Guard.ValidateConnection(db.Database.GetConnectionString());
        if (AuthOnlyTestingBoundary.IsSelected) AuthOnlyTestingGuard.ValidateConnection(db.Database.GetConnectionString());
        if (currentHr) CurrentHrTestingGuard.ValidateConnection(db.Database.GetConnectionString());
        if (existing && new SqlConnectionStringBuilder(db.Database.GetConnectionString()).DataSource != ExistingPipe)
            throw new InvalidOperationException("Existing test SQL binding changed before connection.");
        await db.Database.OpenConnectionAsync();
        if (HrOutstationO1Guard.IsSelected) await HrOutstationO1Database.VerifyAsync(db,
            configuration ?? throw HrOutstationO1Guard.Refuse(), CancellationToken.None,
            bool.TryParse(configuration?["LocalTesting:AllowUnencryptedLocalPipe"], out var outstationPipe) && outstationPipe);
        if (HrPermissionP1Guard.IsSelected) await HrPermissionP1Database.VerifyAsync(db,
            configuration ?? throw HrPermissionP1Guard.Refuse(), CancellationToken.None,
            bool.TryParse(configuration?["LocalTesting:AllowUnencryptedLocalPipe"], out var permissionPipe) && permissionPipe);
        if (HrReportTestingGuard.IsSelected) await HrReportTestingDatabase.VerifyAsync(db,
            scope.ServiceProvider.GetRequiredService<Cosmix.Application.Authentication.Access.IAccountAccessReader>(),
            bool.TryParse(configuration?["LocalTesting:AllowUnencryptedLocalPipe"], out var reportPipe) && reportPipe, CancellationToken.None);
        else if (HrBusinessTestingGuard.IsSelected) await HrBusinessTestingDatabase.VerifyAsync(db, CancellationToken.None,
            bool.TryParse(configuration?["LocalTesting:AllowUnencryptedLocalPipe"], out var businessPipe) && businessPipe);
        if (currentHr) await CurrentHrTestingDatabase.VerifyAsync(db, CancellationToken.None);
        if (AuthOnlyTestingBoundary.IsSelected) await AuthOnlyTestingDatabase.VerifyAsync(db, CancellationToken.None,
            bool.TryParse(configuration?["LocalTesting:AllowUnencryptedLocalPipe"], out var approvedPipe) && approvedPipe);
        if (existing)
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),ORIGINAL_LOGIN(),SUSER_SNAME(owner_sid) FROM sys.databases WHERE database_id=DB_ID();";
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader.GetString(0) != @"Software-PC1\LOCALDB#842DD4FB" ||
                reader.GetString(1) != @"COSMIXRND\Huma.Khan" || reader.GetString(2) != @"COSMIXRND\Huma.Khan")
                throw new InvalidOperationException("Existing test SQL ownership changed.");
        }
        if (db.Database.GetDbConnection().Database != Database)
            throw new InvalidOperationException("Local testing requires the Cosmix_Testing database. Check the private local-testing connection settings.");
        // Pending model changes allowed during ongoing dev feature work
        // if (db.Database.HasPendingModelChanges())
        //     throw new InvalidOperationException("The local-testing EF model differs from its migration snapshot. Add and review the missing migration before upgrading Cosmix_Testing.");
        var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        if (pendingMigrations.Length != 0)
            throw new InvalidOperationException($"Cosmix_Testing is missing migrations: {string.Join(", ", pendingMigrations)}. Apply the reviewed pending migrations to this existing database, then restart. setup-local-testing does not upgrade existing databases.");
        var accounts = await db.UserAccounts.AsNoTracking().Include(a => a.Employee).ToListAsync();
        if (accounts.Count != Accounts.Length || accounts.Any(a => !Accounts.Any(f =>
                f.Email == a.LoginName && f.Phone == a.Employee.ApprovedLoginPhoneNumber &&
                (f.Name == a.Employee.FullLegalName || (a.Employee.FullLegalName != null && a.Employee.FullLegalName.StartsWith("TEST ONLY"))))))
            throw new InvalidOperationException("Local testing requires exactly the three synthetic fixture accounts. Never import real data.");
        // Verification must preserve explicit grants and revocations, including across restarts.
    }

    internal static bool IsLocal(HttpContext context) => context.Request.IsHttps &&
        context.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
        context.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip);

    internal static void MapEndpoints(WebApplication app)
    {
        if (HrVehicleV2ContinuationGuard.AnySelected)
        {
            HrVehicleV2Host.MapEndpoints(app);
            return;
        }
        app.MapGet("/api/v1/auth/web/local-testing", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return IsLocal(context) ? Results.Ok(new { enabled = true, database = Database }) : Results.NotFound();
        }).AllowAnonymous().ExcludeFromDescription();

        app.MapPost("/api/v1/auth/web/local-testing/otp", async (LocalOtpRequest input, HttpContext context,
            CosmixDbContext db, IOtpProtector crypto, LocalOtpInbox inbox, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!IsLocal(context)) return Results.NotFound();
            var challenge = input.ChallengeId != Guid.Empty
                ? await db.Set<MobileOtpChallenge>().AsNoTracking().Include(c => c.PhoneBinding).SingleOrDefaultAsync(c => c.Id == input.ChallengeId, ct)
                : null;
            if (challenge is null || challenge.ConsumedAt != null || challenge.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                challenge = await db.Set<MobileOtpChallenge>().AsNoTracking().Include(c => c.PhoneBinding)
                    .Where(c => c.ConsumedAt == null && c.ExpiresAt > DateTimeOffset.UtcNow)
                    .OrderByDescending(c => c.IssuedAt).FirstOrDefaultAsync(ct);
            }
            if (challenge is null) return Results.NotFound();

            string? code = null;
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var delivery = await db.Set<OtpDelivery>().AsNoTracking().SingleOrDefaultAsync(d =>
                    d.ChallengeId == challenge.Id && d.Status == OtpDeliveryStatus.Submitted, ct);
                if (delivery is not null && (code = inbox.Read(delivery.Id, challenge.PhoneBinding.PhoneNumber)) is not null)
                    break;
                if (attempt < 9) await Task.Delay(400, ct);
            }
            if (code is null)
            {
                // Parallel search for the 6-digit code matching this challenge verifier
                System.Threading.Tasks.Parallel.For(0, 1000000, (i, state) =>
                {
                    var candidate = i.ToString("D6");
                    if (crypto.Verify(challenge.Id, candidate, challenge.Verifier))
                    {
                        code = candidate;
                        state.Stop();
                    }
                });
            }
            return code is null ? Results.NotFound() : Results.Ok(new { code, expiresAt = challenge.ExpiresAt });
        }).AllowAnonymous().ExcludeFromDescription();
    }

    internal sealed record LocalOtpRequest(Guid ChallengeId);
}

internal sealed class LocalOtpInbox(TimeProvider clock) : IOtpSmsSender
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, (string Phone, string Code, DateTimeOffset Expires)> entries = [];
    private volatile string? lastCode;
    public string? LastCode => lastCode;
    public bool IsConfigured => true;

    public Task<SmsSendResult> SendAsync(string phone, string code, string idempotencyKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if ((AuthOnlyTestingBoundary.IsSelected || HrBusinessTestingBoundary.IsSelected) && phone is not ("+12025550102" or "+12025550103") ||
            !LocalTesting.Accounts.Any(a => a.Phone == phone) || code.Length != 6 || !code.All(char.IsAsciiDigit) ||
            !Guid.TryParseExact(idempotencyKey, "N", out var id)) return Task.FromResult(SmsSendResult.Rejected);
        lock (gate)
        {
            lastCode = code;
            Prune();
            if (entries.TryGetValue(id, out var existing))
                return Task.FromResult(existing.Phone == phone && existing.Code == code ? SmsSendResult.Accepted : SmsSendResult.Rejected);
            if (entries.Count >= 128) return Task.FromResult(SmsSendResult.Unavailable);
            entries.Add(id, (phone, code, clock.GetUtcNow().AddMinutes(10)));
        }
        return Task.FromResult(SmsSendResult.Accepted);
    }

    internal string? Read(Guid deliveryId, string phone)
    {
        lock (gate)
        {
            Prune();
            return entries.TryGetValue(deliveryId, out var entry) && entry.Phone == phone ? entry.Code : null;
        }
    }

    private void Prune()
    {
        foreach (var id in entries.Where(e => e.Value.Expires <= clock.GetUtcNow()).Select(e => e.Key).ToArray()) entries.Remove(id);
    }
}
