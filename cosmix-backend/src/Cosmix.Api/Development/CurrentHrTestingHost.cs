using System.Security.AccessControl;
using System.Security.Principal;
using Cosmix.Api.Authentication;
using Cosmix.Infrastructure.Authentication.Bootstrap;
using Cosmix.Infrastructure.Zones;
using Microsoft.AspNetCore.DataProtection;

namespace Cosmix.Api.Development;

internal static class CurrentHrTestingHost
{
    internal static void ValidatePrivatePaths(string? settingsPath)
    {
        if (!OperatingSystem.IsWindows() || !CurrentHrTestingGuard.Same(settingsPath, CurrentHrTestingGuard.SettingsPath))
            throw CurrentHrTestingGuard.Refuse();
        using var identity = WindowsIdentity.GetCurrent();
        if (!CurrentHrTestingGuard.Same(identity.Name, CurrentHrTestingGuard.Owner) || identity.User is null)
            throw CurrentHrTestingGuard.Refuse();
        var root = CurrentHrTestingGuard.PrivateRoot;
        foreach (var directory in new[] { root, root + @"\config", root + @"\files", root + @"\keys", CurrentHrTestingGuard.DataProtectionDirectory })
            Validate(directory, true);
        Validate(CurrentHrTestingGuard.SettingsPath, false);
        Validate(root + @"\keys\files.key", false);
        if (new FileInfo(root + @"\keys\files.key").Length != 32) throw CurrentHrTestingGuard.Refuse();
        foreach (var entry in Directory.EnumerateFileSystemEntries(CurrentHrTestingGuard.DataProtectionDirectory))
        {
            if (Directory.Exists(entry)) throw CurrentHrTestingGuard.Refuse();
            Validate(entry, false);
        }

        void Validate(string path, bool directory)
        {
            PrivateBootstrapSnapshotStore.RequirePrivatePath(path, directory);
            FileSystemSecurity security = directory ? new DirectoryInfo(path).GetAccessControl() : new FileInfo(path).GetAccessControl();
            if (security.GetOwner(typeof(SecurityIdentifier))?.Value != identity.User.Value) throw CurrentHrTestingGuard.Refuse();
        }
    }

    internal static void ConfigureServices(IServiceCollection services)
    {
        if (!OperatingSystem.IsWindows()) throw CurrentHrTestingGuard.Refuse();
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
            throw CurrentHrTestingGuard.Refuse();
        services.AddDataProtection().SetApplicationName(CurrentHrTestingGuard.Profile)
            .PersistKeysToFileSystem(new DirectoryInfo(CurrentHrTestingGuard.DataProtectionDirectory)).ProtectKeysWithDpapi();
    }
}
