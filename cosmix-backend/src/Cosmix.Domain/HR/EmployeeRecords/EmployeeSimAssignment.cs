using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.EmployeeRecords;

/// <summary>SIM state changes append a successor revision, preserving the original issue and earlier status evidence.</summary>
public sealed class EmployeeSimAssignment : IAppendOnlyEntity, IEmployeeProfileRecord
{
    private EmployeeSimAssignment() { }

    public EmployeeSimAssignment(Guid employeeId, string phoneNumber, string simReference, string allowedCreditTierCode, CompanySimStatus status, DateOnly issuedOn, DateOnly? returnedOn, string recordedByUserId, DateTimeOffset recordedAt, int revision = 1, Guid? supersedesEmployeeSimAssignmentId = null,
        DateOnly? effectiveOn = null, Guid? issueDocumentId = null, Guid? returnDocumentId = null,
        Guid? returnedToAccountId = null, Guid? approvalRequestId = null, string? reason = null, Guid? transferredFromAssignmentId = null)
    {
        if (returnedOn < issuedOn || (status == CompanySimStatus.Returned) != returnedOn.HasValue) throw new ArgumentException("SIM return date must follow issue and match returned status.");
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        Revision = HrModelGuard.Positive(revision, nameof(revision));
        if ((revision == 1 && supersedesEmployeeSimAssignmentId.HasValue) || (revision > 1 && !supersedesEmployeeSimAssignmentId.HasValue))
            throw new ArgumentException("An initial revision has no predecessor; later revisions require one.", nameof(supersedesEmployeeSimAssignmentId));
        SupersedesEmployeeSimAssignmentId = supersedesEmployeeSimAssignmentId.HasValue
            ? ModelGuard.Id(supersedesEmployeeSimAssignmentId.Value, nameof(supersedesEmployeeSimAssignmentId)) : null;
        PhoneNumber = ModelGuard.Required(phoneNumber, 30, nameof(phoneNumber));
        SimReference = ModelGuard.Required(simReference, 100, nameof(simReference));
        AllowedCreditTierCode = ModelGuard.Required(allowedCreditTierCode, 40, nameof(allowedCreditTierCode));
        Status = Enum.IsDefined(status) ? status : throw new ArgumentOutOfRangeException(nameof(status));
        IssuedOn = issuedOn;
        ReturnedOn = returnedOn;
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
        EffectiveOn = effectiveOn ?? returnedOn ?? issuedOn;
        if (EffectiveOn < issuedOn) throw new ArgumentException("Effective date cannot precede issue.");
        IssueDocumentId = OptionalId(issueDocumentId); ReturnDocumentId = OptionalId(returnDocumentId);
        ReturnedToAccountId = OptionalId(returnedToAccountId); ApprovalRequestId = OptionalId(approvalRequestId);
        TransferredFromAssignmentId = OptionalId(transferredFromAssignmentId);
        Reason = ModelGuard.Optional(reason, 1000, nameof(reason));
        if (returnDocumentId.HasValue != returnedToAccountId.HasValue || (returnDocumentId.HasValue && status != CompanySimStatus.Returned))
            throw new ArgumentException("Return evidence requires a returned SIM and receiver.");
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public int Revision { get; private set; }
    public Guid? SupersedesEmployeeSimAssignmentId { get; private set; }
    public string PhoneNumber { get; private set; } = string.Empty;
    public string SimReference { get; private set; } = string.Empty;
    public string AllowedCreditTierCode { get; private set; } = string.Empty;
    public CompanySimStatus Status { get; private set; }
    public DateOnly IssuedOn { get; private set; }
    public DateOnly? ReturnedOn { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public EmployeeSimAssignment? SupersedesEmployeeSimAssignment { get; private set; }
    public DateOnly EffectiveOn { get; private set; }
    public Guid? IssueDocumentId { get; private set; }
    public Guid? ReturnDocumentId { get; private set; }
    public Guid? ReturnedToAccountId { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public Guid? TransferredFromAssignmentId { get; private set; }
    public string? Reason { get; private set; }
    private static Guid? OptionalId(Guid? id) => id.HasValue ? ModelGuard.Id(id.Value, nameof(id)) : null;
}
