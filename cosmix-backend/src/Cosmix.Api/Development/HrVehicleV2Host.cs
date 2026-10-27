using System.Security.AccessControl;
using System.Security.Principal;
using Cosmix.Api.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Infrastructure.Authentication.Bootstrap;
using Cosmix.Infrastructure.Database;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cosmix.Api.Development;

internal static class HrVehicleV2Host
{
    internal static void ValidatePrivatePaths(string? path)
    {
        var continuation = HrVehicleV2ContinuationGuard.IsSelected;
        var settings = continuation ? HrVehicleV2ContinuationGuard.SettingsPath : HrVehicleV2Guard.SettingsPath;
        var protection = continuation ? HrVehicleV2ContinuationGuard.DataProtectionDirectory : HrVehicleV2Guard.DataProtectionDirectory;
        if (!OperatingSystem.IsWindows() || !HrVehicleV2Guard.Same(path, settings)) throw HrVehicleV2Guard.Refuse();
        using var identity = WindowsIdentity.GetCurrent();
        if (!HrVehicleV2Guard.Same(identity.Name, HrOutstationO1Guard.Owner) || identity.User is null) throw HrVehicleV2Guard.Refuse();
        var root = continuation ? HrVehicleV2ContinuationGuard.PrivateRoot : HrVehicleV2Guard.PrivateRoot;
        foreach (var directory in new[] { root, root + @"\config", root + @"\files", root + @"\keys", protection })
            Validate(directory, true);
        Validate(settings, false);
        Validate(root + @"\keys\files.key", false);
        if (new FileInfo(root + @"\keys\files.key").Length != 32) throw HrVehicleV2Guard.Refuse();
        foreach (var file in Directory.EnumerateFileSystemEntries(protection)) Validate(file, false);
        if (continuation)
        {
            Validate(HrVehicleV2Guard.PrivateRoot, true);
            Validate(HrVehicleV2Guard.PrivateRoot + @"\files", true);
            Validate(HrVehicleV2Guard.PrivateRoot + @"\keys", true);
            Validate(HrVehicleV2Guard.PrivateRoot + @"\keys\files.key", false);
        }

        void Validate(string value, bool directory)
        {
            PrivateBootstrapSnapshotStore.RequirePrivatePath(value, directory);
            FileSystemSecurity acl = directory ? new DirectoryInfo(value).GetAccessControl() : new FileInfo(value).GetAccessControl();
            if (acl.GetOwner(typeof(SecurityIdentifier))?.Value != identity.User.Value) throw HrVehicleV2Guard.Refuse();
        }
    }

    internal static void ConfigureServices(IServiceCollection services)
    {
        if (!HrVehicleV2ContinuationGuard.AnySelected || !OperatingSystem.IsWindows()) throw HrVehicleV2Guard.Refuse();
        var continuation = HrVehicleV2ContinuationGuard.IsSelected;
        var root = continuation ? HrVehicleV2ContinuationGuard.PrivateRoot : HrVehicleV2Guard.PrivateRoot;
        var protection = continuation ? HrVehicleV2ContinuationGuard.DataProtectionDirectory : HrVehicleV2Guard.DataProtectionDirectory;
        if (Directory.EnumerateFileSystemEntries(root + @"\files").Any() ||
            Directory.EnumerateFileSystemEntries(protection).Any()) throw HrVehicleV2Guard.Refuse();
        services.RemoveAll<IHostedService>();
        services.AddScoped<BoundOtpRepository>();
        services.RemoveAll<IOtpDeliveryRepository>();
        services.AddScoped<IOtpDeliveryRepository>(p => p.GetRequiredService<BoundOtpRepository>());
        services.RemoveAll<IOtpSmsSender>();
        services.AddSingleton<IOtpSmsSender>(p => p.GetRequiredService<LocalOtpInbox>());
        services.AddSingleton<HrVehicleV2Boundary>();
        services.AddDataProtection().SetApplicationName(continuation ? HrVehicleV2ContinuationGuard.ApplicationName : HrVehicleV2Guard.ApplicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(protection)).ProtectKeysWithDpapi();
        if (continuation) HrVehicleV2ContinuationFiles.ConfigureServices(services);
        services.RemoveAll<IHostedService>();
    }

    internal static void MapEndpoints(WebApplication app)
    {
        if (!HrVehicleV2ContinuationGuard.AnySelected || !app.Environment.IsDevelopment()) throw HrVehicleV2Guard.Refuse();
        if (HrVehicleV2Guard.IsSelected) app.MapPost(HrVehicleV2Boundary.SeedPath, async (SeedRequest input, HttpContext context,
            HrVehicleV2Boundary boundary, IAccessManagementRepository access, IAntiforgery csrf) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Items[CurrentAuthentication.ItemKey] is not AuthenticatedAccount actor ||
                actor.AccountId != HrVehicleV2Guard.SuperAdminAccount || actor.EmployeeId != HrVehicleV2Guard.SuperAdminEmployee ||
                actor.Client != SessionClient.Web || !actor.MfaSatisfied ||
                !await access.IsCurrentSuperAdminAsync(actor, DateTimeOffset.UtcNow, context.RequestAborted)) return Results.Forbid();
            await csrf.ValidateRequestAsync(context);
            return await boundary.SeedAsync(input.VehicleId, async () =>
            {
                // Authentication may track accounts on the request context. The fixture receives its own clean context.
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<CosmixDbContext>();
                await HrVehicleV2Database.VerifyAsync(db, context.RequestAborted,
                    app.Configuration.GetValue<bool>("LocalTesting:AllowUnencryptedLocalPipe"));
                var seedAccess = scope.ServiceProvider.GetRequiredService<IAccessManagementRepository>();
                async Task Authorize(CancellationToken ct)
                {
                    if (!await seedAccess.IsCurrentSuperAdminAsync(actor, DateTimeOffset.UtcNow, ct)) throw HrVehicleV2Guard.Refuse();
                }
                var result = await HrVehicleV2Fixture.SeedAsync(db,
                    scope.ServiceProvider.GetRequiredService<IPrivateFileStore>(), input.VehicleId, Authorize, context.RequestAborted);
                return result;
            });
        }).RequireAuthorization().ExcludeFromDescription();

        // No latest-challenge lookup, OTP recovery, or synthetic authentication result.
        app.MapPost(HrVehicleV2Boundary.Auth + "local-testing/otp", async (LocalTesting.LocalOtpRequest input,
            HttpContext context, HrVehicleV2Boundary boundary, CosmixDbContext db, LocalOtpInbox inbox) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var identity = boundary.RequireOtpIdentity(context, input.ChallengeId);
            var challenge = await db.Set<MobileOtpChallenge>().AsNoTracking().Include(c => c.PhoneBinding)
                .SingleOrDefaultAsync(c => c.Id == input.ChallengeId, context.RequestAborted);
            if (challenge is null || challenge.UserAccountId != identity.Account || challenge.Purpose != "WebLogin" ||
                !challenge.IsUsableAt(DateTimeOffset.UtcNow) || challenge.PhoneBinding.PhoneNumber != identity.Phone ||
                challenge.PhoneBinding.RevokedAt.HasValue) throw HrVehicleV2Guard.Refuse();
            var delivery = await db.Set<OtpDelivery>().AsNoTracking().SingleOrDefaultAsync(d =>
                d.ChallengeId == input.ChallengeId && d.Status == OtpDeliveryStatus.Submitted, context.RequestAborted);
            var code = delivery is null ? null : inbox.Read(delivery.Id, identity.Phone);
            return code is null ? Results.NotFound() : Results.Ok(new { code, expiresAt = challenge.ExpiresAt });
        }).AllowAnonymous().ExcludeFromDescription();
    }

    internal sealed record SeedRequest(Guid VehicleId);

    internal static async Task DispatchOtpAsync(HttpContext context, Guid challenge, HrVehicleV2Guard.Identity identity)
    {
        context.RequestServices.GetRequiredService<BoundOtpRepository>().Bind(challenge, identity);
        await context.RequestServices.GetRequiredService<OtpDispatchService>().DispatchOneAsync(context.RequestAborted);
    }

    internal sealed class BoundOtpRepository(CosmixDbContext db) : IOtpDeliveryRepository
    {
        private Guid? challengeId, deliveryId;
        private HrVehicleV2Guard.Identity? identity;
        internal void Bind(Guid challenge, HrVehicleV2Guard.Identity actor)
        {
            if (challenge == Guid.Empty || challengeId.HasValue || !HrVehicleV2Guard.Actors.Contains(actor)) throw HrVehicleV2Guard.Refuse();
            challengeId = challenge; identity = actor;
        }
        public async Task<IReadOnlyList<OtpDelivery>> GetNonterminalAsync(DateTimeOffset now, CancellationToken ct)
        {
            if (!challengeId.HasValue || deliveryId.HasValue || identity is null) throw HrVehicleV2Guard.Refuse();
            var challenge = await db.Set<MobileOtpChallenge>().AsNoTracking().Include(c => c.PhoneBinding)
                .SingleOrDefaultAsync(c => c.Id == challengeId, ct);
            if (challenge is null || challenge.UserAccountId != identity.Account || challenge.Purpose != "WebLogin" ||
                !challenge.IsUsableAt(now) || challenge.PhoneBinding.PhoneNumber != identity.Phone || challenge.PhoneBinding.RevokedAt.HasValue)
                throw HrVehicleV2Guard.Refuse();
            var rows = await db.Set<OtpDelivery>().Where(d => d.ChallengeId == challengeId).ToArrayAsync(ct);
            if (rows.Length != 1 || rows[0].Status != OtpDeliveryStatus.Pending || rows[0].Attempts != 0 || rows[0].ExpiresAt <= now)
                throw HrVehicleV2Guard.Refuse();
            deliveryId = rows[0].Id;
            return rows;
        }
        public Task<OtpDelivery?> GetAsync(Guid id, CancellationToken ct)
        {
            if (id != deliveryId) throw HrVehicleV2Guard.Refuse();
            return db.Set<OtpDelivery>().SingleOrDefaultAsync(d => d.Id == id && d.ChallengeId == challengeId, ct);
        }
    }
}
