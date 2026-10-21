using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Security.AccessControl;
using System.Security.Principal;
using Cosmix.Api.Authentication;
using Cosmix.Infrastructure.Authentication.Bootstrap;
using Cosmix.Infrastructure.Zones;
using Microsoft.AspNetCore.DataProtection;

namespace Cosmix.Api.Development;

internal static class HrPermissionP1Host
{
    internal static void ValidatePrivatePaths(string? settingsPath)
    {
        if (!OperatingSystem.IsWindows() || !HrPermissionP1Guard.Same(settingsPath, HrPermissionP1Guard.SettingsPath))
            throw HrPermissionP1Guard.Refuse();
        using var identity = WindowsIdentity.GetCurrent();
        if (!HrPermissionP1Guard.Same(identity.Name, HrPermissionP1Guard.Owner) || identity.User is null)
            throw HrPermissionP1Guard.Refuse();
        var root = HrPermissionP1Guard.PrivateRoot;
        foreach (var directory in new[] { root, root + @"\config", root + @"\files", root + @"\keys", HrPermissionP1Guard.DataProtectionDirectory })
            Validate(directory, true);
        Validate(HrPermissionP1Guard.SettingsPath, false);
        Validate(root + @"\keys\files.key", false);
        if (new FileInfo(root + @"\keys\files.key").Length != 32) throw HrPermissionP1Guard.Refuse();
        foreach (var entry in Directory.EnumerateFileSystemEntries(HrPermissionP1Guard.DataProtectionDirectory))
        {
            if (Directory.Exists(entry)) throw HrPermissionP1Guard.Refuse();
            Validate(entry, false);
        }

        void Validate(string path, bool directory)
        {
            PrivateBootstrapSnapshotStore.RequirePrivatePath(path, directory);
            FileSystemSecurity security = directory ? new DirectoryInfo(path).GetAccessControl() : new FileInfo(path).GetAccessControl();
            if (security.GetOwner(typeof(SecurityIdentifier))?.Value != identity.User.Value) throw HrPermissionP1Guard.Refuse();
        }
    }

    internal static void ConfigureServices(IServiceCollection services)
    {
        if (!HrPermissionP1Guard.IsSelected || !OperatingSystem.IsWindows()) throw HrPermissionP1Guard.Refuse();
        if (Directory.EnumerateFileSystemEntries(HrPermissionP1Guard.PrivateRoot + @"\files").Any() ||
            Directory.EnumerateFileSystemEntries(HrPermissionP1Guard.DataProtectionDirectory).Any())
            throw HrPermissionP1Guard.Refuse();
        services.RemoveAll<IHostedService>();
        services.AddScoped<BoundOtpRepository>();
        services.RemoveAll<IOtpDeliveryRepository>();
        services.AddScoped<IOtpDeliveryRepository>(p => p.GetRequiredService<BoundOtpRepository>());
        services.RemoveAll<IOtpSmsSender>();
        services.AddSingleton<IOtpSmsSender>(p => new P1SmsSender(p.GetRequiredService<LocalOtpInbox>()));
        services.AddSingleton<HrPermissionP1Boundary>();
        services.AddDataProtection().SetApplicationName(HrPermissionP1Guard.ApplicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(HrPermissionP1Guard.DataProtectionDirectory)).ProtectKeysWithDpapi();
        services.RemoveAll<IHostedService>();
    }

    internal static async Task DispatchOtpAsync(HttpContext context, Guid challengeId, Guid accountId, string phone)
    {
        context.RequestServices.GetRequiredService<BoundOtpRepository>().Bind(challengeId, accountId, phone);
        await context.RequestServices.GetRequiredService<OtpDispatchService>().DispatchOneAsync(context.RequestAborted);
    }

    internal sealed class BoundOtpRepository(CosmixDbContext db) : IOtpDeliveryRepository
    {
        private Guid? challengeId, deliveryId, accountId;
        private string? phone;
        internal void Bind(Guid challenge, Guid account, string number)
        {
            if (challenge == Guid.Empty || account == Guid.Empty || challengeId.HasValue ||
                number is not ("+12025550101" or "+12025550102" or "+12025550103")) throw HrPermissionP1Guard.Refuse();
            challengeId = challenge; accountId = account; phone = number;
        }
        public async Task<IReadOnlyList<OtpDelivery>> GetNonterminalAsync(DateTimeOffset now, CancellationToken ct)
        {
            if (!challengeId.HasValue || deliveryId.HasValue) throw HrPermissionP1Guard.Refuse();
            var challenge = await db.Set<MobileOtpChallenge>().AsNoTracking().Include(c => c.PhoneBinding)
                .SingleOrDefaultAsync(c => c.Id == challengeId, ct);
            if (challenge is null || challenge.UserAccountId != accountId || challenge.Purpose != "WebLogin" ||
                !challenge.IsUsableAt(now) || challenge.PhoneBinding.PhoneNumber != phone || challenge.PhoneBinding.RevokedAt.HasValue)
                throw HrPermissionP1Guard.Refuse();
            var rows = await db.Set<OtpDelivery>().Where(d => d.ChallengeId == challengeId).ToArrayAsync(ct);
            if (rows.Length != 1 || rows[0].Status != OtpDeliveryStatus.Pending || rows[0].Attempts != 0 ||
                rows[0].ExpiresAt <= now) throw HrPermissionP1Guard.Refuse();
            deliveryId = rows[0].Id;
            return rows;
        }
        public Task<OtpDelivery?> GetAsync(Guid id, CancellationToken ct)
        {
            if (id != deliveryId) throw HrPermissionP1Guard.Refuse();
            return db.Set<OtpDelivery>().SingleOrDefaultAsync(d => d.Id == id && d.ChallengeId == challengeId, ct);
        }
    }
    private sealed class P1SmsSender(LocalOtpInbox inbox) : IOtpSmsSender
    {
        public bool IsConfigured => true;
        public Task<SmsSendResult> SendAsync(string phone, string code, string key, CancellationToken ct) =>
            phone is "+12025550101" or "+12025550102" or "+12025550103"
                ? inbox.SendAsync(phone, code, key, ct) : Task.FromResult(SmsSendResult.Rejected);
    }
}
