using System.Runtime.Versioning;
using System.Security.AccessControl;
using Cosmix.Infrastructure.Authentication.Bootstrap;

namespace Cosmix.Infrastructure.Files;

internal static class PrivateFilePath
{
    public static StringComparison Comparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string Require(string path, bool directory)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            path.StartsWith("//", StringComparison.Ordinal) || path.StartsWith("\\\\", StringComparison.Ordinal) ||
            path.Split('/', '\\').Any(segment =>
                segment is "." or ".." || (OperatingSystem.IsWindows() && segment.Length > 0 &&
                    (segment.EndsWith('.') || char.IsWhiteSpace(segment[^1])))))
            throw new InvalidOperationException("Private paths must not contain traversal or ambiguous segments.");

        var normalized = Path.GetFullPath(path);
        if (normalized.StartsWith("\\\\", StringComparison.Ordinal) || normalized.StartsWith("//", StringComparison.Ordinal))
            throw new InvalidOperationException("Private paths must be local.");
        var full = PrivateBootstrapSnapshotStore.RequirePrivatePath(normalized, directory);
        if (OperatingSystem.IsWindows())
        {
            RejectNullDacl(full, directory);
            if (!directory) RejectNullDacl(Path.GetDirectoryName(full)!, true);
        }
        return full;
    }

    [SupportedOSPlatform("windows")]
    private static void RejectNullDacl(string path, bool directory)
    {
        FileSystemSecurity security = directory
            ? new DirectoryInfo(path).GetAccessControl() : new FileInfo(path).GetAccessControl();
        // An absent DACL grants everyone access but produces no Allow rules for the shared checker to inspect.
        var descriptor = new RawSecurityDescriptor(security.GetSecurityDescriptorBinaryForm(), 0);
        if (descriptor.DiscretionaryAcl is null)
            throw new InvalidOperationException("Private storage requires an explicit private access policy.");
    }
}
