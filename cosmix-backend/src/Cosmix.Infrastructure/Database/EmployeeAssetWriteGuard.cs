using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.EmployeeRecords;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Database;

internal static class EmployeeAssetWriteGuard
{
    public static void Validate(CosmixDbContext db)
    {
        var events = db.ChangeTracker.Entries<EmployeeAssetHistory>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToArray();
        var rows = db.ChangeTracker.Entries<EmployeeAssetCustody>().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        foreach (var entry in rows)
        {
            if (entry.State == EntityState.Deleted) throw new InvalidOperationException("Asset custody cannot be deleted.");
            if (entry.State != EntityState.Modified) continue;
            string[] allowed = [nameof(EmployeeAssetCustody.ReturnedAt), nameof(EmployeeAssetCustody.ReturnCondition),
                nameof(EmployeeAssetCustody.ReturnedToUserId), nameof(EmployeeAssetCustody.ReturnEvidencePrivateReference), nameof(EmployeeAssetCustody.RowVersion)];
            if (entry.OriginalValues.GetValue<DateTimeOffset?>(nameof(EmployeeAssetCustody.ReturnedAt)).HasValue ||
                !entry.Entity.ReturnedAt.HasValue || entry.Properties.Any(p => p.IsModified && !allowed.Contains(p.Metadata.Name)) ||
                events.Count(h => h.EmployeeAssetCustodyId == entry.Entity.Id && h.Action == EmployeeAssetHistoryAction.Returned) != 1)
                throw new InvalidOperationException("Only one evidenced return may close immutable custody.");
        }
        foreach (var history in events)
        {
            var entry = rows.SingleOrDefault(e => e.Entity.Id == history.EmployeeAssetCustodyId && e.Entity.EmployeeId == history.EmployeeId);
            if (entry is null || history.Action == EmployeeAssetHistoryAction.Issued && entry.State != EntityState.Added ||
                history.Action == EmployeeAssetHistoryAction.Returned && entry.State != EntityState.Modified)
                throw new InvalidOperationException("Custody history must be paired with its exact issue/return mutation.");
            var custody = entry.Entity;
            if (history.Condition != (history.Action == EmployeeAssetHistoryAction.Issued ? custody.IssueCondition : custody.ReturnCondition) ||
                history.EffectiveAt != (history.Action == EmployeeAssetHistoryAction.Issued ? custody.IssuedAt : custody.ReturnedAt) ||
                history.Action == EmployeeAssetHistoryAction.Returned && history.ReceiverAccountId?.ToString() != custody.ReturnedToUserId)
                throw new InvalidOperationException("Custody history must match condition, effective time and receiver.");
            var request = db.ChangeTracker.Entries<ApprovalRequest>().Select(e => e.Entity).SingleOrDefault(r => r.Id == history.ApprovalRequestId);
            var decision = db.ChangeTracker.Entries<ApprovalDecision>().Select(e => e.Entity).SingleOrDefault(d =>
                d.ApprovalRequestId == history.ApprovalRequestId && d.Decision == ApprovalState.Approved);
            var document = db.ChangeTracker.Entries<EmployeeDocument>().Select(e => e.Entity).SingleOrDefault(d => d.Id == history.EvidenceDocumentId && d.EmployeeId == history.EmployeeId);
            if (db.Database.CurrentTransaction is null || request?.State != ApprovalState.Approved || request.RequestType != "EmployeeRecord.Asset" ||
                decision is null || decision.ActorUserId != history.ActorUserId || decision.DecidedAt != history.RecordedAt ||
                document is null || document.Privacy != EmployeeDocumentPrivacy.Standard || !document.IsAvailable ||
                request.SubjectId != history.EmployeeId && !history.TransferredFromCustodyId.HasValue)
                throw new InvalidOperationException("Operational custody requires approved request and verified employee evidence in one transaction.");
            if (history.TransferredFromCustodyId.HasValue)
            {
                var source = db.ChangeTracker.Entries<EmployeeAssetCustody>().Select(e => e.Entity).SingleOrDefault(c => c.Id == history.TransferredFromCustodyId);
                if (source is null || source.EmployeeId != request.SubjectId || source.AssetReference != custody.AssetReference ||
                    source.ReturnedAt != custody.IssuedAt || source.EmployeeId == custody.EmployeeId)
                    throw new InvalidOperationException("Transfer history must link the exact returned source and receiving custody.");
            }
        }
    }
}
