using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Attendance.PayrollInputs;

/// <summary>Reads exact retained manual posted/settled evidence under the caller's transaction.
/// No producer/service dependency, transaction, posting inference, credit or financial write.</summary>
public sealed class AttendancePayrollReconciliationSourceReader(IAttendancePayrollInputLedger ledger,
    IAttendancePayrollAttestationRepository receipts, IAttendanceSummaryRepository summaries,
    AttendanceOperation operation, ICurrentAuthentication current, IAccountAccessReader access,
    ISchedulingJournalRepository journal, TimeProvider clock) : IAttendancePayrollReconciliationSourceReader
{
    public async Task<AttendancePayrollReconciliationSources> ReadAsync(Guid employeeId, DateOnly targetMonthStart,
        IReadOnlyList<DateOnly> priorDates, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || targetMonthStart.Day != 1 || targetMonthStart.Year is < 1900 or > 9998 ||
            priorDates.Count is < 1 or > AttendancePayrollInputRules.MaximumDays ||
            priorDates.Distinct().Count() != priorDates.Count)
            throw AttendanceOperation.Conflict("prior_deduction_reconciliation_range_invalid");
        var earliest = new DateOnly(targetMonthStart.Month < 7 ? targetMonthStart.Year - 1 : targetMonthStart.Year,
            targetMonthStart.Month < 7 ? 7 : 1, 1);
        if (priorDates.Any(x => x < earliest || x >= targetMonthStart))
            throw AttendanceOperation.Conflict("prior_deduction_reconciliation_range_invalid");
        var identity = current.Require();
        if (identity.Client != SessionClient.Web || !identity.MfaSatisfied) throw AttendanceOperation.Denied();
        if (!await journal.IsCurrentSessionAsync(identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        var actor = new AttendanceActor(identity, "", clock.GetUtcNow(), null);
        var days = new List<AttendancePayrollConfirmedOrdinaryDay>();
        var scopes = new List<AttendanceResourceScope>();
        var issues = new List<AttendancePayrollInputIssue>();
        foreach (var month in priorDates.GroupBy(x => new DateOnly(x.Year, x.Month, 1)).OrderBy(x => x.Key))
        {
            ct.ThrowIfCancellationRequested();
            var dates = month.Order().ToArray();
            var currentScopes = await summaries.ResourceScopesAsync(employeeId, month.Key, month.Key.AddMonths(1).AddDays(-1), ct);
            await RequireScopes(employeeId, actor, currentScopes, ct);
            scopes.AddRange(currentScopes);
            var headers = await receipts.HistoryAsync(employeeId, month.Key, ct);
            if (headers.Count > 3 || headers.Any(x => x.EmployeeId != employeeId || x.PeriodStart != month.Key) ||
                headers.Select(x => x.Id).Distinct().Count() != headers.Count ||
                headers.Select(x => x.SourceRevisionId).Distinct().Count() > 1)
                throw AttendanceOperation.Conflict("payroll_attestation_chain_source_conflict");
            if (headers.Count == 0)
            {
                issues.Add(new("prior_deduction_posting_unavailable", SourceDates: dates));
                continue;
            }
            // A later saved replacement is never the financial source of an already pinned receipt.
            var row = await ledger.FindAsync(employeeId, headers[0].SourceRevisionId, ct);
            if (row is null || row.EmployeeId != employeeId || row.PeriodStart != month.Key)
                throw AttendanceOperation.Conflict("prior_deduction_reconciliation_revision_missing");
            var evidence = await AttendancePayrollInputService.ReadValidatedEvidenceAsync(row, actor,
                AttendancePayrollInputPermissions.Read, operation, access, ct);
            var source = evidence.Calculation;
            var manifest = source.SourceManifest;
            if (source.State != "ready" || source.Issues.Count != 0 || manifest.SchemaVersion != 1 ||
                source.SourceFrom > source.SourceTo || source.SourceTo >= month.Key.AddMonths(1) ||
                manifest.SourceFrom != source.SourceFrom || manifest.SourceTo != source.SourceTo ||
                manifest.Days.Count is < 1 or > AttendancePayrollInputRules.MaximumDays ||
                manifest.Days.Select(x => x.BusinessDate).Distinct().Count() != manifest.Days.Count ||
                manifest.Days.Any(x => x.BusinessDate < source.SourceFrom || x.BusinessDate > source.SourceTo))
                throw AttendanceOperation.Conflict("prior_deduction_reconciliation_source_conflict");
            var retainedCurrentScopes = await summaries.ResourceScopesAsync(employeeId, source.SourceFrom, source.SourceTo, ct);
            await RequireScopes(employeeId, actor, retainedCurrentScopes, ct);
            await RequireScopes(employeeId, actor, evidence.OriginalScopes, ct);
            scopes.AddRange(retainedCurrentScopes); scopes.AddRange(evidence.OriginalScopes);
            var history = await AttendancePayrollAttestationService.ReadValidatedHistoryAsync(row, evidence, actor,
                operation, receipts, access, ct);
            var confirmed = history.LastOrDefault(x => x.ConfirmsPosting);
            if (confirmed is null || confirmed.Status is not ("posted" or "settled"))
            {
                issues.Add(new("prior_deduction_posting_unavailable", SourceDates: dates));
                continue;
            }
            var header = headers.SingleOrDefault(x => x.Id == confirmed.Id);
            if (header is null || confirmed.SourceRevisionId != row.Id || confirmed.SourceFingerprint != row.InputFingerprint)
                throw AttendanceOperation.Conflict("prior_deduction_reconciliation_posting_conflict");
            foreach (var date in dates)
            {
                var day = manifest.Days.SingleOrDefault(x => x.BusinessDate == date);
                var policy = day?.Policy ?? day?.Snapshot?.Policy;
                if (day is null || day.State != "available" || day.Snapshot is null || day.Time?.Status != "calculated" ||
                    day.Compensation is null || policy is null || policy.State != "effective" || policy.PolicyVersionId is null ||
                    day.Snapshot.EmployeeId != employeeId || day.Snapshot.BusinessDate != date ||
                    day.Policy is not null && JsonSerializer.Serialize(day.Policy) != JsonSerializer.Serialize(day.Snapshot.Policy))
                    throw AttendanceOperation.Conflict("prior_deduction_reconciliation_source_conflict");
                if (source.Lines.Any(x => x.Kind == "late_threshold" && x.SourceDates.Contains(date)))
                {
                    issues.Add(new("prior_deduction_reconciliation_overlap_unavailable", date, [date]));
                    continue;
                }
                var ordinary = source.Lines.Where(x => (x.Kind is "ordinary_day" or "ordinary_shortfall") &&
                    (x.CompletionDate == date || x.SourceDates.Contains(date))).ToArray();
                if (ordinary.Length > 1 || ordinary.Any(x => x.CompletionDate != date ||
                    x.SourceDates.Count != 1 || x.SourceDates[0] != date))
                    throw AttendanceOperation.Conflict("prior_deduction_reconciliation_source_conflict");
                days.Add(new(date, month.Key, row.Id, row.InputFingerprint, confirmed.Id,
                    header.EvidenceFingerprint, confirmed.Status, day.Fingerprint, policy.PolicyVersionId.Value,
                    day.Compensation.Id, day.Compensation.Fingerprint, ordinary.SingleOrDefault()));
            }
        }
        var retainedScopes = scopes.DistinctBy(x => JsonSerializer.Serialize(x)).OrderBy(x => JsonSerializer.Serialize(x), StringComparer.Ordinal).ToArray();
        if (!AttendanceScopeEvaluation.IsValid(retainedScopes))
            throw AttendanceOperation.Conflict("payroll_source_scope_conflict");
        return new(days.OrderBy(x => x.BusinessDate).ToArray(), retainedScopes, issues);
    }

    private async Task RequireScopes(Guid employeeId, AttendanceActor actor,
        IReadOnlyList<AttendanceResourceScope> scopes, CancellationToken ct)
    {
        if (!AttendanceScopeEvaluation.IsValid(scopes) || scopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId != employeeId))
            throw AttendanceOperation.Conflict("payroll_source_scope_conflict");
        var grants = await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, ct);
        if (!AttendanceScopeEvaluation.Allows(actor.Identity, grants, AttendancePayrollInputPermissions.Read, scopes) ||
            !AttendanceScopeEvaluation.Allows(actor.Identity, grants, AttendancePayrollAttestationPermissions.Read, scopes))
            throw AttendanceOperation.Denied();
    }
}
