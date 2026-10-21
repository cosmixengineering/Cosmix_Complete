using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Employees;

/// <summary>Append-only archive/unarchive evidence. Employee identity and all referenced records remain intact.</summary>
public sealed class EmployeeArchiveChange : IAppendOnlyEntity
{
    private EmployeeArchiveChange() { }

    public EmployeeArchiveChange(Guid employeeId, bool previousArchived, bool archived, EmployeeStatus employmentStatus,
        string reason, string actorUserId, string actorRoleCode, DateTimeOffset recordedAt)
    {
        if (previousArchived == archived || employmentStatus is not (EmployeeStatus.Resigned or EmployeeStatus.Terminated or EmployeeStatus.Retired))
            throw new ArgumentException("Archival requires an ended employment and a changed archive state.");
        Id = Guid.NewGuid(); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        PreviousArchived = previousArchived; IsArchived = archived; EmploymentStatus = employmentStatus;
        Reason = ModelGuard.Required(reason, 1000, nameof(reason));
        ActorUserId = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        ActorRoleCode = ModelGuard.Required(actorRoleCode, 100, nameof(actorRoleCode));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public bool PreviousArchived { get; private set; }
    public bool IsArchived { get; private set; }
    public EmployeeStatus EmploymentStatus { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string ActorUserId { get; private set; } = string.Empty;
    public string ActorRoleCode { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
}
