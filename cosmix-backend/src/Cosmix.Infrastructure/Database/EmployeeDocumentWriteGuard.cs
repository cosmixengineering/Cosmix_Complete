using System.Text.Json;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Cosmix.Infrastructure.Database;

/// <summary>Withdrawal may invalidate a submitted document, while immutable upload metadata and the profile remain frozen.</summary>
internal static class EmployeeDocumentWriteGuard
{
    public static void Validate(CosmixDbContext db)
    {
        var history = db.ChangeTracker.Entries<EmployeeDocumentHistory>().Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity).ToArray();
        var documents = db.ChangeTracker.Entries<EmployeeDocument>().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        var parents = db.ChangeTracker.Entries<Employee>().Where(e => e.State != EntityState.Deleted).Select(e => e.Entity.Id).ToHashSet();
        foreach (var entry in documents)
        {
            if (entry.State == EntityState.Deleted || !parents.Contains(entry.Entity.EmployeeId))
                throw new InvalidOperationException("Private document writes require a retained, tracked employee; deletion is prohibited.");
            string[] immutable = [nameof(EmployeeDocument.Id), nameof(EmployeeDocument.EmployeeId), nameof(EmployeeDocument.DocumentType),
                nameof(EmployeeDocument.Privacy), nameof(EmployeeDocument.StorageKey), nameof(EmployeeDocument.OriginalFileName),
                nameof(EmployeeDocument.ContentType), nameof(EmployeeDocument.SizeBytes), nameof(EmployeeDocument.Sha256),
                nameof(EmployeeDocument.UploadedByUserId), nameof(EmployeeDocument.UploadedAt), nameof(EmployeeDocument.IssuedOn), nameof(EmployeeDocument.ExpiresOn)];
            if (entry.State == EntityState.Modified && entry.Properties.Any(p => p.IsModified && immutable.Contains(p.Metadata.Name)))
                throw new InvalidOperationException("Uploaded document identity, privacy, dates and private object metadata are immutable.");
            var beforeRevision = entry.State == EntityState.Added ? 0 : entry.OriginalValues.GetValue<int>(nameof(EmployeeDocument.HistoryRevision));
            var events = history.Where(h => h.EmployeeDocumentId == entry.Entity.Id && h.EmployeeId == entry.Entity.EmployeeId).OrderBy(h => h.Revision).ToArray();
            if (events.Length == 0 || entry.Entity.HistoryRevision != beforeRevision + events.Length ||
                events.Where((h, i) => h.Revision != beforeRevision + i + 1).Any())
                throw new InvalidOperationException("Every document mutation requires ordered history in the same transaction.");
            var expectedBefore = entry.State == EntityState.Added ? "{}" : Original(entry).Encode();
            foreach (var item in events)
            {
                if (item.BeforeState != expectedBefore || !ActionMatches(item, entry.State == EntityState.Added))
                    throw new InvalidOperationException("Document history must retain the exact before/after transition.");
                expectedBefore = item.AfterState;
            }
            if (expectedBefore != EmployeeDocumentState.Capture(entry.Entity).Encode())
                throw new InvalidOperationException("Document history must match its committed state.");
        }
        if (history.Any(h => !documents.Any(d => d.Entity.Id == h.EmployeeDocumentId && d.Entity.EmployeeId == h.EmployeeId)))
            throw new InvalidOperationException("Document history cannot be appended without its matching document mutation.");
    }

    private static bool ActionMatches(EmployeeDocumentHistory h, bool newlyUploaded)
    {
        var after = JsonSerializer.Deserialize<EmployeeDocumentState>(h.AfterState);
        var before = h.BeforeState == "{}" ? null : JsonSerializer.Deserialize<EmployeeDocumentState>(h.BeforeState);
        if (after is null) return false;
        return h.Action switch
        {
            EmployeeDocumentHistoryAction.Uploaded => newlyUploaded && before is null && after.ScanStatus == DocumentScanStatus.Pending &&
                after.LifecycleStatus == EmployeeDocumentLifecycleStatus.Current,
            EmployeeDocumentHistoryAction.ScanStarted or EmployeeDocumentHistoryAction.ScanRetried => before is not null &&
                before.LifecycleStatus == EmployeeDocumentLifecycleStatus.Current && after.LifecycleStatus == EmployeeDocumentLifecycleStatus.Current &&
                after.IsScanInProgress && after.ActiveScanAttemptId.HasValue && after.ActiveScanAttemptId != before.ActiveScanAttemptId &&
                (h.Action == EmployeeDocumentHistoryAction.ScanRetried ? before.IsScanInProgress : !before.IsScanInProgress) &&
                after.VerificationStatus == DocumentVerificationStatus.Pending &&
                after.ScanStartedAt.HasValue && after.ScanStartedAt <= h.RecordedAt &&
                after.VerifiedAt is null && after.VerifiedByUserId is null && after.VerifiedScanAt is null && after.VerificationReason is null &&
                (after with { IsScanInProgress = before.IsScanInProgress, ActiveScanAttemptId = before.ActiveScanAttemptId,
                    ScanStartedAt = before.ScanStartedAt, VerificationStatus = before.VerificationStatus, VerifiedAt = before.VerifiedAt,
                    VerifiedByUserId = before.VerifiedByUserId, VerifiedScanAt = before.VerifiedScanAt, VerificationReason = before.VerificationReason }) == before,
            EmployeeDocumentHistoryAction.ScanCompleted => before is not null && after.LifecycleStatus == EmployeeDocumentLifecycleStatus.Current &&
                !after.IsScanInProgress && after.ScanStatus != DocumentScanStatus.Pending &&
                after.ScannedAt.HasValue && after.ScannedAt <= h.RecordedAt && !string.IsNullOrWhiteSpace(after.ScannerVersion) &&
                (!before.ScannedAt.HasValue || after.ScannedAt >= before.ScannedAt) &&
                (newlyUploaded || (before.IsScanInProgress && before.ActiveScanAttemptId == after.ActiveScanAttemptId)) &&
                (after with { ScanStatus = before.ScanStatus, SignatureVerified = before.SignatureVerified, HasScanConflict = before.HasScanConflict,
                    ScannedAt = before.ScannedAt, ScannerVersion = before.ScannerVersion, IsScanInProgress = before.IsScanInProgress }) == before,
            EmployeeDocumentHistoryAction.Verified => before is not null && after.VerificationStatus == DocumentVerificationStatus.Verified &&
                after.ScanStatus == DocumentScanStatus.Clean && after.SignatureVerified && !after.HasScanConflict && after.ScannedAt.HasValue &&
                !after.IsScanInProgress && after.LifecycleStatus == EmployeeDocumentLifecycleStatus.Current && VerificationMatches(before, after, h),
            EmployeeDocumentHistoryAction.VerificationRejected => before is not null && after.VerificationStatus == DocumentVerificationStatus.Rejected &&
                !after.IsScanInProgress && after.LifecycleStatus == EmployeeDocumentLifecycleStatus.Current && VerificationMatches(before, after, h),
            EmployeeDocumentHistoryAction.Expired => before is not null && before.LifecycleStatus == EmployeeDocumentLifecycleStatus.Current &&
                after.LifecycleStatus == EmployeeDocumentLifecycleStatus.Expired && WithdrawalMatches(before, after, h),
            EmployeeDocumentHistoryAction.Revoked => before is not null && before.LifecycleStatus == EmployeeDocumentLifecycleStatus.Current &&
                after.LifecycleStatus == EmployeeDocumentLifecycleStatus.Revoked && WithdrawalMatches(before, after, h),
            EmployeeDocumentHistoryAction.Archived => before is not null && before.LifecycleStatus != EmployeeDocumentLifecycleStatus.Archived &&
                after.LifecycleStatus == EmployeeDocumentLifecycleStatus.Archived && WithdrawalMatches(before, after, h),
            _ => false
        };
    }

    private static bool WithdrawalMatches(EmployeeDocumentState before, EmployeeDocumentState after, EmployeeDocumentHistory h) =>
        after.LifecycleChangedAt.HasValue && after.LifecycleChangedAt <= h.RecordedAt &&
        after.LifecycleChangedByUserId == h.ActorUserId && after.LifecycleReason == h.Reason &&
        (after with { LifecycleStatus = before.LifecycleStatus, LifecycleChangedAt = before.LifecycleChangedAt,
            LifecycleChangedByUserId = before.LifecycleChangedByUserId, LifecycleReason = before.LifecycleReason }) == before;

    private static bool VerificationMatches(EmployeeDocumentState before, EmployeeDocumentState after, EmployeeDocumentHistory h) =>
        after.VerifiedAt.HasValue && after.VerifiedAt <= h.RecordedAt && after.VerifiedByUserId == h.ActorUserId &&
        after.VerifiedScanAt == after.ScannedAt && (!after.ScannedAt.HasValue || after.VerifiedAt >= after.ScannedAt) &&
        (h.Action != EmployeeDocumentHistoryAction.VerificationRejected || after.VerificationReason == h.Reason) &&
        (after with { VerificationStatus = before.VerificationStatus, VerifiedAt = before.VerifiedAt,
            VerifiedScanAt = before.VerifiedScanAt, VerifiedByUserId = before.VerifiedByUserId, VerificationReason = before.VerificationReason }) == before;

    private static EmployeeDocumentState Original(EntityEntry<EmployeeDocument> entry)
    {
        var p = entry.OriginalValues;
        return new(p.GetValue<EmployeeDocumentLifecycleStatus>(nameof(EmployeeDocument.LifecycleStatus)),
            p.GetValue<DocumentScanStatus>(nameof(EmployeeDocument.ScanStatus)), p.GetValue<DocumentVerificationStatus>(nameof(EmployeeDocument.VerificationStatus)),
            p.GetValue<bool>(nameof(EmployeeDocument.SignatureVerified)), p.GetValue<bool>(nameof(EmployeeDocument.HasScanConflict)),
            p.GetValue<bool>(nameof(EmployeeDocument.IsScanInProgress)), p.GetValue<Guid?>(nameof(EmployeeDocument.ActiveScanAttemptId)),
            p.GetValue<DateTimeOffset?>(nameof(EmployeeDocument.ScanStartedAt)), p.GetValue<DateTimeOffset?>(nameof(EmployeeDocument.ScannedAt)),
            p.GetValue<string?>(nameof(EmployeeDocument.ScannerVersion)), p.GetValue<DateTimeOffset?>(nameof(EmployeeDocument.VerifiedAt)),
            p.GetValue<DateTimeOffset?>(nameof(EmployeeDocument.VerifiedScanAt)), p.GetValue<string?>(nameof(EmployeeDocument.VerifiedByUserId)),
            p.GetValue<string?>(nameof(EmployeeDocument.VerificationReason)), p.GetValue<DateTimeOffset?>(nameof(EmployeeDocument.LifecycleChangedAt)),
            p.GetValue<string?>(nameof(EmployeeDocument.LifecycleChangedByUserId)), p.GetValue<string?>(nameof(EmployeeDocument.LifecycleReason)));
    }
}
