using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.EmployeeRecords;

public enum EmployeeAssetHistoryAction { Issued = 1, Returned = 2 }
public sealed class EmployeeAssetHistory : IAppendOnlyEntity, IEmployeeProfileRecord
{
    private EmployeeAssetHistory() { }
    public EmployeeAssetHistory(EmployeeAssetCustody custody, int revision, EmployeeAssetHistoryAction action,
        Guid documentId, Guid approvalRequestId, string reason, string actorId, DateTimeOffset recordedAt,
        Guid? receiverAccountId = null, Guid? transferredFromCustodyId = null)
    {
        ArgumentNullException.ThrowIfNull(custody);
        if (!Enum.IsDefined(action) || revision != (action == EmployeeAssetHistoryAction.Issued ? 1 : 2) ||
            (action == EmployeeAssetHistoryAction.Returned) != receiverAccountId.HasValue ||
            (action == EmployeeAssetHistoryAction.Returned) != custody.ReturnedAt.HasValue)
            throw new ArgumentException("History must match issue or single return.");
        Id = Guid.NewGuid(); EmployeeId = custody.EmployeeId; EmployeeAssetCustodyId = custody.Id;
        Revision = revision; Action = action; EvidenceDocumentId = ModelGuard.Id(documentId, nameof(documentId));
        ApprovalRequestId = ModelGuard.Id(approvalRequestId, nameof(approvalRequestId));
        Reason = ModelGuard.Required(reason, 1000, nameof(reason)); ActorUserId = ModelGuard.Required(actorId, 200, nameof(actorId));
        RecordedAt = ModelGuard.Utc(recordedAt);
        EffectiveAt = action == EmployeeAssetHistoryAction.Issued ? custody.IssuedAt : custody.ReturnedAt!.Value;
        if (RecordedAt < EffectiveAt) throw new ArgumentException("History cannot precede the event.");
        Condition = action == EmployeeAssetHistoryAction.Issued ? custody.IssueCondition : custody.ReturnCondition!;
        ReceiverAccountId = receiverAccountId.HasValue ? ModelGuard.Id(receiverAccountId.Value, nameof(receiverAccountId)) : null;
        if (ReceiverAccountId.HasValue && custody.ReturnedToUserId != ReceiverAccountId.Value.ToString()) throw new ArgumentException("Return history receiver must match custody.");
        TransferredFromCustodyId = transferredFromCustodyId.HasValue ? ModelGuard.Id(transferredFromCustodyId.Value, nameof(transferredFromCustodyId)) : null;
        if (TransferredFromCustodyId.HasValue && action != EmployeeAssetHistoryAction.Issued) throw new ArgumentException("Transfer linkage belongs to the receiving issue.");
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid EmployeeAssetCustodyId { get; private set; }
    public int Revision { get; private set; }
    public EmployeeAssetHistoryAction Action { get; private set; }
    public Guid EvidenceDocumentId { get; private set; }
    public Guid ApprovalRequestId { get; private set; }
    public string Condition { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string ActorUserId { get; private set; } = string.Empty;
    public DateTimeOffset EffectiveAt { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public Guid? ReceiverAccountId { get; private set; }
    public Guid? TransferredFromCustodyId { get; private set; }
}
