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

internal static class HrOutstationO1Host
{
    internal static void ValidatePrivatePaths(string? settingsPath)
    {
        if (!OperatingSystem.IsWindows() || !HrOutstationO1Guard.Same(settingsPath, HrOutstationO1Guard.SettingsPath))
            throw HrOutstationO1Guard.Refuse();
        using var identity = WindowsIdentity.GetCurrent();
        if (!HrOutstationO1Guard.Same(identity.Name, HrOutstationO1Guard.Owner) || identity.User is null)
            throw HrOutstationO1Guard.Refuse();
        var root = HrOutstationO1Guard.PrivateRoot;
        foreach (var directory in new[] { root, root + @"\config", root + @"\files", root + @"\keys", HrOutstationO1Guard.DataProtectionDirectory })
            Validate(directory, true);
        Validate(HrOutstationO1Guard.SettingsPath, false);
        Validate(root + @"\keys\files.key", false);
        if (new FileInfo(root + @"\keys\files.key").Length != 32) throw HrOutstationO1Guard.Refuse();
        foreach (var entry in Directory.EnumerateFileSystemEntries(HrOutstationO1Guard.DataProtectionDirectory))
        {
            if (Directory.Exists(entry)) throw HrOutstationO1Guard.Refuse();
            Validate(entry, false);
        }

        void Validate(string path, bool directory)
        {
            PrivateBootstrapSnapshotStore.RequirePrivatePath(path, directory);
            FileSystemSecurity security = directory ? new DirectoryInfo(path).GetAccessControl() : new FileInfo(path).GetAccessControl();
            if (security.GetOwner(typeof(SecurityIdentifier))?.Value != identity.User.Value) throw HrOutstationO1Guard.Refuse();
        }
    }

    internal static void ConfigureServices(IServiceCollection services)
    {
        if (!HrOutstationO1Guard.IsSelected || !OperatingSystem.IsWindows()) throw HrOutstationO1Guard.Refuse();
        if (Directory.EnumerateFileSystemEntries(HrOutstationO1Guard.PrivateRoot + @"\files").Any() ||
            Directory.EnumerateFileSystemEntries(HrOutstationO1Guard.DataProtectionDirectory).Any())
            throw HrOutstationO1Guard.Refuse();
        services.RemoveAll<IHostedService>();
        services.AddScoped<BoundOtpRepository>();
        services.RemoveAll<IOtpDeliveryRepository>();
        services.AddScoped<IOtpDeliveryRepository>(p => p.GetRequiredService<BoundOtpRepository>());
        services.RemoveAll<IOtpSmsSender>();
        services.AddSingleton<IOtpSmsSender>(p => new O1SmsSender(p.GetRequiredService<LocalOtpInbox>()));
        services.AddSingleton<HrOutstationO1Boundary>();
        services.AddDataProtection().SetApplicationName(HrOutstationO1Guard.ApplicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(HrOutstationO1Guard.DataProtectionDirectory)).ProtectKeysWithDpapi();
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
                number is not ("+12025550102" or "+12025550103")) throw HrOutstationO1Guard.Refuse();
            challengeId = challenge; accountId = account; phone = number;
        }
        public async Task<IReadOnlyList<OtpDelivery>> GetNonterminalAsync(DateTimeOffset now, CancellationToken ct)
        {
            if (!challengeId.HasValue || deliveryId.HasValue) throw HrOutstationO1Guard.Refuse();
            var challenge = await db.Set<MobileOtpChallenge>().AsNoTracking().Include(c => c.PhoneBinding)
                .SingleOrDefaultAsync(c => c.Id == challengeId, ct);
            if (challenge is null || challenge.UserAccountId != accountId || challenge.Purpose != "WebLogin" ||
                !challenge.IsUsableAt(now) || challenge.PhoneBinding.PhoneNumber != phone || challenge.PhoneBinding.RevokedAt.HasValue)
                throw HrOutstationO1Guard.Refuse();
            var rows = await db.Set<OtpDelivery>().Where(d => d.ChallengeId == challengeId).ToArrayAsync(ct);
            if (rows.Length != 1 || rows[0].Status != OtpDeliveryStatus.Pending || rows[0].Attempts != 0 ||
                rows[0].ExpiresAt <= now) throw HrOutstationO1Guard.Refuse();
            deliveryId = rows[0].Id;
            return rows;
        }
        public Task<OtpDelivery?> GetAsync(Guid id, CancellationToken ct)
        {
            if (id != deliveryId) throw HrOutstationO1Guard.Refuse();
            return db.Set<OtpDelivery>().SingleOrDefaultAsync(d => d.Id == id && d.ChallengeId == challengeId, ct);
        }
    }
    private sealed class O1SmsSender(LocalOtpInbox inbox) : IOtpSmsSender
    {
        public bool IsConfigured => true;
        public Task<SmsSendResult> SendAsync(string phone, string code, string key, CancellationToken ct) =>
            phone is "+12025550102" or "+12025550103"
                ? inbox.SendAsync(phone, code, key, ct) : Task.FromResult(SmsSendResult.Rejected);
    }
}
