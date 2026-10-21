using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.EmployeeRecords;

public sealed class EmployeeAssetCustody : IEmployeeProfileRecord
{
    private EmployeeAssetCustody() { }

    public EmployeeAssetCustody(Guid employeeId, string assetReference, DateTimeOffset issuedAt, string issueCondition, string? issueEvidencePrivateReference, string recordedByUserId)
    {
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        AssetReference = ModelGuard.Required(assetReference, 100, nameof(assetReference));
        IssuedAt = ModelGuard.Utc(issuedAt);
        IssueCondition = ModelGuard.Required(issueCondition, 1000, nameof(issueCondition));
        IssueEvidencePrivateReference = ModelGuard.Optional(issueEvidencePrivateReference, 500, nameof(issueEvidencePrivateReference));
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string AssetReference { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; private set; }
    public string IssueCondition { get; private set; } = string.Empty;
    public string? IssueEvidencePrivateReference { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public Employee Employee { get; private set; } = null!;
    public byte[] RowVersion { get; private set; } = [];

    public DateTimeOffset? ReturnedAt { get; private set; }
    public string? ReturnCondition { get; private set; }
    public string? ReturnEvidencePrivateReference { get; private set; }
    public string? ReturnedToUserId { get; private set; }

    public void RecordReturn(DateTimeOffset returnedAt, string condition, string returnedToUserId, string? privateEvidenceReference)
    {
        if (ReturnedAt.HasValue || returnedAt < IssuedAt) throw new InvalidOperationException("Custody return must follow issue and can be recorded once.");
        var detail = ModelGuard.Required(condition, 1000, nameof(condition));
        var receiver = ModelGuard.Required(returnedToUserId, 200, nameof(returnedToUserId));
        var evidence = ModelGuard.Optional(privateEvidenceReference, 500, nameof(privateEvidenceReference));
        ReturnedAt = ModelGuard.Utc(returnedAt); ReturnCondition = detail;
        ReturnedToUserId = receiver; ReturnEvidencePrivateReference = evidence;
    }
}
