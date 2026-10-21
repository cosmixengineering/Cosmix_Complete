using System.Security.AccessControl;
using System.Security.Principal;
using Cosmix.Api.Authentication;
using Cosmix.Infrastructure.Authentication.Bootstrap;
using Cosmix.Infrastructure.Zones;
using Microsoft.AspNetCore.DataProtection;

namespace Cosmix.Api.Development;

internal static class AuthOnlyTestingHost
{
    internal static void ValidatePrivatePaths(string? settingsPath)
    {
        if (!OperatingSystem.IsWindows() || !AuthOnlyTestingGuard.Same(settingsPath, AuthOnlyTestingGuard.SettingsPath))
            throw AuthOnlyTestingGuard.Refuse();
        using var identity = WindowsIdentity.GetCurrent();
        if (!AuthOnlyTestingGuard.Same(identity.Name, AuthOnlyTestingGuard.Owner) || identity.User is null)
            throw AuthOnlyTestingGuard.Refuse();
        var root = AuthOnlyTestingGuard.PrivateRoot;
        foreach (var directory in new[] { root, root + @"\config", root + @"\files", root + @"\keys", AuthOnlyTestingGuard.DataProtectionDirectory })
            Validate(directory, true);
        Validate(AuthOnlyTestingGuard.SettingsPath, false);
        Validate(root + @"\keys\files.key", false);
        if (new FileInfo(root + @"\keys\files.key").Length != 32) throw AuthOnlyTestingGuard.Refuse();
        foreach (var entry in Directory.EnumerateFileSystemEntries(AuthOnlyTestingGuard.DataProtectionDirectory))
        {
            if (Directory.Exists(entry)) throw AuthOnlyTestingGuard.Refuse();
            Validate(entry, false);
        }

        void Validate(string path, bool directory)
        {
            PrivateBootstrapSnapshotStore.RequirePrivatePath(path, directory);
            FileSystemSecurity security = directory ? new DirectoryInfo(path).GetAccessControl() : new FileInfo(path).GetAccessControl();
            if (security.GetOwner(typeof(SecurityIdentifier))?.Value != identity.User.Value) throw AuthOnlyTestingGuard.Refuse();
        }
    }

    internal static void ConfigureServices(IServiceCollection services)
    {
        if (!OperatingSystem.IsWindows()) throw AuthOnlyTestingGuard.Refuse();
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
            throw AuthOnlyTestingGuard.Refuse();
        services.AddDataProtection().SetApplicationName(AuthOnlyTestingGuard.Profile)
            .PersistKeysToFileSystem(new DirectoryInfo(AuthOnlyTestingGuard.DataProtectionDirectory)).ProtectKeysWithDpapi();
    }
}


