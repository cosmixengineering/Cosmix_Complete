using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Domain.HR.Leaves;

/// <summary>Balance is derived from an append-only ledger. Reversals reference an original entry; approved policy defines reservation/usage semantics.</summary>
public sealed class LeaveLedgerEntry : IAppendOnlyEntity
{
    private LeaveLedgerEntry() { }

    public LeaveLedgerEntry(Guid employeeId, Guid leaveTypeId, DateOnly leaveYearStart, DateOnly leaveYearEnd, LeaveLedgerEntryType entryType, int minutesDelta, Guid? leaveRequestId, Guid? reversesEntryId, Guid policyVersionId, string reason, string idempotencyKey, string recordedByUserId, DateTimeOffset recordedAt)
    {
        if (leaveYearEnd <= leaveYearStart || minutesDelta == 0) throw new ArgumentException("A leave ledger entry needs a valid year and nonzero change.");
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        LeaveTypeId = ModelGuard.Id(leaveTypeId, nameof(leaveTypeId));
        LeaveYearStart = leaveYearStart;
        LeaveYearEnd = leaveYearEnd;
        EntryType = Enum.IsDefined(entryType) ? entryType : throw new ArgumentOutOfRangeException(nameof(entryType));
        MinutesDelta = minutesDelta;
        LeaveRequestId = leaveRequestId.HasValue ? ModelGuard.Id(leaveRequestId.Value, nameof(leaveRequestId)) : null;
        ReversesEntryId = reversesEntryId.HasValue ? ModelGuard.Id(reversesEntryId.Value, nameof(reversesEntryId)) : null;
        PolicyVersionId = ModelGuard.Id(policyVersionId, nameof(policyVersionId));
        Reason = ModelGuard.Required(reason, 1000, nameof(reason));
        IdempotencyKey = ModelGuard.Required(idempotencyKey, 100, nameof(idempotencyKey));
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public DateOnly LeaveYearStart { get; private set; }
    public DateOnly LeaveYearEnd { get; private set; }
    public LeaveLedgerEntryType EntryType { get; private set; }
    public int MinutesDelta { get; private set; }
    public Guid? LeaveRequestId { get; private set; }
    public Guid? ReversesEntryId { get; private set; }
    public Guid PolicyVersionId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public LeaveType LeaveType { get; private set; } = null!;
    public LeaveRequest? LeaveRequest { get; private set; }
    public LeaveLedgerEntry? ReversesEntry { get; private set; }
    public PolicyVersion PolicyVersion { get; private set; } = null!;
}
