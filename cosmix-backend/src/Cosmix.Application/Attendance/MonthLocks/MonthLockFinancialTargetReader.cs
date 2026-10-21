using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Attendance.MonthLocks;

/// <summary>Transaction-local exact membership check, without recursively invoking financial inspection.</summary>
public sealed class MonthLockFinancialTargetReader(IMonthLockRepository repository, AttendanceOperation operation)
    : IAttendancePayrollFinancialTargetLockReader
{
    public async Task<bool> ContainsAsync(AttendancePayrollFinancialMonthEntry entry, AttendanceActor actor, CancellationToken ct)
    {
        // The financial service owns current authorization, source scopes and the SQL transaction.
        if (actor.Identity.Client != SessionClient.Web || !actor.Identity.MfaSatisfied)
            throw new AuthenticationFailure("access_denied", 403);
        var existing = await repository.FindAsync(entry.TargetMonth, entry.TargetMonth.AddMonths(1), ct);
        if (existing?.Period.IsLocked != true) return false;
        if (existing.Event?.ProtectedEvidenceReference is not string reference)
            throw AttendanceOperation.Conflict("month_lock_evidence_unavailable");
        var snapshot = await operation.ReadPrivateAsync<MonthLockSnapshot>(reference,
            MonthLockEvidence.Purpose(existing.Period.Id, existing.Event.Id), ct);
        MonthLockEvidence.Validate(existing, snapshot);
        if (snapshot.SchemaVersion != 4) return false;
        var retained = snapshot.FinancialAdjustments!.SingleOrDefault(x => x.AdjustmentId == entry.AdjustmentId);
        if (retained is null) return false;
        MonthLockFinancialAdjustments.RequireRetainedMatch([retained], [entry]);
        return true;
    }
}
