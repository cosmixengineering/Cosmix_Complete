using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Employees;

public enum EmployeeChangeSection
{
    Basic = 1,
    Identity = 2,
    Employment = 3,
    Bank = 4,
    Medical = 5,
    Documents = 6
}

/// <summary>
/// Immutable profile history. Before/after values are serialized references to encrypted private snapshots,
/// never sensitive payloads. Application verifies approval authority and snapshot ownership before recording.
/// </summary>
public sealed class EmployeeChange : IAppendOnlyEntity, IEmployeeProfileRecord
{
    private EmployeeChange() { }

    public EmployeeChange(Guid employeeId, EmployeeChangeSection section, DateOnly effectiveFrom,
        Guid? approvalRequestId, string beforePrivateReference, string afterPrivateReference,
        string reason, string recordedByUserId, DateTimeOffset recordedAt, int revision = 1)
    {
        if (!Enum.IsDefined(section))
            throw new ArgumentOutOfRangeException(nameof(section));
        if (section != EmployeeChangeSection.Basic && !approvalRequestId.HasValue)
            throw new ArgumentException("This employee change section requires an approval request.", nameof(approvalRequestId));
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        Section = section;
        Revision = revision;
        EffectiveFrom = effectiveFrom;
        ApprovalRequestId = approvalRequestId.HasValue ? ModelGuard.Id(approvalRequestId.Value, nameof(approvalRequestId)) : null;
        BeforePrivateReference = ModelGuard.Required(beforePrivateReference, 1000, nameof(beforePrivateReference));
        AfterPrivateReference = ModelGuard.Required(afterPrivateReference, 1000, nameof(afterPrivateReference));
        Reason = ModelGuard.Required(reason, 1000, nameof(reason));
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public EmployeeChangeSection Section { get; private set; }
    public int Revision { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public string BeforePrivateReference { get; private set; } = string.Empty;
    public string AfterPrivateReference { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
}
