using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.EmployeeDocuments;

public enum EmployeeDocumentHistoryAction
{
    Uploaded = 1, ScanStarted = 2, ScanCompleted = 3, Verified = 4, VerificationRejected = 5,
    Expired = 6, Revoked = 7, Archived = 8, ScanRetried = 9
}

/// <summary>Append-only metadata evidence. Private object references and plaintext never belong in this journal.</summary>
public sealed class EmployeeDocumentHistory : IAppendOnlyEntity
{
    private EmployeeDocumentHistory() { }
    internal EmployeeDocumentHistory(EmployeeDocument document, int revision, EmployeeDocumentHistoryAction action,
        string actorUserId, string reason, DateTimeOffset at, string beforeState, string afterState)
    {
        if (!Enum.IsDefined(action) || revision < 1 || at < document.UploadedAt)
            throw new ArgumentException("Valid ordered document history is required.");
        Id = Guid.NewGuid(); EmployeeId = document.EmployeeId; EmployeeDocumentId = document.Id;
        Revision = revision; Action = action;
        ActorUserId = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        Reason = ModelGuard.Required(reason, 1000, nameof(reason));
        if (Reason.Any(char.IsControl)) throw new ArgumentException("A printable reason is required.", nameof(reason));
        RecordedAt = ModelGuard.Utc(at); ScanAttemptId = document.ActiveScanAttemptId;
        // Two 1000-character reasons plus retained actors/scanner may expand sixfold under JSON escaping.
        BeforeState = ModelGuard.Required(beforeState, 20000, nameof(beforeState));
        AfterState = ModelGuard.Required(afterState, 20000, nameof(afterState));
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid EmployeeDocumentId { get; private set; }
    public int Revision { get; private set; }
    public EmployeeDocumentHistoryAction Action { get; private set; }
    public string ActorUserId { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public Guid? ScanAttemptId { get; private set; }
    public string BeforeState { get; private set; } = string.Empty;
    public string AfterState { get; private set; } = string.Empty;
}
