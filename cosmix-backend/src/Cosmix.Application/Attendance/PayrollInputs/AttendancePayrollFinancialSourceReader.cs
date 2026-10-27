using System.Text.Json;
using Cosmix.Application.Attendance.CarryForward;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.PayrollInputs;

namespace Cosmix.Application.Attendance.PayrollInputs;

public sealed class AttendancePayrollFinancialSourceReader(IAttendancePayrollInputLedger ledger,
    IAttendancePayrollAttestationRepository attestations, IAttendancePayrollInputProducer producer,
    AttendancePayrollInputService inputs, ICarryForwardRepository factual, CarryForwardEvidence factualEvidence,
    AttendanceOperation operation, IAccountAccessReader access) : IAttendancePayrollFinancialSourceReader
{
    public Task<AttendancePayrollFinancialSources> ReadAsync(Guid employeeId, DateOnly sourceMonth,
        AttendanceActor actor, CancellationToken ct) => Read(employeeId, sourceMonth, null, actor, ct);

    public Task<AttendancePayrollFinancialSources> ReadOpenTargetAsync(Guid employeeId, DateOnly sourceMonth,
        DateOnly targetMonth, AttendanceActor actor, CancellationToken ct) => Read(employeeId, sourceMonth, targetMonth, actor, ct);

    private async Task<AttendancePayrollFinancialSources> Read(Guid employeeId, DateOnly month,
        DateOnly? retainedTarget, AttendanceActor actor, CancellationToken ct)
    {
        var scopes = (await producer.ResourceScopesAsync(employeeId, month, ct)).ToList();
        await Require(scopes, actor, ct);
        var headers = await attestations.HistoryAsync(employeeId, month, ct);
        if (headers.Count > 3 || headers.Count(x => x.Status == "posted") > 1)
            throw AttendanceOperation.Conflict("financial_carry_forward_source_conflict");
        var postedHeader = headers.SingleOrDefault(x => x.Status == "posted");
        if (postedHeader is null) throw AttendanceOperation.Conflict("financial_carry_forward_confirmed_posting_required");
        var row = await ledger.FindAsync(employeeId, postedHeader.SourceRevisionId, ct)
            ?? throw AttendanceOperation.Conflict("financial_carry_forward_source_missing");
        if (row.EmployeeId != employeeId || row.PeriodStart != month)
            throw AttendanceOperation.Conflict("financial_carry_forward_source_conflict");
        var saved = await AttendancePayrollInputService.ReadValidatedEvidenceAsync(row, actor,
            AttendancePayrollInputPermissions.Read, operation, access, ct);
        var history = await AttendancePayrollAttestationService.ReadValidatedHistoryAsync(row, saved, actor,
            operation, attestations, access, ct);
        var posted = history.SingleOrDefault(x => x.Id == postedHeader.Id && x.Status == "posted" && x.ConfirmsPosting)
            ?? throw AttendanceOperation.Conflict("financial_carry_forward_confirmed_posting_required");
        scopes.AddRange(saved.OriginalScopes);
        var changes = new List<AttendancePayrollFinancialCorrection>();
        foreach (var day in saved.Calculation.SourceManifest.Days.OrderBy(x => x.BusinessDate))
        {
            var latest = await factual.LatestAsync(employeeId, day.BusinessDate, ct);
            if (latest is null) continue;
            var proof = await factualEvidence.AdjustmentAsync(latest, ct);
            var frozen = await factualEvidence.FrozenAsync(employeeId, day.BusinessDate, ct);
            if (latest.EmployeeId != employeeId || latest.SourceBusinessDate != day.BusinessDate || day.Snapshot is null ||
                row.CreatedAtUtc > frozen.Rows.Lock.Event!.AtUtc ||
                day.Snapshot.FactualFingerprint != frozen.Day.Facts.InputFingerprint ||
                JsonSerializer.Serialize(day.Snapshot.Roster) != JsonSerializer.Serialize(frozen.Day.Roster))
                throw AttendanceOperation.Conflict("financial_carry_forward_frozen_source_conflict");
            scopes.AddRange(proof.OriginalScopes);
            var predecessors = new List<Guid>();
            var previousId = latest.SupersedesAdjustmentId;
            while (previousId is Guid previous)
            {
                if (predecessors.Count >= 31 || predecessors.Contains(previous))
                    throw AttendanceOperation.Conflict("financial_carry_forward_correction_history_limit");
                var prior = await factual.AdjustmentAsync(previous, ct)
                    ?? throw AttendanceOperation.Conflict("financial_carry_forward_correction_source_conflict");
                if (prior.EmployeeId != employeeId || prior.SourceBusinessDate != day.BusinessDate ||
                    prior.SourcePeriodLockEventId != latest.SourcePeriodLockEventId || prior.Revision >= latest.Revision)
                    throw AttendanceOperation.Conflict("financial_carry_forward_correction_source_conflict");
                predecessors.Add(previous); previousId = prior.SupersedesAdjustmentId;
            }
            changes.Add(new(latest.Id, proof, predecessors));
        }
        if (changes.Sum(x => 1 + (x.PredecessorAdjustmentIds?.Count ?? 0)) > 31)
            throw AttendanceOperation.Conflict("financial_carry_forward_correction_history_limit");
        _ = AttendancePayrollFinancialCalculation.Calculate(saved.Calculation, changes);
        var targetMonth = retainedTarget ?? CarryForwardRules.FirstAvailableMonth(month,
            await factual.LockedRangesAsync(month, ct)).Start;
        if (targetMonth <= month) throw AttendanceOperation.Conflict("financial_carry_forward_target_conflict");
        AttendancePayrollMonthSource target;
        try
        {
            scopes.AddRange(await producer.ResourceScopesAsync(employeeId, targetMonth, ct));
            await Require(scopes, actor, ct);
            target = await inputs.ReadForMonthLockAsync(employeeId, targetMonth, actor, ct);
        }
        catch (AuthenticationFailure failure) when (failure.StatusCode == 409 && failure.Code is
            "employee_assignment_unavailable" or "payroll_month_has_no_employment" or "payroll_employment_coverage_unavailable")
        { throw AttendanceOperation.Conflict("financial_carry_forward_target_inputs_unavailable"); }
        if (target.Row is null || target.Evidence is null || target.Issues.Count != 0)
            throw AttendanceOperation.Conflict("financial_carry_forward_target_inputs_unavailable");
        scopes.AddRange(target.Evidence.OriginalScopes);
        await Require(scopes, actor, ct);
        return new(AttendancePayrollFinancialRevision.From(row), saved.Calculation, posted, postedHeader.EvidenceFingerprint,
            AttendancePayrollFinancialRevision.From(target.Row), changes,
            scopes.DistinctBy(x => JsonSerializer.Serialize(x)).OrderBy(x => JsonSerializer.Serialize(x), StringComparer.Ordinal).ToArray());
    }

    private async Task Require(IReadOnlyList<AttendanceResourceScope> scopes, AttendanceActor actor, CancellationToken ct)
    {
        await operation.RequireScopedPermissionAsync(actor, AttendancePayrollInputPermissions.Read, scopes, ct);
        await operation.RequireScopedPermissionAsync(actor, AttendancePayrollAttestationPermissions.Read, scopes, ct);
    }
}
