using Cosmix.Domain.Common;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.EmployeeRecords;

/// <summary>Authorized medical/emergency information only; clinical contents are encrypted outside normal profile fields.</summary>
public sealed class EmployeeMedicalRecord : IAppendOnlyEntity, IEmployeeProfileRecord
{
    private EmployeeMedicalRecord() { }

    public EmployeeMedicalRecord(Guid employeeId, string encryptedPayloadReference, string encryptionKeyReference, DateOnly effectiveOn, Guid? employeeDocumentId, string recordedByUserId, DateTimeOffset recordedAt)
    {
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        EncryptedPayloadReference = ModelGuard.Required(encryptedPayloadReference, 500, nameof(encryptedPayloadReference));
        EncryptionKeyReference = ModelGuard.Required(encryptionKeyReference, 200, nameof(encryptionKeyReference));
        EffectiveOn = effectiveOn;
        EmployeeDocumentId = employeeDocumentId.HasValue ? ModelGuard.Id(employeeDocumentId.Value, nameof(employeeDocumentId)) : null;
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string EncryptedPayloadReference { get; private set; } = string.Empty;
    public string EncryptionKeyReference { get; private set; } = string.Empty;
    public DateOnly EffectiveOn { get; private set; }
    public Guid? EmployeeDocumentId { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public EmployeeDocument? EmployeeDocument { get; private set; }
}
