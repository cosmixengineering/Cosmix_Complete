using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.HR.Employees;

/// <summary>Reconciles cached status and revocations. Authentication independently checks effective history on every request.</summary>
public sealed class EmployeeLifecycleProcessor(IEmployeeHiringRepository hiring, IEmployeeLifecycleRepository repository,
    IAuthenticationRepository authentication, IAccessManagementRepository transactions, TimeProvider clock)
{
    public async Task<int> ReconcileAsync(CancellationToken ct)
    {
        var batch = await ReconcileBatchAsync(0, ct);
        if (batch.ReviewRequired > 0) throw EmployeeOperation.Conflict("employee_lifecycle_reconciliation_requires_review");
        return batch.Applied;
    }

    public async Task<EmployeeLifecycleBatchResult> ReconcileBatchAsync(long afterEmployeeNumber, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterEmployeeNumber);
        var now = clock.GetUtcNow();
        var candidates = await transactions.InTransactionAsync(token => repository.DueEmployeesAsync(now, 25, token, afterEmployeeNumber), ct);
        var count = 0;
        var needsReview = 0;
        var lastEmployeeNumber = afterEmployeeNumber;
        foreach (var id in candidates)
        {
            try
            {
                if (await transactions.InTransactionAsync(async token =>
                {
                    var employee = await hiring.FindAsync(id, token) ?? throw EmployeeOperation.Missing();
                    lastEmployeeNumber = employee.EmployeeNumber;
                    var applied = await ApplyDueAsync(employee, clock.GetUtcNow(), token);
                    await hiring.SaveChangesAsync(token);
                    return applied;
                }, ct)) count++;
            }
            catch (AuthenticationFailure error) when (error.StatusCode == 409) { needsReview++; }
        }
        // Progress past conflicts, then wrap for a fresh sweep, without storing another business/job table.
        return new(count, candidates.Count < 25 ? 0 : lastEmployeeNumber, needsReview);
    }

    /// <summary>Caller owns the existing serializable authentication transaction.</summary>
    public async Task<bool> ApplyDueAsync(Employee employee, DateTimeOffset now, CancellationToken ct)
    {
        var current = await repository.CurrentChangesAsync(employee.Id, now, ct);
        if (current.Count > 1 && current[0].EffectiveAt == current[1].EffectiveAt)
            throw EmployeeOperation.Conflict("employee_status_history_conflict");
        var rows = await repository.UnappliedChangesAsync(employee.Id, employee.LastAppliedStatusChangeId, now, ct);
        if (rows.Count == 0) return false;
        if (rows.GroupBy(x => x.EffectiveAt).Any(x => x.Count() > 1))
            throw EmployeeOperation.Conflict("employee_status_history_conflict");
        foreach (var change in rows)
        {
            if (!change.ApprovalRequestId.HasValue) throw EmployeeOperation.Conflict("employee_status_requires_manual_review");
            var request = await repository.FindRequestAsync(employee.Id, change.ApprovalRequestId.Value, ct);
            var decisions = await repository.DecisionsAsync(change.ApprovalRequestId.Value, ct);
            if (request?.State != ApprovalState.Approved || decisions.Count != 1 ||
                decisions[0].Decision != ApprovalState.Approved || decisions[0].DecidedAt != change.RecordedAt ||
                decisions[0].ActorUserId != change.ChangedByUserId || request.ClosedAt != change.RecordedAt ||
                !await repository.HasApprovedEmploymentAsync(employee, ct))
                throw EmployeeOperation.Conflict("employee_status_approval_evidence_invalid");
            await ApplyAsync(employee, change, now, ct);
        }
        return true;
    }

    /// <summary>Called only after a verified due approval, within the caller's transaction.</summary>
    public async Task ApplyAsync(Employee employee, EmployeeStatusChange change, DateTimeOffset now, CancellationToken ct)
    {
        var account = await repository.FindAccountAsync(employee.Id, ct);
        if (account is not null && await transactions.HasSystemRoleAsync(account.Id, now, includeScheduled: true, ct))
            throw EmployeeOperation.Conflict("protected_owner_requires_separate_handover");
        try { employee.ApplyStatusChange(change, now); }
        catch (InvalidOperationException) { throw EmployeeOperation.Conflict("employee_status_history_conflict"); }
        var reason = $"employee-lifecycle:{change.Id:D}";
        if (account is not null)
        {
            // An independent block remains in force; release prepares eligibility without reviving any channel.
            if (account.Status == AccountStatus.Disabled) account.InvalidateSecurityVersion();
            else if (change.NewStatus == EmployeeStatus.Active)
            {
                if (account.Status == AccountStatus.Suspended && account.StatusReason == "employee-lifecycle")
                    account.PrepareForEmploymentReactivation(now);
                else account.InvalidateSecurityVersion();
            }
            else if (change.NewStatus == EmployeeStatus.Suspended)
            {
                if (account.Status == AccountStatus.Suspended && account.StatusReason != "employee-lifecycle") account.InvalidateSecurityVersion();
                else account.Suspend("employee-lifecycle", now);
            }
            else account.Disable("employee-lifecycle", now);
            account.SetMobileAccess(false, now);
            account.SetWebAccess(false, now);
            foreach (var session in await authentication.GetSessionsAsync(account.Id, ct)) session.Revoke(reason, now);
            foreach (var challenge in await authentication.GetOpenChallengesAsync(account.Id, ct)) challenge.Revoke(now);
            foreach (var device in await repository.UnrevokedDevicesAsync(account.Id, ct)) device.Revoke(reason, now);
            var assignments = await repository.UnrevokedRoleAssignmentsAsync(account.Id, ct);
            if (assignments.Count > 0 && !Guid.TryParse(change.ChangedByUserId, out _))
                throw EmployeeOperation.Conflict("employee_status_approval_actor_invalid");
            foreach (var assignment in assignments) assignment.Revoke(Guid.Parse(change.ChangedByUserId), reason, now);
        }
        // Release also retires any enrollment left by an older offboarding implementation.
        await repository.RevokeFaceEnrollmentsAsync(employee.Id, change.Id, now, ct);
        hiring.AddAudit(new AuditEvent("hr.employee.lifecycle.effective", "system:employee-lifecycle", "SYSTEM",
            "EmployeeStatusChange", change.Id.ToString(), now, change.Id.ToString("N"), employee.Id));
    }
}
