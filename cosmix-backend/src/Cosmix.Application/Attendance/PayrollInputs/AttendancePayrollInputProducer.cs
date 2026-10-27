using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Compensation;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Application.HR.StaffCategories;

namespace Cosmix.Application.Attendance.PayrollInputs;

/// <summary>Read-only producer. The caller must authorize ResourceScopesAsync inside the same
/// transaction before CalculateAsync. Financial evidence never enters employee daily-summary DTOs.</summary>
public sealed class AttendancePayrollInputProducer(IAttendancePayrollInputRepository repository,
    IAttendanceSummaryRepository summaries, IIndividualRosterResolver rosters,
    IAttendanceCalculationSnapshotProducer snapshots, IEmployeeCompensationRepository compensationHistory,
    AttendanceOperation operation, IAttendancePolicyResolver policies,
    IAttendancePayrollReconciliationSourceReader? reconciliation = null) : IAttendancePayrollInputProducer
{
    public async Task<IReadOnlyList<AttendanceResourceScope>> ResourceScopesAsync(Guid employeeId, DateOnly monthStart, CancellationToken ct)
    {
        var range = await RangeAsync(employeeId, monthStart, ct);
        return await ScopesAsync(employeeId, range.From, range.To, ct);
    }

    public async Task<AttendancePayrollInputCalculation> CalculateAsync(Guid employeeId, DateOnly monthStart, CancellationToken ct)
    {
        var range = await RangeAsync(employeeId, monthStart, ct);
        var scopes = await ScopesAsync(employeeId, range.From, range.To, ct);
        var sessions = await repository.SessionsAsync(employeeId, range.From, range.To.AddDays(1), ct);
        if (sessions.Any(x => x.EmployeeId != employeeId || x.AttendanceDate < range.From || x.AttendanceDate > range.To))
            throw AttendanceOperation.Conflict("payroll_session_identity_conflict");
        if (await compensationHistory.HasConflictingHistoryAsync(employeeId, ct))
            throw AttendanceOperation.Conflict("payroll_compensation_history_conflict");
        var salaryRows = await repository.CompensationAsync(employeeId, ct);
        if (salaryRows.Count > 4096 || salaryRows.Any(x => x.Record.EmployeeId != employeeId) ||
            salaryRows.Select(x => x.Record.Id).Distinct().Count() != salaryRows.Count)
            throw AttendanceOperation.Conflict("payroll_compensation_history_conflict");
        var verified = new Dictionary<Guid, AttendancePayrollCompensation>();
        var manifestDays = new List<AttendancePayrollDaySource>();
        var inputs = new List<AttendancePayrollInputDay>();
        foreach (var cell in range.Population.Cells.OrderBy(x => x.BusinessDate))
        {
            ct.ThrowIfCancellationRequested();
            var day = cell.BusinessDate;
            var roster = await rosters.ResolveAsync(employeeId, day, ct);
            var facts = AttendanceFacts.Calculate(employeeId, day, sessions.Where(x => x.AttendanceDate == day).ToArray());
            var source = await snapshots.ReadAsync(employeeId, day, facts, roster, ct);
            if (source.Snapshot is { } taxonomySnapshot && !RosterStaffTaxonomy.Valid(taxonomySnapshot.Roster.Taxonomy,
                taxonomySnapshot.Employment.StaffCategory,
                taxonomySnapshot.Roster.BranchId.HasValue ? StaffWorkLocation.Office : StaffWorkLocation.Site,
                taxonomySnapshot.Roster.BranchId.HasValue ? StaffScheduleBasis.Office : StaffScheduleBasis.Site))
                throw AttendanceOperation.Conflict(taxonomySnapshot.Roster.BranchId.HasValue ?
                    "payroll_office_source_conflict" : "payroll_taxonomy_source_conflict");
            if (roster.BranchId.HasValue || source.Snapshot?.OfficeSource is not null || source.Snapshot?.Employment.StaffCategory == "OFFICE")
            {
                var snapshot = source.Snapshot; var office = snapshot?.OfficeSource;
                if (roster.BranchId is null || roster.BranchId == Guid.Empty || source.State == "complete" && snapshot is null ||
                    snapshot is not null && (office is null || snapshot.TimeInput is null || snapshot.SchemaVersion != 1 || snapshot.EmployeeId != employeeId ||
                        snapshot.BusinessDate != day || snapshot.CalculationVersion != AttendanceDailyTimeCalculation.CalculationVersionFor(snapshot.TimeInput) ||
                        snapshot.FactualFingerprint != facts.InputFingerprint || JsonSerializer.Serialize(snapshot.Roster) != JsonSerializer.Serialize(roster) ||
                        !RosterStaffTaxonomy.Valid(snapshot.Roster.Taxonomy, snapshot.Employment.StaffCategory,
                            StaffWorkLocation.Office, StaffScheduleBasis.Office) || snapshot.Employment.SiteId is not null ||
                        snapshot.Employment.BranchId != roster.BranchId || snapshot.Employment.AssignmentId != office.EmployeeAssignmentId ||
                        snapshot.Employment.EffectiveFrom > day || snapshot.Employment.EffectiveTo <= day ||
                        roster.SiteId is not null || roster.Explanation?.Source != "office" ||
                        office.BusinessDate != day || office.EmployeeId != employeeId || office.BranchId != roster.BranchId ||
                        office.EmployeeAssignmentId == Guid.Empty || office.EmployeeAssignmentId != roster.Explanation.MembershipRecordId ||
                        office.OfficeRosterAssignmentId == Guid.Empty || office.OfficeRosterAssignmentId != roster.AssignmentId ||
                        office.OfficeRosterAssignmentId != roster.Explanation.OfficeRosterAssignmentId ||
                        office.ShiftVersionId == Guid.Empty || office.ShiftVersionId != roster.ShiftVersionId ||
                        office.HolidayCalendarId != roster.Holiday?.CalendarId || office.OfficeGeofenceVersionId == Guid.Empty || office.BoundaryVersionNumber < 1))
                    throw AttendanceOperation.Conflict("payroll_office_source_conflict");
            }
            var policy = source.Snapshot?.Policy ?? await policies.ResolveAsync(employeeId, day, ct);
            var state = source.State == "complete" ? "available" : source.State;
            if (source.Snapshot?.ReviewedWork?.FinancialUnverified == true) state = "reviewed_work_financial_interpretation_required";
            var time = source.State == "complete" && source.Snapshot is not null
                ? AttendanceDailyTimeCalculation.Calculate(source.Snapshot.TimeInput) : null;
            AttendancePayrollCompensation? salary = null;
            var candidate = salaryRows.Where(x => x.Record.EffectiveFrom <= day)
                .OrderByDescending(x => x.Record.EffectiveFrom).ThenByDescending(x => x.Record.Revision).FirstOrDefault();
            if (candidate is not null && (!candidate.Record.EffectiveTo.HasValue || candidate.Record.EffectiveTo > day))
            {
                if (!verified.TryGetValue(candidate.Record.Id, out salary))
                {
                    salary = await VerifyCompensationAsync(employeeId, candidate, salaryRows, ct);
                    verified.Add(candidate.Record.Id, salary);
                }
            }
            var fingerprint = Hash(new { source.Fingerprint, Policy = policy.Fingerprint, Compensation = salary?.Fingerprint });
            manifestDays.Add(new(day, state, fingerprint, source.Snapshot, time, salary, policy,
                roster.BranchId, source.Snapshot?.OfficeSource));
            inputs.Add(new(day, state, policy.PolicyVersionId, policy.State == "effective" ? policy.Rules : null,
                source.Snapshot?.TimeInput, time, salary, fingerprint, SourceIds(source.Snapshot)));
        }
        var result = AttendancePayrollInputRules.Calculate(monthStart, inputs);
        IReadOnlyList<AttendancePayrollConfirmedOrdinaryDay>? confirmed = null;
        var priorDates = result.Issues.Where(x => x.Code == "prior_deduction_reconciliation_required")
            .SelectMany(x => x.SourceDates ?? []).Where(x => x < monthStart).Distinct().Order().ToArray();
        if (priorDates.Length != 0 && reconciliation is not null)
        {
            var retained = await reconciliation.ReadAsync(employeeId, monthStart, priorDates, ct);
            if (retained.Days.Count > AttendancePayrollInputRules.MaximumDays ||
                retained.Days.Any(x => !priorDates.Contains(x.BusinessDate)) ||
                retained.Days.Select(x => x.BusinessDate).Distinct().Count() != retained.Days.Count ||
                !AttendanceScopeEvaluation.IsValid(retained.OriginalScopes) ||
                retained.OriginalScopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId != employeeId))
                throw AttendanceOperation.Conflict("prior_deduction_reconciliation_contract_invalid");
            confirmed = retained.Days;
            scopes = scopes.Concat(retained.OriginalScopes).DistinctBy(x => JsonSerializer.Serialize(x))
                .OrderBy(x => JsonSerializer.Serialize(x), StringComparer.Ordinal).ToArray();
            if (!AttendanceScopeEvaluation.IsValid(scopes)) throw AttendanceOperation.Conflict("payroll_source_scope_conflict");
            result = AttendancePayrollInputRules.Calculate(monthStart, inputs, confirmed);
            var issues = result.Issues.Concat(retained.Issues).DistinctBy(x => (x.Code, x.BusinessDate))
                .OrderBy(x => x.BusinessDate).ThenBy(x => x.Code, StringComparer.Ordinal).ToArray();
            result = result with { State = issues.Length == 0 ? "ready" : "unavailable", Issues = issues };
        }
        var manifest = new AttendancePayrollInputManifest(1, employeeId, monthStart, monthStart.AddMonths(1),
            range.From, range.To, AttendancePayrollInputRules.VersionFor(manifestDays.Select(x=>x.Compensation)), range.Population.EvidenceFingerprint, scopes, manifestDays, confirmed);
        var digest = Hash(new { Manifest = manifest, Result = result });
        return new(employeeId, monthStart, monthStart.AddMonths(1), range.From, range.To,
            result.State, digest, result.Lines, result.Issues, manifest);
    }

    private async Task<(DateOnly From, DateOnly To, MonthLockPopulation Population)> RangeAsync(Guid employeeId, DateOnly month, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || month.Day != 1 || month.Year is < 2 or > 9998)
            throw AttendanceOperation.Invalid("invalid_payroll_input_month");
        // The earliest of calendar-year/fiscal-year starts also contains all month/week starts.
        var from = new DateOnly(month.Month < 7 ? month.Year - 1 : month.Year, month.Month < 7 ? 7 : 1, 1);
        var end = month.AddMonths(1);
        var population = await repository.CoverageAsync(employeeId, from, end, ct);
        if (population.Issues.Any(x => x.BusinessDate is null) || population.Cells.Count == 0 || population.Cells.Count > AttendancePayrollInputRules.MaximumDays ||
            population.Cells.Any(x => x.EmployeeId != employeeId || x.BusinessDate < from || x.BusinessDate >= end))
            throw AttendanceOperation.Conflict(population.Issues.FirstOrDefault()?.Code ?? "payroll_employment_coverage_unavailable");
        var requiredFrom = month;
        foreach (var cell in population.Cells.Where(x => x.BusinessDate >= month).OrderBy(x => x.BusinessDate))
        {
            var policy = await policies.ResolveAsync(employeeId, cell.BusinessDate, ct);
            var valid = Cosmix.Domain.HR.Policies.AttendancePolicyRulesCodec.Validate(policy.Rules);
            if (policy.State != "effective" || !valid.IsValid) continue;
            var late = valid.Rules!.LateDeduction!;
            if (late.Enabled == false) continue;
            var period = AttendanceDailyTimeCalculation.LatePeriod(cell.BusinessDate, late.ResetPeriod!.Value, late.WeekStartsOn);
            if (period.From < requiredFrom) requiredFrom = period.From;
        }
        var cells = population.Cells.Where(x => x.BusinessDate >= requiredFrom).OrderBy(x => x.BusinessDate).ToArray();
        var relevantIssues = population.Issues.Where(x => x.BusinessDate is null || x.BusinessDate >= requiredFrom).ToArray();
        if (relevantIssues.Length != 0 || cells.Length == 0)
            throw AttendanceOperation.Conflict(relevantIssues.FirstOrDefault()?.Code ?? "payroll_month_has_no_employment");
        var bounded = new MonthLockPopulation(cells, Hash(new { population.EvidenceFingerprint, requiredFrom, cells }), []);
        return (cells[0].BusinessDate, cells[^1].BusinessDate, bounded);
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> ScopesAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var scopes = await summaries.ResourceScopesAsync(employeeId, from, to, ct);
        if (!AttendanceScopeEvaluation.IsValid(scopes) || scopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId != employeeId))
            throw AttendanceOperation.Conflict("payroll_source_scope_conflict");
        return scopes.Select(x => new AttendanceResourceScope(x.Alternatives.Distinct().OrderBy(r => r.EmployeeId)
            .ThenBy(r => r.DepartmentId).ThenBy(r => r.TeamId).ThenBy(r => r.SiteId).ThenBy(r => r.ProjectId).ToArray()))
            .OrderBy(x => JsonSerializer.Serialize(x), StringComparer.Ordinal).ToArray();
    }

    private async Task<AttendancePayrollCompensation> VerifyCompensationAsync(Guid employeeId,
        AttendancePayrollCompensationSource source, IReadOnlyList<AttendancePayrollCompensationSource> history, CancellationToken ct)
    {
        var row = source.Record; var ids = new List<Guid> { row.Id }; string evidence;
        if (row.ApprovalRequestId is Guid approvalId)
        {
            var request = source.Request;
            var decision = source.Decisions.Count == 1 ? source.Decisions[0] : null;
            if (request is null || request.Id != approvalId || request.SubjectId != employeeId ||
                request.RequestType != EmployeeCompensationService.RequestType || request.Stage != 1 ||
                request.State != ApprovalState.Approved || decision is null || decision.ApprovalRequestId != approvalId ||
                decision.Decision != ApprovalState.Approved || decision.ActorUserId != row.RecordedByUserId ||
                decision.ActorUserId == request.RequesterUserId || request.ClosedAt != decision.DecidedAt ||
                decision.DecidedAt != row.RecordedAt || decision.DecidedAt < request.RequestedAt || decision.ProtectedChangeReference is null)
                throw AttendanceOperation.Conflict("payroll_compensation_approval_conflict");
            var proposal = await operation.ReadPrivateAsync<CompensationChangeSnapshot>(request.SnapshotPrivateReference,
                EmployeeHiringService.Purpose(employeeId, "compensation"), ct);
            var change = await operation.ReadPrivateAsync<CompensationChangeProof>(decision.ProtectedChangeReference,
                EmployeeHiringService.Purpose(employeeId, "compensation"), ct);
            var previous = history.SingleOrDefault(x => x.Record.Id == row.SupersedesCompensationRecordId)?.Record;
            if (proposal.SchemaVersion != 1 || proposal.EmployeeId != employeeId || proposal.Proposed != Terms(row) ||
                proposal.EffectiveFrom != row.EffectiveFrom || proposal.Before != (previous is null ? null : Entry(previous)) ||
                change.Before != proposal.Before || change.After != Entry(row))
                throw AttendanceOperation.Conflict("payroll_compensation_protected_evidence_conflict");
            ids.Add(request.Id); ids.Add(decision.Id);
            if (previous is not null) ids.Add(previous.Id);
            evidence = Hash(new { proposal, change, RequestId = request.Id, DecisionId = decision.Id, decision.DecidedAt, decision.ActorUserId });
        }
        else
        {
            var hiring = await repository.HiringAsync(employeeId, ct);
            if (row.Revision != 1 || row.SupersedesCompensationRecordId is not null || hiring is null ||
                hiring.Submission.EmployeeId != employeeId || hiring.Decision.EmployeeId != employeeId ||
                hiring.Decision.SubmissionId != hiring.Submission.Id || hiring.Decision.Decision != OnboardingDecision.Approve ||
                hiring.Decision.Provenance != OnboardingProvenance.HrWorkflow || hiring.Submission.Provenance != hiring.Decision.Provenance ||
                hiring.Decision.ActorUserId is null || hiring.Decision.ActorUserId == hiring.Submission.SubmittedByUserId ||
                hiring.Decision.ActorRoleId is null || hiring.Decision.ActorRoleId == Guid.Empty || hiring.Submission.Version < 1 ||
                hiring.Decision.DecidedAt < hiring.Submission.SubmittedAt ||
                hiring.Decision.ActorUserId.ToString() != row.RecordedByUserId || hiring.Decision.DecidedAt != row.RecordedAt)
                throw AttendanceOperation.Conflict("payroll_initial_compensation_provenance_unavailable");
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(hiring.Submission.SnapshotStorageKey);
            if (reference is null || reference.ObjectVersion != hiring.Submission.SnapshotObjectVersion ||
                reference.Sha256 != hiring.Submission.SnapshotHash || reference.KeyReference != hiring.Submission.EncryptionKeyReference ||
                hiring.Submission.SnapshotSchemaVersion != 1)
                throw AttendanceOperation.Conflict("payroll_initial_compensation_provenance_unavailable");
            var snapshot = await operation.ReadPrivateAsync<EmployeeHiringService.HiringSnapshot>(hiring.Submission.SnapshotStorageKey,
                EmployeeHiringService.Purpose(employeeId, "submission"), ct);
            var financial = snapshot.Financial;
            if (snapshot.EmployeeId != employeeId || snapshot.DraftRevision != hiring.Submission.DraftRevision || financial is null ||
                financial.BaseSalary != row.BaseAmount || financial.GrossSalary != row.GrossAmount || financial.Currency != row.Currency ||
                financial.WageBasis != row.WageBasis || financial.PaymentMethod != row.PaymentMethod || financial.EffectiveFrom != row.EffectiveFrom)
                throw AttendanceOperation.Conflict("payroll_initial_compensation_protected_evidence_conflict");
            ids.Add(hiring.Submission.Id); ids.Add(hiring.Decision.Id);
            // Do not retain unrelated hiring/private form payload in the payroll manifest.
            evidence = Hash(new { hiring.Submission.Id, hiring.Submission.SnapshotHash, DecisionId = hiring.Decision.Id, Terms = Terms(row) });
        }
        var next = history.Where(x => x.Record.EffectiveFrom > row.EffectiveFrom).MinBy(x => x.Record.EffectiveFrom)?.Record.EffectiveFrom;
        var end = row.EffectiveTo;
        if (next.HasValue && (!end.HasValue || next < end)) end = next;
        return new(row.Id, row.BaseAmount, row.GrossAmount, row.Currency, row.WageBasis, row.EffectiveFrom, end,
            Hash(new { Record = Entry(row), End = end, evidence }), ids.Order().ToArray());
    }
    internal static IReadOnlyList<Guid> SourceIds(AttendanceCalculationSnapshot? snapshot)
    {
        if (snapshot is null) return [];
        return new Guid?[] { snapshot.Policy.PolicyVersionId, snapshot.Roster.AssignmentId, snapshot.Roster.ShiftId,
                snapshot.Roster.ShiftVersionId, snapshot.Roster.SiteId, snapshot.Employment.AssignmentId }
            .Where(x => x.HasValue).Select(x => x!.Value)
            .Concat(snapshot.Policy.AssignmentIds).Concat(snapshot.Sessions.Select(x => x.Id))
            .Concat(snapshot.Punches.Select(x => x.Id)).Concat(snapshot.Punches.Where(x => x.AssessmentId.HasValue).Select(x => x.AssessmentId!.Value))
            .Concat(snapshot.Corrections.SelectMany(x => new[] { x.CorrectionId, x.DecisionId, x.SessionRevisionId }))
            .Concat(snapshot.Overtime.Select(x => x.RequestId)).Concat(snapshot.Overtime.Where(x => x.DecisionId.HasValue).Select(x => x.DecisionId!.Value))
            .Concat(snapshot.Leave.Effects.SelectMany(x => new[] { x.LeaveRequestId, x.AllocationId, x.ApprovalRequestId, x.ApprovalDecisionId }))
            .Concat(snapshot.Punches.SelectMany(x => new Guid?[] { x.SiteId, x.EmployeeAssignmentId, x.ShiftVersionId,
                x.SiteRosterAssignmentId, x.TemporaryShiftChangeId, x.HolidayCalendarId }).Where(x => x.HasValue).Select(x => x!.Value))
            .Concat(snapshot.Leave.Effects.SelectMany(x => new Guid?[] { x.PolicyVersionId, x.ShiftVersionId }).Where(x => x.HasValue).Select(x => x!.Value))
            .Concat(snapshot.OfficeSource is not { } office ? [] : new Guid?[] { office.BranchId, office.OfficeRosterAssignmentId,
                office.EmployeeAssignmentId, office.OfficeGeofenceVersionId, office.ShiftVersionId, office.HolidayCalendarId }
                .Where(x => x.HasValue).Select(x => x!.Value))
            .Distinct().Order().ToArray();
    }
    private static CompensationTerms Terms(CompensationRecord row) => new(row.BaseAmount, row.GrossAmount, row.Currency, row.WageBasis, row.PaymentMethod);
    private static CompensationEntry Entry(CompensationRecord row) => new(row.Id, row.Revision, row.SupersedesCompensationRecordId,
        Terms(row), row.EffectiveFrom, row.EffectiveTo, row.EffectiveTo, row.ApprovalRequestId, row.RecordedByUserId, row.RecordedAt);
    private sealed record CompensationChangeProof([property: JsonPropertyName("before")] CompensationEntry? Before,
        [property: JsonPropertyName("after")] CompensationEntry After);
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
