using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

public static class LeaveEncashmentLedger
{
    public static string ReserveKey(Guid id) => $"encash-reserve:{id:N}";
    public static string ReleaseKey(Guid id) => $"encash-release:{id:N}";
    public static string ConsumeKey(Guid id) => $"encash-consume:{id:N}";
    public static IReadOnlyList<LeaveLedgerEntry> Entries(LeaveEncashmentRequest row, IReadOnlyList<LeaveLedgerEntry> ledger) =>
        ledger.Where(x => x.IdempotencyKey == ReserveKey(row.Id) || x.IdempotencyKey == ReleaseKey(row.Id) || x.IdempotencyKey == ConsumeKey(row.Id)).ToArray();

    public static LeaveLedgerEntry? Verify(LeaveEncashmentRequest row, IReadOnlyList<LeaveLedgerEntry> ledger, bool allowLegacy = false)
    {
        var entries = Entries(row, ledger);
        if (entries.Count == 0 && allowLegacy) return null;
        var reserve = entries.SingleOrDefault(x => x.IdempotencyKey == ReserveKey(row.Id));
        var release = entries.SingleOrDefault(x => x.IdempotencyKey == ReleaseKey(row.Id));
        var consume = entries.SingleOrDefault(x => x.IdempotencyKey == ConsumeKey(row.Id));
        if (reserve is null || reserve.EntryType != LeaveLedgerEntryType.Reservation || reserve.MinutesDelta != -row.RequestedMinutes ||
            reserve.ReversesEntryId is not null || reserve.RecordedAt != row.CreatedAt || reserve.RecordedByUserId != row.RequestedByUserId ||
            entries.Any(x => x.EmployeeId != row.EmployeeId || x.LeaveRequestId is not null || x.LeaveTypeId != reserve.LeaveTypeId ||
                x.PolicyVersionId != row.PolicyVersionId || x.LeaveYearStart != row.LeaveYearStart || x.LeaveYearEnd != row.LeaveYearEnd))
            throw AttendanceOperation.Conflict("encashment_reservation_conflict");
        var state = row.ApprovalRequest?.State;
        if (state == ApprovalState.Pending)
        {
            if (entries.Count != 1) throw AttendanceOperation.Conflict("encashment_reservation_conflict");
        }
        else if (state is ApprovalState.Approved or ApprovalState.Rejected or ApprovalState.Returned)
        {
            if (release is null || release.EntryType != LeaveLedgerEntryType.Release || release.MinutesDelta != row.RequestedMinutes ||
                release.ReversesEntryId != reserve.Id || release.RecordedAt != row.ApprovalRequest!.ClosedAt ||
                entries.Count != (state == ApprovalState.Approved ? 3 : 2) ||
                state == ApprovalState.Approved && (consume is null || consume.EntryType != LeaveLedgerEntryType.Encashment ||
                    consume.MinutesDelta != -row.RequestedMinutes || consume.ReversesEntryId is not null || consume.RecordedAt != release.RecordedAt ||
                    consume.RecordedByUserId != release.RecordedByUserId))
                throw AttendanceOperation.Conflict("encashment_reservation_conflict");
        }
        else throw AttendanceOperation.Conflict("encashment_reservation_conflict");
        return reserve;
    }
}
