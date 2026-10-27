using Cosmix.Application.Common.Files;

namespace Cosmix.Application.Attendance.PunchRetention;

public sealed record PunchRetentionRunResult(
    int RecordsRedacted,
    int AuditPayloadsProcessed,
    DateTimeOffset RunAt,
    string Message);

public interface IPunchRetentionRepository
{
    /// <summary>Atomically clears coordinates and records one receipt per punch, serializing concurrent workers.</summary>
    Task<int> RedactBatchAsync(DateTimeOffset thresholdUtc, DateTimeOffset redactedAtUtc,
        int retentionDays, int batchSize, CancellationToken ct);
    Task<int> ExpireLegacyAuditBatchAsync(DateTimeOffset thresholdUtc, DateTimeOffset atUtc,
        int batchSize, CancellationToken ct);
}

/// <summary>Purpose-bound deletion of authenticated ciphertext for the legacy punch audit only.</summary>
public interface IPunchRetentionPrivateFiles
{
    Task<bool> DeleteAuthenticatedAsync(PrivateFileReference reference, string purpose, CancellationToken ct);
}
