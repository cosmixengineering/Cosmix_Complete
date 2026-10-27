using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Application.Authentication;

namespace Cosmix.Application.Attendance.PayrollInputs;

/// <summary>Inspects authoritative monthly dependencies under the month operation's transaction.
/// Recorded legal dossiers are not activation. Amounts stay in the restricted source, not lock DTOs.</summary>
public sealed class AttendanceMonthLockFinancialReader(AttendancePayrollInputService inputs,
    AttendancePayrollAttestationService attestations, IAttendancePayrollInputLedger ledger,
    IAttendancePayrollAttestationRepository receipts, AttendancePayrollActivationService? activations = null) : IMonthLockFinancialReader
{
    public async Task<MonthLockFinancialInspection> InspectAsync(DateOnly start, DateOnly end,
        IReadOnlyList<Guid> employeeIds, AttendanceActor actor, CancellationToken ct)
    {
        if (start.Day != 1 || end != start.AddMonths(1) || employeeIds.Count > MonthLockCoverage.MaximumEmployees ||
            employeeIds.Any(x => x == Guid.Empty) || employeeIds.Distinct().Count() != employeeIds.Count)
            throw AttendanceOperation.Conflict("month_lock_financial_contract_invalid");
        var entries = new List<MonthLockPayrollInputEntry>();
        var issues = new List<MonthLockIssue>();
        var fingerprints = new List<string>();
        foreach (var employee in employeeIds.Order())
        {
            AttendancePayrollMonthSource source;
            try { source = await inputs.ReadForMonthLockAsync(employee, start, actor, ct); }
            catch (AuthenticationFailure failure) when (failure.StatusCode == 409 && failure.Code == "employee_assignment_unavailable")
            {
                issues.Add(new(failure.Code, employee));
                fingerprints.Add(MonthLockEvidence.Fingerprint(new { employee, unavailable = failure.Code }));
                continue;
            }
            fingerprints.Add(source.CurrentFingerprint);
            issues.AddRange(source.Issues);
            if (source.Row is null || source.Evidence is null) continue;
            var row = source.Row; var evidence = source.Evidence;
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(row.ProtectedEvidenceReference)
                ?? throw AttendanceOperation.Conflict("payroll_input_evidence_invalid");
            Guid? handoffId = null; string? handoffHash = null;
            IReadOnlyList<AttendancePayrollAttestationResponse> history;
            try { history = await attestations.ReadForMonthLockAsync(row, actor, ct); }
            catch (AuthenticationFailure failure) when (failure.StatusCode == 409 &&
                failure.Code == "payroll_attestation_chain_source_conflict")
            {
                // A newer calculation cannot inherit an older handoff. Keep the month
                // visibly unready without discarding the other employees' dependencies.
                issues.Add(new("payroll_handoff_source_reconciliation_required", employee));
                var conflicting = await receipts.HistoryAsync(employee, start, ct);
                fingerprints.Add(MonthLockEvidence.Fingerprint(new { employee,
                    sourceRevision = row.Id, conflicting = conflicting.OrderBy(x => x.Step).Select(x => new {
                        x.Id, x.SourceRevisionId, x.SourceRevision, x.Step, x.Status, x.EvidenceFingerprint }) }));
                history = [];
            }
            var handoff = history.FirstOrDefault(x => x.Status == "handoff");
            if (handoff is null)
                issues.Add(new(evidence.Calculation.Lines.Count == 0 ? "payroll_no_charge_handoff_required" : "payroll_handoff_required", employee));
            else
            {
                var header = (await receipts.HistoryAsync(employee, start, ct)).Single(x => x.Id == handoff.Id);
                handoffId = header.Id; handoffHash = header.EvidenceFingerprint;
            }
            if (evidence.Jurisdictions.Count == 0 || evidence.Jurisdictions.Any(x => x.State != "configured" ||
                x.ConfigurationVersionId is null || string.IsNullOrWhiteSpace(x.JurisdictionCode)))
                issues.Add(new("payroll_jurisdiction_configuration_required", employee));
            // Settings and recorded legal dossiers do not activate payroll. Exact-source,
            // independent company operational approval is a separate protected dependency.
            var activation = activations is null ? null : await activations.ApprovedFingerprintAsync(row, evidence, ct);
            if (activation is null) issues.Add(new("legal_payroll_activation_required", employee));
            entries.Add(new(employee, start, row.Id, row.Revision, row.InputFingerprint, row.CalculationVersion,
                reference.Sha256, handoffId, handoffHash, activation));
        }
        var distinct = issues.Distinct().OrderBy(x => x.Code, StringComparer.Ordinal)
            .ThenBy(x => x.EmployeeId).ThenBy(x => x.BusinessDate).ToArray();
        var digest = MonthLockEvidence.Fingerprint(new { schema = "monthly-financial-dependencies-v1", start, end,
            employees = employeeIds.Order().ToArray(), fingerprints, entries, issues = distinct });
        return new(digest, entries, distinct);
    }

    public async Task VerifyRetainedAsync(IReadOnlyList<MonthLockPayrollInputEntry> entries,
        AttendanceActor actor, CancellationToken ct)
    {
        foreach (var entry in entries)
        {
            var row = await ledger.FindAsync(entry.EmployeeId, entry.SourceRevisionId, ct)
                ?? throw AttendanceOperation.Conflict("month_lock_payroll_revision_missing");
            var retained = await inputs.ReadRetainedForMonthLockAsync(entry.EmployeeId, entry.SourceRevisionId, actor, ct);
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(row.ProtectedEvidenceReference);
            if (row.PeriodStart != entry.MonthStart || row.Revision != entry.SourceRevision ||
                row.InputFingerprint != entry.InputFingerprint || row.CalculationVersion != entry.CalculationVersion ||
                reference?.Sha256 != entry.SourceEvidenceSha256 || retained.Calculation.State != "ready")
                throw AttendanceOperation.Conflict("month_lock_payroll_revision_conflict");
            var history = await attestations.ReadForMonthLockAsync(row, actor, ct);
            var header = (await receipts.HistoryAsync(entry.EmployeeId, entry.MonthStart, ct))
                .SingleOrDefault(x => x.Id == entry.HandoffId);
            if (!history.Any(x => x.Id == entry.HandoffId && x.Status == "handoff") ||
                header?.EvidenceFingerprint != entry.HandoffEvidenceFingerprint)
                throw AttendanceOperation.Conflict("month_lock_payroll_handoff_conflict");
            var activation = activations is null ? null : await activations.ApprovedFingerprintAsync(row, retained, ct);
            if (activation is null || activation != entry.LegalActivationFingerprint)
                throw AttendanceOperation.Conflict("month_lock_legal_activation_unavailable");
        }
    }
}
