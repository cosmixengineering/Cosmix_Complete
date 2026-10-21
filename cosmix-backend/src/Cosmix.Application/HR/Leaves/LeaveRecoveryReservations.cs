using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

public sealed partial class LeaveEncashmentService
{
    private async Task VerifyRecoveryReservationsAsync(RecoveryContext initial,
        IReadOnlyList<LeaveRecoveryCommitment> roots, DateTimeOffset now, CancellationToken ct, bool otherLeaves = false)
    {
        const string conflict = "leave_recovery_reservation_evidence_unavailable";
        var employeeId = initial.Parent.Request.EmployeeId;
        var contexts = new Dictionary<Guid, RecoveryContext> { [initial.Parent.Request.Id] = initial };
        var byLeave = new Dictionary<Guid, IReadOnlyList<LeaveRecoveryCommitment>>();
        var byDate = new Dictionary<DateOnly, IReadOnlyList<LeaveRecoveryCommitment>>();
        var discovered = new Dictionary<Guid, LeaveRecoveryCommitmentResponse>();
        var dependencies = new Dictionary<Guid, HashSet<Guid>>();
        var pending = new Queue<LeaveRecoveryCommitment>();
        ValidateSet(roots);
        if (!otherLeaves && roots.Any(x => x.LeaveRequestId != initial.Parent.Request.Id))
            throw AttendanceOperation.Conflict(conflict);
        foreach (var root in roots) Enqueue(root);

        while (pending.TryDequeue(out var row))
        {
            ct.ThrowIfCancellationRequested();
            if (!contexts.TryGetValue(row.LeaveRequestId, out var context))
            {
                var parent = await leaves.FindAsync(row.LeaveRequestId, employeeId, ct);
                if (parent is null || parent.Request.Id != row.LeaveRequestId || parent.Request.EmployeeId != employeeId)
                    throw AttendanceOperation.Conflict(conflict);
                await provenance.VerifyAsync(parent, ct);
                await VerifyRecoveryCancellationsAsync(parent, now, ct);
                // Cancellation preserves a historical promise; it does not release capacity.
                context = await RecoveryContextAsync(parent, now, ct);
                contexts.Add(row.LeaveRequestId, context);
            }
            var before = await VerifyRecoveryCommitmentAsync(context, row, now, ct);
            if (!byLeave.TryGetValue(row.LeaveRequestId, out var sameLeave))
            {
                sameLeave = await repository.GetRecoveryCommitmentsAsync(row.LeaveRequestId, ct);
                ValidateSet(sameLeave);
                if (sameLeave.Any(x => x.LeaveRequestId != row.LeaveRequestId))
                    throw AttendanceOperation.Conflict(conflict);
                byLeave.Add(row.LeaveRequestId, sameLeave);
            }
            if (!byDate.TryGetValue(row.CommitmentDate, out var sameDate))
            {
                sameDate = await repository.RecoveryOnDateAsync(employeeId, row.CommitmentDate, ct);
                ValidateSet(sameDate);
                if (sameDate.Any(x => x.CommitmentDate != row.CommitmentDate))
                    throw AttendanceOperation.Conflict(conflict);
                byDate.Add(row.CommitmentDate, sameDate);
            }

            var priorLeave = PriorRows(before.ExistingCommitmentIds, sameLeave, row);
            var priorDate = PriorRows(before.SameDateCommitmentIds, sameDate, row);
            if (priorLeave.Any(x => x.CommitmentDate == row.CommitmentDate) ||
                priorLeave.Sum(x => (long)x.CommittedMinutes) + row.CommittedMinutes >
                    Math.Min(context.Rules.MaximumMinutesPerLeave, context.Parent.Request.RequestedMinutes) ||
                priorDate.Sum(x => (long)x.CommittedMinutes) + row.CommittedMinutes > context.Rules.MaximumMinutesPerDay)
                throw AttendanceOperation.Conflict(conflict);
            var prior = priorLeave.Concat(priorDate).ToArray();
            dependencies.Add(row.Id, prior.Select(x => x.Id).ToHashSet());
            foreach (var predecessor in prior) Enqueue(predecessor);
        }

        // Equal timestamps are legitimate. Recorded dependency order must still be acyclic.
        var remaining = dependencies.ToDictionary(x => x.Key, x => x.Value.Count);
        var dependents = dependencies.SelectMany(x => x.Value.Select(id => (Prior: id, Next: x.Key)))
            .ToLookup(x => x.Prior, x => x.Next);
        var ready = new Queue<Guid>(remaining.Where(x => x.Value == 0).Select(x => x.Key));
        var verified = 0;
        while (ready.TryDequeue(out var id))
        {
            verified++;
            foreach (var next in dependents[id]) if (--remaining[next] == 0) ready.Enqueue(next);
        }
        if (verified != dependencies.Count) throw AttendanceOperation.Conflict(conflict);

        void Enqueue(LeaveRecoveryCommitment row)
        {
            if (discovered.TryGetValue(row.Id, out var saved))
            {
                if (saved != RecoveryResponse(row)) throw AttendanceOperation.Conflict(conflict);
                return;
            }
            // Same operational evidence bound as the existing repository, not a company entitlement.
            if (discovered.Count >= 1000) throw AttendanceOperation.Conflict("leave_recovery_history_limit");
            discovered.Add(row.Id, RecoveryResponse(row));
            pending.Enqueue(row);
        }

        static void ValidateSet(IReadOnlyList<LeaveRecoveryCommitment> rows)
        {
            if (rows.Count > 1000) throw AttendanceOperation.Conflict("leave_recovery_history_limit");
            if (rows.Any(x => x.Id == Guid.Empty) || rows.Select(x => x.Id).Distinct().Count() != rows.Count)
                throw AttendanceOperation.Conflict(conflict);
        }

        static LeaveRecoveryCommitment[] PriorRows(Guid[] ids, IReadOnlyList<LeaveRecoveryCommitment> rows,
            LeaveRecoveryCommitment current)
        {
            var members = rows.ToDictionary(x => x.Id);
            if (!members.TryGetValue(current.Id, out var persisted) || RecoveryResponse(persisted) != RecoveryResponse(current) ||
                ids.Any(id => !members.TryGetValue(id, out var prior) || prior.RecordedAt > current.RecordedAt) ||
                rows.Any(x => x.Id != current.Id && x.RecordedAt < current.RecordedAt && !ids.Contains(x.Id)))
                throw AttendanceOperation.Conflict(conflict);
            return ids.Select(id => members[id]).ToArray();
        }
    }
}
