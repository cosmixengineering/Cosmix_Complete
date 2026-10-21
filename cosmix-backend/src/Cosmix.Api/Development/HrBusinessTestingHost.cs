using System.Security.AccessControl;
using System.Security.Principal;
using Cosmix.Api.Authentication;
using Cosmix.Infrastructure.Authentication.Bootstrap;
using Cosmix.Infrastructure.Zones;
using Microsoft.AspNetCore.DataProtection;

namespace Cosmix.Api.Development;

internal static class HrBusinessTestingHost
{
    internal static void ValidatePrivatePaths(string? settingsPath)
    {
        if (!OperatingSystem.IsWindows() || !HrBusinessTestingGuard.Same(settingsPath, HrBusinessTestingGuard.SettingsPath))
            throw HrBusinessTestingGuard.Refuse();
        using var identity = WindowsIdentity.GetCurrent();
        if (!HrBusinessTestingGuard.Same(identity.Name, HrBusinessTestingGuard.Owner) || identity.User is null)
            throw HrBusinessTestingGuard.Refuse();
        var root = HrBusinessTestingGuard.PrivateRoot;
        foreach (var directory in new[] { root, root + @"\config", root + @"\files", root + @"\keys", HrBusinessTestingGuard.DataProtectionDirectory })
            Validate(directory, true);
        Validate(HrBusinessTestingGuard.SettingsPath, false);
        Validate(root + @"\keys\files.key", false);
        if (new FileInfo(root + @"\keys\files.key").Length != 32) throw HrBusinessTestingGuard.Refuse();
        foreach (var entry in Directory.EnumerateFileSystemEntries(HrBusinessTestingGuard.DataProtectionDirectory))
        {
            if (Directory.Exists(entry)) throw HrBusinessTestingGuard.Refuse();
            Validate(entry, false);
        }

        void Validate(string path, bool directory)
        {
            PrivateBootstrapSnapshotStore.RequirePrivatePath(path, directory);
            FileSystemSecurity security = directory ? new DirectoryInfo(path).GetAccessControl() : new FileInfo(path).GetAccessControl();
            if (security.GetOwner(typeof(SecurityIdentifier))?.Value != identity.User.Value) throw HrBusinessTestingGuard.Refuse();
        }
    }

    internal static void ConfigureServices(IServiceCollection services)
    {
        if (!OperatingSystem.IsWindows()) throw HrBusinessTestingGuard.Refuse();
        // AddInfrastructure also registers this retention job outside the six API worker registrations.
        foreach (var service in services.Where(s => s.ServiceType == typeof(IHostedService) &&
                     s.ImplementationType == typeof(ZoneClaimRetentionWorker)).ToArray())
            services.Remove(service);
        var dataProtectionWorker = typeof(DataProtectionServiceCollectionExtensions).Assembly.GetType(
            "Microsoft.AspNetCore.DataProtection.Internal.DataProtectionHostedService", throwOnError: true)!;
        var workers = services.Where(s => s.ServiceType == typeof(IHostedService)).ToArray();
        if (workers.Count(s => s.ImplementationType == typeof(OtpDeliveryWorker)) != 1 ||
            workers.Any(s => s.Lifetime != ServiceLifetime.Singleton ||
                s.ImplementationType != typeof(OtpDeliveryWorker) && s.ImplementationType != dataProtectionWorker))
            throw HrBusinessTestingGuard.Refuse();
        services.AddDataProtection().SetApplicationName(HrBusinessTestingGuard.ApplicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(HrBusinessTestingGuard.DataProtectionDirectory)).ProtectKeysWithDpapi();
    }
}
