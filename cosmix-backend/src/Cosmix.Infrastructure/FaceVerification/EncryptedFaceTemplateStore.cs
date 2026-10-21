using System.Security.Cryptography;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.Files;

namespace Cosmix.Infrastructure.FaceVerification;

public sealed class FaceTemplateStorageOptions
{
    public const string SectionName = "FaceTemplateStorage";
    public bool Enabled { get; set; }
    public PrivateFileStorageOptions Storage { get; set; } = new();
}

/// <summary>Reuses authenticated encryption with an independently provisioned biometric directory/keyring.
/// Missing/overlapping configuration never falls back to HR documents or ordinary audit storage.</summary>
public sealed class EncryptedFaceTemplateStore : IFaceTemplateStore
{
    private readonly EncryptedPrivateFileStore? inner;
    public bool IsConfigured => inner is not null;
    public EncryptedFaceTemplateStore(FaceTemplateStorageOptions options, PrivateFileStorageOptions ordinary)
    {
        if (!options.Enabled) return;
        try
        {
            var dedicated = options.Storage;
            var faceDirectory = Path.GetFullPath(dedicated.Directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalDirectory = Path.GetFullPath(ordinary.Directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(dedicated.Directory) || string.IsNullOrWhiteSpace(ordinary.Directory) ||
                Within(faceDirectory, normalDirectory) || Within(normalDirectory, faceDirectory)) return;
            var faceKeys = dedicated.DecryptionKeyFiles.Append(new(dedicated.KeyReference, dedicated.KeyFile)).ToArray();
            var normalKeys = ordinary.DecryptionKeyFiles.Append(new(ordinary.KeyReference, ordinary.KeyFile)).ToArray();
            foreach (var faceKey in faceKeys)
            foreach (var ordinaryKey in normalKeys)
            {
                if (faceKey.Key == ordinaryKey.Key || string.Equals(Path.GetFullPath(faceKey.Value), Path.GetFullPath(ordinaryKey.Value), StringComparison.OrdinalIgnoreCase)) return;
                // Different filenames containing the same material are not independent keys.
                byte[]? faceBytes = null, ordinaryBytes = null;
                try
                {
                    if (new FileInfo(faceKey.Value).Length != 32 || new FileInfo(ordinaryKey.Value).Length != 32) return;
                    faceBytes = File.ReadAllBytes(faceKey.Value); ordinaryBytes = File.ReadAllBytes(ordinaryKey.Value);
                    if (CryptographicOperations.FixedTimeEquals(faceBytes, ordinaryBytes)) return;
                }
                finally
                {
                    if (faceBytes is not null) CryptographicOperations.ZeroMemory(faceBytes);
                    if (ordinaryBytes is not null) CryptographicOperations.ZeroMemory(ordinaryBytes);
                }
            }
            inner = new EncryptedPrivateFileStore(dedicated);
        }
        catch (Exception) { inner = null; }
    }
    public Task<PrivateFileReference> WriteAsync(byte[] template, string purpose, CancellationToken ct)
    { Require(purpose); return inner!.WriteAsync(template, purpose, ct); }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    { Require(purpose); return inner!.ReadAsync(reference, purpose, ct); }
    private void Require(string purpose)
    {
        if (!IsConfigured || !purpose.StartsWith("attendance:face:template:", StringComparison.Ordinal))
            throw new AuthenticationFailure("face_template_storage_unavailable", 503);
    }
    private static bool Within(string path, string root) => path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
