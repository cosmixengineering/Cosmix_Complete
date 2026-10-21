using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Infrastructure.Database;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cosmix.Api.Development;

internal static class HrReportTestingHost
{
    internal static void ValidatePrivatePaths(string? settingsPath)
    {
        HrBusinessTestingHost.ValidatePrivatePaths(settingsPath);
        if (Directory.EnumerateFileSystemEntries(HrReportTestingGuard.PrivateRoot + @"\files").Any() ||
            Directory.EnumerateFileSystemEntries(HrBusinessTestingGuard.DataProtectionDirectory).Any())
            throw HrReportTestingGuard.Refuse();
    }

    internal static void ConfigureServices(IServiceCollection services)
    {
        if (!HrReportTestingGuard.IsSelected || !OperatingSystem.IsWindows()) throw HrReportTestingGuard.Refuse();
        // No background queue, retention, notification or OTP execution in R1.
        services.RemoveAll<IHostedService>();
        services.RemoveAll<AttendanceJobOptions>();
        services.AddSingleton(new AttendanceJobOptions { Enabled = true, BatchSize = 1, MaximumAttempts = 1 });
        services.AddScoped<HrReportTestingDispatcher>();
        services.AddScoped<BoundOtpRepository>();
        services.RemoveAll<IOtpDeliveryRepository>();
        services.AddScoped<IOtpDeliveryRepository>(p => p.GetRequiredService<BoundOtpRepository>());
        services.RemoveAll<IOtpSmsSender>();
        services.AddSingleton<IOtpSmsSender>(p => new HrOnlySmsSender(p.GetRequiredService<LocalOtpInbox>()));
        services.AddDataProtection().SetApplicationName(HrReportTestingGuard.ApplicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(HrBusinessTestingGuard.DataProtectionDirectory)).ProtectKeysWithDpapi();
        services.RemoveAll<IHostedService>();
    }

    internal static async Task DispatchOtpAsync(HttpContext context, Guid challengeId)
    {
        var repository = context.RequestServices.GetRequiredService<BoundOtpRepository>();
        repository.Bind(challengeId);
        await context.RequestServices.GetRequiredService<OtpDispatchService>().DispatchOneAsync(context.RequestAborted);
    }

    // A fresh request scope is inert until the boundary binds its one captured login challenge.
    internal sealed class BoundOtpRepository(CosmixDbContext db) : IOtpDeliveryRepository
    {
        private Guid? challengeId, deliveryId;
        internal void Bind(Guid id)
        {
            if (id == Guid.Empty || challengeId.HasValue) throw HrReportTestingGuard.Refuse();
            challengeId = id;
        }

        public async Task<IReadOnlyList<OtpDelivery>> GetNonterminalAsync(DateTimeOffset now, CancellationToken ct)
        {
            if (!challengeId.HasValue || deliveryId.HasValue) throw HrReportTestingGuard.Refuse();
            var challenge = await db.Set<MobileOtpChallenge>().AsNoTracking().Include(c => c.PhoneBinding)
                .SingleOrDefaultAsync(c => c.Id == challengeId, ct);
            if (challenge is null || challenge.UserAccountId != HrReportTestingGuard.HrAccount ||
                challenge.Purpose != "WebLogin" || !challenge.IsUsableAt(now) ||
                challenge.PhoneBinding.PhoneNumber != HrReportTestingGuard.Phone || challenge.PhoneBinding.RevokedAt.HasValue)
                throw HrReportTestingGuard.Refuse();
            var items = await db.Set<OtpDelivery>().Where(d => d.ChallengeId == challengeId).ToArrayAsync(ct);
            if (items.Length != 1 || items[0].Status != OtpDeliveryStatus.Pending || items[0].Attempts != 0 ||
                items[0].ExpiresAt <= now) throw HrReportTestingGuard.Refuse();
            deliveryId = items[0].Id;
            return items;
        }

        public Task<OtpDelivery?> GetAsync(Guid id, CancellationToken ct)
        {
            if (!deliveryId.HasValue || id != deliveryId) throw HrReportTestingGuard.Refuse();
            return db.Set<OtpDelivery>().SingleOrDefaultAsync(d => d.Id == id && d.ChallengeId == challengeId, ct);
        }
    }

    private sealed class HrOnlySmsSender(LocalOtpInbox inbox) : IOtpSmsSender
    {
        public bool IsConfigured => true;
        public Task<SmsSendResult> SendAsync(string phone, string code, string idempotencyKey, CancellationToken ct) =>
            phone == HrReportTestingGuard.Phone ? inbox.SendAsync(phone, code, idempotencyKey, ct) : Task.FromResult(SmsSendResult.Rejected);
    }
}
