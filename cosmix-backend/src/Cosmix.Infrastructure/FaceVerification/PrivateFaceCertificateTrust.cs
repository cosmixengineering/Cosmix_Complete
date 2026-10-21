using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Cosmix.Application.Authentication;

namespace Cosmix.Infrastructure.FaceVerification;

/// <summary>Explicit per-client private PKI trust; never changes machine/user trust or hostname checks.</summary>
internal static class PrivateFaceCertificateTrust
{
    internal static void ValidateOptions(CompreFaceOptions options)
    {
        var path = options.TrustedRootCertificatePath;
        var hash = options.TrustedRootCertificateSha256;
        if (path is null && hash is null) return;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal) ||
            hash?.Length != 64 || !hash.All(Uri.IsHexDigit)) throw Unavailable();
        if (OperatingSystem.IsWindows() && Path.GetFullPath(path)[2..].Contains(':')) throw Unavailable();
    }

    internal static X509ChainPolicy? LoadPolicy(CompreFaceOptions options)
    {
        ValidateOptions(options);
        if (options.TrustedRootCertificatePath is not string path) return null;
        try
        {
            for (FileSystemInfo? entry = new FileInfo(Path.GetFullPath(path)); entry is not null;
                entry = entry is FileInfo file ? file.Directory : ((DirectoryInfo)entry).Parent)
                if (!entry.Exists || (entry.Attributes & FileAttributes.ReparsePoint) != 0) throw Unavailable();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > 65536) throw Unavailable();
            var bytes = new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1 || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes),
                Convert.FromHexString(options.TrustedRootCertificateSha256!))) throw Unavailable();
            var certificate = X509Certificate2.CreateFromPem(Encoding.UTF8.GetString(bytes));
            if (!certificate.Extensions.OfType<X509BasicConstraintsExtension>().Any(e => e.CertificateAuthority))
            { certificate.Dispose(); throw Unavailable(); }
            var policy = new X509ChainPolicy
            {
                TrustMode = X509ChainTrustMode.CustomRootTrust,
                VerificationFlags = X509VerificationFlags.NoFlag,
                // This isolated private CA has no public revocation distribution point.
                RevocationMode = X509RevocationMode.NoCheck
            };
            policy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
            policy.CustomTrustStore.Add(certificate);
            return policy;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException)
        { throw Unavailable(); }
    }

    private static AuthenticationFailure Unavailable() => new("face_provider_not_configured", 503);
}
