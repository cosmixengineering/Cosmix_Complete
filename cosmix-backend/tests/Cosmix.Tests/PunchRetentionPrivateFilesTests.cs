using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Text;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.Files;
using Xunit;

namespace Cosmix.Tests;

public sealed class PunchRetentionPrivateFilesTests
{
    [Fact]
    public async Task Delete_RequiresAuthenticatedOriginalPurpose_AndIsRetrySafe()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "cosmix-retention-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            Restrict(root);
            var objects = Directory.CreateDirectory(Path.Combine(root, "objects")).FullName;
            Restrict(objects);
            var keys = Directory.CreateDirectory(Path.Combine(root, "keys")).FullName;
            Restrict(keys);
            var key = Path.Combine(keys, "key.bin");
            await File.WriteAllBytesAsync(key, RandomNumberGenerator.GetBytes(32));
            RestrictFile(key);
            var store = new EncryptedPrivateFileStore(new PrivateFileStorageOptions
            {
                Directory = objects, KeyFile = key, KeyReference = "test-key", MaxSizeBytes = 4096
            });
            const string purpose = "attendance:operation:0123456789abcdef0123456789abcdef:attendance.punch.submit:audit";
            var original = Encoding.UTF8.GetBytes("{\"operation\":\"attendance.punch.submit\",\"Latitude\":24.1}");
            var reference = await store.WriteAsync(original, purpose, CancellationToken.None);
            await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAuthenticatedAsync(reference,
                "attendance:operation:0123456789abcdef0123456789abcdef:payroll.accounting:audit", CancellationToken.None));
            await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAuthenticatedAsync(reference,
                "attendance:operation:11111111111111111111111111111111:attendance.punch.submit:audit", CancellationToken.None));
            Assert.Equal(original, await store.ReadAsync(reference, purpose, CancellationToken.None));
            var tampered = reference with { Sha256 = new string('A', 64) };
            await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAuthenticatedAsync(tampered, purpose, CancellationToken.None));
            Assert.Equal(original, await store.ReadAsync(reference, purpose, CancellationToken.None));
            Assert.True(await store.DeleteAuthenticatedAsync(reference, purpose, CancellationToken.None));
            Assert.False(await store.DeleteAuthenticatedAsync(reference, purpose, CancellationToken.None));
            await Assert.ThrowsAnyAsync<Exception>(() => store.ReadAsync(reference, purpose, CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(root, true);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static void Restrict(string path)
    {
        var current = WindowsIdentity.GetCurrent().User!;
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(current, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(security);
    }

    [SupportedOSPlatform("windows")]
    private static void RestrictFile(string path)
    {
        var current = WindowsIdentity.GetCurrent().User!;
        var security = new FileSecurity();
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(current, FileSystemRights.FullControl,
            AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }
}
