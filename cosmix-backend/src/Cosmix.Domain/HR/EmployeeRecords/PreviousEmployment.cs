using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.EmployeeRecords;

/// <summary>Last salary is a separately protected payload; never copied to ordinary employee responses.</summary>
public sealed class PreviousEmployment : IEmployeeProfileRecord
{
    private PreviousEmployment() { }

    public PreviousEmployment(Guid employeeId, string employerName, string roleTitle, DateOnly startedOn, DateOnly? endedOn, string? leavingReason, string? lastSalaryEncryptedReference, string recordedByUserId, DateTimeOffset recordedAt)
    {
        if (endedOn < startedOn) throw new ArgumentException("Employment end cannot precede its start.");
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        EmployerName = ModelGuard.Required(employerName, 200, nameof(employerName));
        RoleTitle = ModelGuard.Required(roleTitle, 150, nameof(roleTitle));
        StartedOn = startedOn;
        EndedOn = endedOn;
        LeavingReason = ModelGuard.Optional(leavingReason, 1000, nameof(leavingReason));
        LastSalaryEncryptedReference = ModelGuard.Optional(lastSalaryEncryptedReference, 500, nameof(lastSalaryEncryptedReference));
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string EmployerName { get; private set; } = string.Empty;
    public string RoleTitle { get; private set; } = string.Empty;
    public DateOnly StartedOn { get; private set; }
    public DateOnly? EndedOn { get; private set; }
    public string? LeavingReason { get; private set; }
    public string? LastSalaryEncryptedReference { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public byte[] RowVersion { get; private set; } = [];
}
