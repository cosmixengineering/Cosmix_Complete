using Cosmix.Domain.HR.EmployeeDocuments;

namespace Cosmix.Application.Common.Files;

/// <summary>Internal metadata only. Sha256 and SizeBytes describe plaintext; never expose this reference in client DTOs.</summary>
public sealed record PrivateFileReference(string StorageKey, string ObjectVersion, string Sha256, string KeyReference, long SizeBytes);

/// <summary>Purpose must be server-selected and stable. Authorization and clean-document availability belong to the caller.</summary>
public interface IPrivateFileStore
{
    Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct);
    Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct);
}

public interface IEmployeeFileScanner
{
    Task<FileScanResult> ScanAsync(byte[] content, CancellationToken ct);
}

public sealed record FileScanResult(DocumentScanStatus State, string ScannerVersion);
