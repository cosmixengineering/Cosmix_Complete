using Cosmix.Domain.Common;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.EmployeeRecords;

public enum EmployeeCertificationStatus { Active = 1, Suspended = 2, Revoked = 3 }

public sealed class EmployeeCertification : IAppendOnlyEntity, IEmployeeProfileRecord
{
    private EmployeeCertification() { }

    public EmployeeCertification(Guid employeeId, string certificateType, string certificateNumber, string issuer, DateOnly issuedOn, DateOnly? expiresOn, Guid? employeeDocumentId, string recordedByUserId, DateTimeOffset recordedAt,
        int revision = 1, Guid? supersedesEmployeeCertificationId = null, DateOnly? effectiveOn = null,
        EmployeeCertificationStatus status = EmployeeCertificationStatus.Active, Guid? approvalRequestId = null,
        string? reason = null, Guid? actionDocumentId = null)
    {
        if (expiresOn < issuedOn) throw new ArgumentException("Expiry cannot precede issue date.");
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        CertificateType = ModelGuard.Required(certificateType, 100, nameof(certificateType));
        CertificateNumber = ModelGuard.Required(certificateNumber, 100, nameof(certificateNumber));
        Issuer = ModelGuard.Required(issuer, 200, nameof(issuer));
        IssuedOn = issuedOn;
        ExpiresOn = expiresOn;
        EmployeeDocumentId = employeeDocumentId.HasValue ? ModelGuard.Id(employeeDocumentId.Value, nameof(employeeDocumentId)) : null;
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
        Revision = HrModelGuard.Positive(revision, nameof(revision));
        if ((revision == 1) != !supersedesEmployeeCertificationId.HasValue) throw new ArgumentException("Successor revision requires a predecessor.");
        SupersedesEmployeeCertificationId = supersedesEmployeeCertificationId.HasValue ? ModelGuard.Id(supersedesEmployeeCertificationId.Value, nameof(supersedesEmployeeCertificationId)) : null;
        EffectiveOn = effectiveOn ?? issuedOn;
        if (EffectiveOn < issuedOn || !Enum.IsDefined(status)) throw new ArgumentException("Certification state/date is invalid.");
        Status = status;
        ApprovalRequestId = approvalRequestId.HasValue ? ModelGuard.Id(approvalRequestId.Value, nameof(approvalRequestId)) : null;
        ActionDocumentId = actionDocumentId.HasValue ? ModelGuard.Id(actionDocumentId.Value, nameof(actionDocumentId)) : null;
        Reason = ModelGuard.Optional(reason, 1000, nameof(reason));
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string CertificateType { get; private set; } = string.Empty;
    public string CertificateNumber { get; private set; } = string.Empty;
    public string Issuer { get; private set; } = string.Empty;
    public DateOnly IssuedOn { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public Guid? EmployeeDocumentId { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public EmployeeDocument? EmployeeDocument { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public int Revision { get; private set; }
    public Guid? SupersedesEmployeeCertificationId { get; private set; }
    public DateOnly EffectiveOn { get; private set; }
    public EmployeeCertificationStatus Status { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public Guid? ActionDocumentId { get; private set; }
    public string? Reason { get; private set; }
}
