using System.Globalization;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Allowances;
using Cosmix.Application.HR.AdvancesLoans;
using Cosmix.Application.HR.Reports;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.AdvancesLoans;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Domain.HR.PayrollHandoff;

namespace Cosmix.Application.HR.PayrollHandoff;

/// <summary>Payroll handoff authority. A locked attendance month is projected into frozen, signed
/// money lines; preparing freezes them, locking approves them with a separate actor and records
/// allowance settlement scheduling, and processing only records Accounts evidence. No code in this
/// module moves money.</summary>
public sealed partial class PayrollHandoffService(AttendanceOperation operation, IPayrollHandoffRepository repository,
    IMonthLockRepository months, IAttendancePayrollInputLedger ledger, IAllowanceRepository allowances,
    IAllowanceSettlementStore settlements, IAccountAccessReader access, IPrivateFileStore files,
    IMonthLockSnapshotReader? lockedMonths = null, IAdvanceLoanRepository? loans = null,
    IAttendancePayrollFinancialMonthReader? financialAdjustments = null, DeploymentService? deployments = null)
{
    public const string LockRequestType = "payroll-handoff.lock";
    public const string Currency = "PKR";
    private const string OvertimeSourceType = "attendance.overtime";
    private const string AllowanceSourceType = "attendance.allowance";
    private const string DeductionSourceType = "attendance.payroll-input";
    private const string LoanSourceType = "hr.advance-loan.schedule";
    private const string FuelSourceType = "hr.vehicle-fuel.claim";
    private const string FinancialAdjustmentSourceType = "attendance.payroll-financial-adjustment";
    private static readonly TimeSpan PakistanOffset = TimeSpan.FromHours(5);

    public Task<PayrollHandoffPreview> PreviewAsync(int year, int month, AuthRequestContext context, CancellationToken ct)
    {
        var (start, end) = MonthLockCoverage.Month(year, month);
        return operation.ReadAsync(PayrollHandoffPermissions.Read, "attendance.payroll-handoff.preview", null, context,
            async (actor, token) =>
            {
                var projected = await ProjectAsync(start, end, actor, token);
                return Map(start, end, projected);
            }, ct);
    }

    public Task<PayrollHandoffPage> BatchesAsync(int page, int pageSize, AuthRequestContext context, CancellationToken ct)
    {
        if (page is < 1 or > 4096 || pageSize is < 1 or > 100) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(PayrollHandoffPermissions.Read, "attendance.payroll-handoff.batches", null, context,
            async (_, token) =>
            {
                var rows = await repository.BatchesAsync(page, pageSize, token);
                return new PayrollHandoffPage(rows.Items.Select(x => Map(x, x.Lines.ToArray())).ToArray(), page,
                    pageSize, rows.TotalCount > page * pageSize);
            }, ct);
    }

    public Task<PayrollHandoffBatchDetail> DetailAsync(Guid id, AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(PayrollHandoffPermissions.Read, "attendance.payroll-handoff.batch", null, context,
            async (_, token) =>
            {
                var batch = await repository.FindBatchAsync(id, false, token) ??
                    throw AttendanceOperation.Missing("payroll_handoff_batch_not_found");
                return Detail(batch, Map(await repository.ProcessingAsync(id, token)));
            }, ct);
    }

    public Task<PayrollHandoffBankAdvice> BankAdviceAsync(Guid id, AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(PayrollHandoffPermissions.Read, "attendance.payroll-handoff.bank-advice", null,
            context, async (actor, token) =>
            {
                var batch = await repository.FindBatchAsync(id, false, token) ??
                    throw AttendanceOperation.Missing("payroll_handoff_batch_not_found");
                var processing = await repository.ProcessingAsync(id, token);
                if (batch.LockedAt is null && processing.Count == 0)
                    throw AttendanceOperation.Conflict("payroll_handoff_batch_not_ready");
                await ValidateFrozenLoanBalancesAsync(batch, token);
                await ValidateFrozenFinancialAdjustmentsAsync(batch, actor, token);

                var groups = batch.Lines.GroupBy(x => x.EmployeeId).OrderBy(x => x.Key).ToArray();
                var names = await repository.EmployeeNamesAsync(groups.Select(x => x.Key).ToArray(), token);
                var output = new StringBuilder();
                output.AppendLine(string.Join(',', BankAdviceHeaders.Select(HrReportCsvExporter.Cell)));
                foreach (var group in groups)
                {
                    var row = new[]
                    {
                        group.Key.ToString(),
                        names.TryGetValue(group.Key, out var employeeName) ? employeeName : string.Empty,
                        Money(Component(group, PayrollComponentKind.BaseCompensation)),
                        Money(Component(group, PayrollComponentKind.AttendanceDeduction)),
                        Money(Component(group, PayrollComponentKind.Overtime)),
                        Money(Component(group, PayrollComponentKind.Allowance)),
                        Money(Component(group, PayrollComponentKind.LoanRepayment)),
                        Money(Component(group, PayrollComponentKind.FuelClaim)),
                        Money(Component(group, PayrollComponentKind.OtherApprovedAdjustment)),
                        Money(group.Sum(x => x.Amount)),
                        batch.Currency,
                        batch.BatchReference,
                        batch.PeriodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        batch.PeriodEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    };
                    output.AppendLine(string.Join(',', row.Select(HrReportCsvExporter.Cell)));
                }
                return new PayrollHandoffBankAdvice(batch.BatchReference, actor.Now, groups.Length,
                    $"bank-advice-{batch.BatchReference}.csv", output.ToString());
            }, ct);
    }

    public Task<PayrollHandoffBatchDetail> PrepareAsync(PreparePayrollHandoffRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (request is null || request.ExpectedPreviewFingerprint is not { Length: 64 } ||
            !MonthLockEvidence.Hash(request.ExpectedPreviewFingerprint))
            throw AttendanceOperation.Invalid("invalid_payroll_handoff_request");
        var (start, end) = MonthLockCoverage.Month(request.Year, request.Month);
        return operation.WriteAsync(PayrollHandoffPermissions.Prepare, "attendance.payroll-handoff.prepare", null,
            key, new { request.Year, request.Month, request.ExpectedPreviewFingerprint }, request.Reason, context,
            async (actor, token) =>
            {
                if (await repository.PeriodPreparedAsync(start, end, token))
                    throw AttendanceOperation.Conflict("payroll_handoff_period_already_prepared");
                var projected = await ProjectAsync(start, end, actor, token);
                if (!string.Equals(projected.Fingerprint, request.ExpectedPreviewFingerprint, StringComparison.Ordinal))
                    throw AttendanceOperation.Conflict("payroll_handoff_preview_changed");
                if (projected.Lines.Count == 0) throw AttendanceOperation.Conflict("payroll_handoff_lines_unavailable");
                var batch = new PayrollHandoffBatch(BatchReference(start), start, end, Currency,
                    actor.Identity.AccountId.ToString(), actor.Now);
                var trace = TraceBase(context.CorrelationId, batch.Id);
                for (var index = 0; index < projected.Lines.Count; index++)
                {
                    var projectedLine = projected.Lines[index];
                    var line = projectedLine.Line;
                    var reference = await LineEvidenceAsync(batch.Id, projected.Lock.LockEventId, line,
                        projectedLine.SourceEvidenceReference, actor, token);
                    if (!Enum.TryParse<PayrollComponentKind>(line.Component, out var component))
                        throw AttendanceOperation.Conflict("payroll_handoff_component_invalid");
                    batch.Lines.Add(new PayrollHandoffLine(batch.Id, line.EmployeeId, component, line.SourceType,
                        line.SourceId, line.SourceVersion, projected.Lock.LockEventId, line.Amount, line.Currency,
                        line.PolicyVersionId, line.ApprovalRequestId, reference, $"{trace}:{index:D3}"));
                }
                var rechecked = await ProjectAsync(start, end, actor, token);
                if (rechecked.Fingerprint != projected.Fingerprint)
                    throw AttendanceOperation.Conflict("payroll_handoff_preview_changed");
                repository.Add(batch);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<PayrollHandoffBatchDetail>(batch.Id, null, Detail(batch, []));
            }, ct, beforeReplay: async (actor, token) => { _ = await ReadLockedMonthAsync(start, end, actor, token); });
    }

    /// <summary>Freeze a prepared batch through a second, separate approval and schedule every
    /// approved, unsettled allowance line into <see cref="ClaimSettlementState.InPayrollHandoff"/>.</summary>
    public Task<PayrollHandoffBatchDetail> LockAsync(Guid id, LockPayrollHandoffRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty) throw AttendanceOperation.Invalid();
        if (request is null) throw AttendanceOperation.Invalid("invalid_payroll_handoff_request");
        var expected = SchedulingOperation.Version(request.ExpectedVersion);
        return operation.WriteAsync(PayrollHandoffPermissions.Lock, "attendance.payroll-handoff.lock", null, key,
            new { id, request }, request.Reason, context, async (actor, token) =>
            {
                var batch = await repository.FindBatchAsync(id, true, token) ??
                    throw AttendanceOperation.Missing("payroll_handoff_batch_not_found");
                repository.ExpectVersion(batch, expected);
                if (batch.LockedAt.HasValue) throw AttendanceOperation.Conflict("payroll_handoff_batch_locked");
                RequireSeparateActor(batch.PreparedByUserId, actor, "payroll_handoff_self_lock_forbidden");
                await ValidateFrozenLoanBalancesAsync(batch, token);
                await ValidateFrozenFinancialAdjustmentsAsync(batch, actor, token);
                var before = Detail(batch, Map(await repository.ProcessingAsync(id, token)));
                var reference = AttendanceOperation.Reference(await files.WriteAsync(
                    JsonSerializer.SerializeToUtf8Bytes(new PayrollHandoffLockEvidence(1, batch.Id, batch.BatchReference,
                        batch.PeriodStart, batch.PeriodEnd, batch.Lines.Count, actor.Identity.AccountId,
                        request.Reason.Trim(), actor.Now)), LockPurpose(batch.Id), token));
                var approval = new ApprovalRequest(LockRequestType, batch.Id, batch.PreparedByUserId, reference, 1, actor.Now);
                var decision = approval.Decide(ApprovalState.Approved, actor.Identity.AccountId.ToString(),
                    actor.RoleCode, request.Reason.Trim(), actor.Now);
                batch.Lock(approval.Id, actor.Identity.AccountId.ToString(), actor.Now);
                foreach (var line in batch.Lines.Where(x => x.Component == PayrollComponentKind.Allowance).OrderBy(x => x.Id))
                    await ScheduleAllowanceAsync(batch.Id, line, actor, token);
                repository.Add(approval, decision);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<PayrollHandoffBatchDetail>(batch.Id, before, Detail(batch, []));
            }, ct);
    }

    /// <summary>Append Accounts processing evidence for a locked batch. It confirms a handoff the
    /// approver already froze; it never reopens lines and never disburses a salary.</summary>
    public Task<PayrollHandoffBatchDetail> ProcessingAsync(Guid id, ProcessPayrollHandoffRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty) throw AttendanceOperation.Invalid();
        if (request is null || string.IsNullOrWhiteSpace(request.ExternalProcessingReference) ||
            request.ExternalProcessingReference.Trim().Length > 150)
            throw AttendanceOperation.Invalid("invalid_payroll_handoff_request");
        return operation.WriteAsync(PayrollHandoffPermissions.Process, "attendance.payroll-handoff.process", null,
            key, new { id, request }, request.Reason, context, async (actor, token) =>
            {
                var batch = await repository.FindBatchAsync(id, false, token) ??
                    throw AttendanceOperation.Missing("payroll_handoff_batch_not_found");
                if (batch.LockedAt is null || batch.ApprovalRequestId is not Guid approvalId)
                    throw AttendanceOperation.Conflict("payroll_handoff_batch_not_locked");
                var deciders = await repository.DecidersAsync(approvalId, token);
                var account = actor.Identity.AccountId.ToString();
                var allowed = deciders.Count > 0
                    ? Guid.TryParse(account, out var me) && deciders.Contains(me)
                    : !string.Equals(batch.PreparedByUserId, account, StringComparison.Ordinal);
                if (!allowed) throw new AuthenticationFailure("payroll_handoff_processing_actor_forbidden", 403);
                var reference = request.ExternalProcessingReference.Trim();
                if (await repository.ProcessingReferenceExistsAsync(id, reference, token))
                    throw AttendanceOperation.Conflict("payroll_handoff_processing_reference_exists");
                var receipt = AttendanceOperation.Reference(await files.WriteAsync(
                    JsonSerializer.SerializeToUtf8Bytes(new PayrollHandoffProcessingEvidence(1, batch.Id,
                        batch.BatchReference, batch.Lines.Count, reference, actor.Identity.AccountId, actor.Now)),
                    ProcessingPurpose(batch.Id), token));
                // Processing describes the handoff, not an exact confirmed payroll posting.
                // Loan balances change only through a separately evidenced repayment workflow.
                repository.Add(new PayrollHandoffProcessingRecord(id, reference, receipt, account, actor.Now));
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<PayrollHandoffBatchDetail>(id, null,
                    Detail(batch, Map(await repository.ProcessingAsync(id, token))));
            }, ct);
    }

    private async Task<(MonthLockExisting Existing, MonthLockSnapshot Snapshot)> ReadLockedMonthAsync(
        DateOnly start, DateOnly end, AttendanceActor actor, CancellationToken ct)
    {
        var existing = await months.FindAsync(start, end, ct);
        if (existing is null || !existing.Period.IsLocked || existing.Event is null || !existing.Event.IsLocked ||
            existing.Period.PeriodStart != start || existing.Period.PeriodEndExclusive != end)
            throw AttendanceOperation.Conflict("payroll_handoff_month_not_locked");
        if (lockedMonths is null) throw AttendanceOperation.Conflict("payroll_handoff_lock_reader_unavailable");
        var snapshot = await lockedMonths.ReadRetainedAsync(existing, actor, ct);
        MonthLockEvidence.Validate(existing, snapshot);
        if (snapshot.SchemaVersion is not (3 or 4))
            throw AttendanceOperation.Conflict("payroll_handoff_retained_membership_unavailable");
        return (existing, snapshot);
    }

    private async Task<Projection> ProjectAsync(DateOnly start, DateOnly end, AttendanceActor actor, CancellationToken ct)
    {
        var (existing, snapshot) = await ReadLockedMonthAsync(start, end, actor, ct);
        var population = existing.Summaries.Select(x => x.EmployeeId).Distinct().OrderBy(x => x).ToArray();
        if (population.Length == 0 || population.Length > MonthLockCoverage.MaximumEmployees)
            throw AttendanceOperation.Conflict("payroll_handoff_population_unavailable");
        var lockEvent = existing.Event!;
        var lines = new List<ProjectedLine>();
        var skipped = new List<PayrollHandoffSkippedSource>();

        foreach (var adjustment in await ReadFinancialAdjustmentsAsync(snapshot, actor, ct))
        {
            if (adjustment.Status == "posted")
            {
                skipped.Add(new("attendance_adjustment_already_posted", adjustment.EmployeeId,
                    FinancialAdjustmentSourceType, adjustment.AdjustmentId));
                continue;
            }
            if (adjustment.SignedAmount == 0) continue;
            // The adjustment ledger uses positive recovery; Finance lines use positive credit.
            lines.Add(new(new(adjustment.EmployeeId, PayrollComponentKind.OtherApprovedAdjustment.ToString(),
                FinancialAdjustmentSourceType, adjustment.AdjustmentId, adjustment.Revision, -adjustment.SignedAmount,
                adjustment.Currency, null, null), adjustment.EvidenceReference));
        }

        foreach (var member in snapshot.Overtime!)
        {
            var award = member.Award;
            lines.Add(new(new(award.EmployeeId, PayrollComponentKind.Overtime.ToString(), OvertimeSourceType,
                award.RequestId, 1, award.FinancialEvidence.Amount, award.FinancialEvidence.Currency,
                award.Inputs.Source.DurationPolicy.PolicyVersionId, award.ApprovalRequestId), member.DecisionEvidenceReference));
        }

        for (var skip = 0; ; skip += 100)
        {
            var claims = await repository.AllowancesAsync(population, start, end, skip, 100, ct);
            foreach (var claim in claims)
            {
                if (claim.Currency != Currency)
                    throw AttendanceOperation.Conflict("payroll_handoff_currency_conflict");
                if (claim.ClaimedAmount <= 0 || claim.SubmissionVersion < 1) continue;
                lines.Add(new(new(claim.EmployeeId, PayrollComponentKind.Allowance.ToString(),
                    AllowanceSourceType, claim.ClaimId, claim.SubmissionVersion, claim.ClaimedAmount,
                    claim.Currency, claim.PolicyVersionId, claim.ApprovalRequestId), claim.CalculationPrivateReference));
            }
            if (claims.Count < 100) break;
        }

        foreach (var member in snapshot.PayrollInputs!)
        {
            var employeeId = member.EmployeeId;
            var revision = await ledger.FindAsync(employeeId, member.SourceRevisionId, ct)
                ?? throw AttendanceOperation.Conflict("month_lock_payroll_revision_missing");
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(revision.ProtectedEvidenceReference);
            if (revision.EmployeeId != employeeId || revision.PeriodStart != start || revision.Revision != member.SourceRevision ||
                revision.InputFingerprint != member.InputFingerprint || revision.CalculationVersion != member.CalculationVersion ||
                reference?.Sha256 != member.SourceEvidenceSha256)
                throw AttendanceOperation.Conflict("month_lock_payroll_revision_conflict");
            var evidence = await AttendancePayrollInputService.ReadValidatedEvidenceAsync(revision, actor,
                AttendancePayrollInputPermissions.Read, operation, access, ct);
            if (evidence.Calculation.State != "ready" || evidence.Calculation.Issues.Count != 0)
                throw AttendanceOperation.Conflict("attendance_deduction_amount_source_unavailable");
            var total = evidence.Calculation.Lines.Sum(x => x.Amount);
            if (evidence.Calculation.Lines.Any(x => x.Amount < 0))
                throw AttendanceOperation.Conflict("attendance_deduction_amount_source_unavailable");
            if (total <= 0) continue;
            lines.Add(new(new(employeeId, PayrollComponentKind.AttendanceDeduction.ToString(), DeductionSourceType,
                revision.Id, revision.Revision, -total, Currency, null, null), revision.ProtectedEvidenceReference));
        }

        var loanSchedules = await repository.LoanSchedulesAsync(population, start, end, ct);
        var paidSchedules = await ValidateLoanDebitsAsync(loanSchedules.Select(x =>
            new LoanDebit(x.EmployeeId, x.ScheduleEntryId, x.Sequence, x.Amount, x.Currency, x.ApprovalRequestId)).ToArray(), start, end, ct);
        foreach (var loan in loanSchedules)
        {
            if (loan.OtherApprovedAmount != 0)
                throw AttendanceOperation.Conflict("interest_free_loan_required");
            if (loan.Currency != Currency)
                throw AttendanceOperation.Conflict("payroll_handoff_currency_conflict");
            if (paidSchedules.Contains(loan.ScheduleEntryId))
            {
                skipped.Add(new("loan_installment_already_repaid", loan.EmployeeId, LoanSourceType, loan.ScheduleEntryId));
                continue;
            }
            if (loan.Amount <= 0) continue;
            lines.Add(new(new(loan.EmployeeId, PayrollComponentKind.LoanRepayment.ToString(),
                LoanSourceType, loan.ScheduleEntryId, loan.Sequence, -loan.Amount,
                loan.Currency, null, loan.ApprovalRequestId), loan.ApprovedTermsPrivateReference));
        }

        var fuelClaims = await repository.FuelClaimsAsync(population, PeriodStartUtc(start), PeriodStartUtc(end), ct);
        foreach (var fuel in fuelClaims)
        {
            if (fuel.Currency != Currency)
                throw AttendanceOperation.Conflict("payroll_handoff_currency_conflict");
            if (fuel.ClaimedAmount <= 0) continue;
            lines.Add(new(new(fuel.EmployeeId, PayrollComponentKind.FuelClaim.ToString(),
                FuelSourceType, fuel.ClaimId, 1, fuel.ClaimedAmount,
                fuel.Currency, fuel.PolicyVersionId, fuel.ApprovalRequestId), fuel.CalculationPrivateReference));
        }

        var ordered = lines.OrderBy(x => x.Line.EmployeeId).ThenBy(x => x.Line.Component, StringComparer.Ordinal)
            .ThenBy(x => x.Line.SourceType, StringComparer.Ordinal).ThenBy(x => x.Line.SourceId)
            .ThenBy(x => x.Line.SourceVersion).ToArray();
        var fingerprint = MonthLockEvidence.Fingerprint(new
        {
            periodStart = start, periodEndExclusive = end, attendancePeriodLockEventId = lockEvent.Id,
            currency = Currency, retainedMembership = MonthLockEvidence.Fingerprint(snapshot),
            lines = ordered.Select(x => new { x.Line, x.SourceEvidenceReference }).ToArray()
        });
        return new(fingerprint, new(lockEvent.Id, lockEvent.AtUtc, population), ordered, skipped.ToArray());
    }

    private async Task<IReadOnlyList<AttendancePayrollFinancialMonthEntry>> ReadFinancialAdjustmentsAsync(
        MonthLockSnapshot snapshot, AttendanceActor actor, CancellationToken ct)
    {
        if (snapshot.SchemaVersion < 4) return [];
        if (financialAdjustments is null)
            throw AttendanceOperation.Conflict("payroll_handoff_financial_adjustment_reader_unavailable");
        var employees = snapshot.Summaries.Select(x => x.EmployeeId).Distinct().Order().ToArray();
        var current = await financialAdjustments.ReadAsync(snapshot.MonthStart, employees, actor, ct);
        MonthLockFinancialAdjustments.Validate(snapshot.MonthStart, employees, snapshot.PayrollInputs!, current);
        MonthLockFinancialAdjustments.RequireRetainedMatch(snapshot.FinancialAdjustments!, current);
        return current;
    }

    private async Task ValidateFrozenFinancialAdjustmentsAsync(PayrollHandoffBatch batch, AttendanceActor actor, CancellationToken ct)
    {
        var lines = batch.Lines.Where(x => x.SourceType == FinancialAdjustmentSourceType ||
            x.Component == PayrollComponentKind.OtherApprovedAdjustment).ToArray();
        if (lines.Length == 0) return;
        var (_, snapshot) = await ReadLockedMonthAsync(batch.PeriodStart, batch.PeriodEnd, actor, ct);
        var current = await ReadFinancialAdjustmentsAsync(snapshot, actor, ct);
        foreach (var line in lines)
        {
            var source = current.SingleOrDefault(x => x.AdjustmentId == line.SourceId);
            if (source is null || source.Status != "approved" || source.SignedAmount == 0 ||
                line.SourceType != FinancialAdjustmentSourceType || line.Component != PayrollComponentKind.OtherApprovedAdjustment ||
                line.EmployeeId != source.EmployeeId || line.PayrollHandoffBatchId != batch.Id ||
                line.SourceVersion != source.Revision || line.Amount != -source.SignedAmount || line.Currency != source.Currency ||
                line.AttendancePeriodLockEventId != snapshot.EventId || line.PolicyVersionId is not null || line.ApprovalRequestId is not null)
                throw AttendanceOperation.Conflict("payroll_handoff_financial_adjustment_source_changed");
            var evidence = await operation.ReadPrivateAsync<PayrollHandoffLineEvidence>(line.CalculationPrivateReference,
                LinePurpose(batch.Id), ct);
            if (evidence.SchemaVersion != 1 || evidence.BatchId != batch.Id || evidence.EmployeeId != line.EmployeeId ||
                evidence.Component != line.Component.ToString() || evidence.SourceType != line.SourceType ||
                evidence.SourceId != line.SourceId || evidence.SourceVersion != line.SourceVersion ||
                evidence.AttendancePeriodLockEventId != snapshot.EventId || evidence.Amount != line.Amount ||
                evidence.Currency != line.Currency || evidence.PolicyVersionId is not null || evidence.ApprovalRequestId is not null ||
                evidence.SourceEvidenceReference != source.EvidenceReference || evidence.PreparedAtUtc != batch.PreparedAt ||
                !Guid.TryParse(batch.PreparedByUserId, out var preparer) || evidence.ActorAccountId != preparer)
                throw AttendanceOperation.Conflict("payroll_handoff_financial_adjustment_evidence_invalid");
        }
        if (lines.Select(x => x.SourceId).Distinct().Count() != lines.Length)
            throw AttendanceOperation.Conflict("payroll_handoff_financial_adjustment_source_changed");
    }

    private async Task ScheduleAllowanceAsync(Guid batchId, PayrollHandoffLine line, AttendanceActor actor, CancellationToken ct)
    {
        var data = await allowances.FindAsync(line.EmployeeId, line.SourceId, ct) ??
            throw AttendanceOperation.Conflict("payroll_handoff_allowance_claim_missing");
        if (data.Claim.Kind == AllowanceKind.OutstationDaily)
        {
            if (deployments is null) throw AttendanceOperation.Conflict("deployment_workflow_unavailable");
            await deployments.ValidateAllowanceClaimAsync(data, ct);
        }
        if (data.Approval.State != ApprovalState.Approved || !data.Approval.ClosedAt.HasValue ||
            data.Approval.ClosedAt.Value > actor.Now || data.Decision is null ||
            data.Decision.ApprovalRequestId != data.Approval.Id || data.Decision.Decision != ApprovalState.Approved ||
            data.Decision.DecidedAt != data.Approval.ClosedAt || data.Decision.ActorUserId == data.Approval.RequesterUserId ||
            data.Claim.Kind != AllowanceKind.OutstationDaily && data.Decision.ProtectedChangeReference is not null ||
            data.Claim.SubmissionVersion < 1 || line.SourceVersion != data.Claim.SubmissionVersion ||
            line.SourceType != AllowanceSourceType || data.Claim.Id != line.SourceId || data.Claim.EmployeeId != line.EmployeeId)
            throw AttendanceOperation.Conflict("payroll_handoff_allowance_evidence_unavailable");
        if (data.Claim.SettlementState != ClaimSettlementState.NotScheduled)
            throw AttendanceOperation.Conflict("payroll_handoff_allowance_claim_already_settled");
        if (data.Claim.ClaimedAmount != line.Amount || data.Claim.PolicyVersionId != line.PolicyVersionId ||
            data.Claim.ApprovalRequestId != line.ApprovalRequestId || data.Claim.Currency != line.Currency)
            throw AttendanceOperation.Conflict("payroll_handoff_allowance_evidence_unavailable");
        await settlements.TrackApprovalEvidenceAsync(data.Approval.Id, ct);
        settlements.Add(data.Claim.RecordSettlement(data.Approval, ClaimSettlementState.InPayrollHandoff, line.Id,
            null, $"handoff:{batchId:N}:{line.SourceId:N}", actor.Identity.AccountId.ToString(), actor.Now));
    }

    private async Task<string> LineEvidenceAsync(Guid batchId, Guid lockEventId, PayrollHandoffPreviewLine line,
        string sourceEvidence, AttendanceActor actor, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new PayrollHandoffLineEvidence(1, batchId, line.EmployeeId,
            line.Component, line.SourceType, line.SourceId, line.SourceVersion, lockEventId, line.Amount,
            line.Currency, line.PolicyVersionId, line.ApprovalRequestId, sourceEvidence,
            actor.Identity.AccountId, actor.Now));
        return AttendanceOperation.Reference(await files.WriteAsync(bytes, LinePurpose(batchId), ct));
    }

    private static PayrollHandoffBatchDetail Detail(PayrollHandoffBatch batch,
        IReadOnlyList<PayrollHandoffProcessingRecordResponse> processing) =>
        new(Map(batch, batch.Lines.ToArray()), batch.Lines.OrderBy(x => x.EmployeeId).ThenBy(x => x.Component)
            .ThenBy(x => x.SourceType).ThenBy(x => x.SourceId).ThenBy(x => x.SourceVersion)
            .Select(Line).ToArray(), processing);

    private static PayrollHandoffBatchResponse Map(PayrollHandoffBatch batch, IReadOnlyList<PayrollHandoffLine> lines) =>
        new(batch.Id, batch.BatchReference, batch.PeriodStart, batch.PeriodEnd, batch.Currency,
            batch.LockedAt.HasValue ? "locked" : "prepared", batch.LinesRevision,
            Convert.ToBase64String(batch.RowVersion), batch.PreparedAt, batch.PreparedByUserId, batch.LockedAt,
            batch.LockedByUserId, batch.ApprovalRequestId, Totals(lines.Select(x => (x.Component.ToString(), x.Amount))),
            lines.Count);

    private static PayrollHandoffLineResponse Line(PayrollHandoffLine line) => new(line.Id, line.EmployeeId,
        line.Component.ToString(), line.SourceType, line.SourceId, line.SourceVersion,
        line.AttendancePeriodLockEventId, line.Amount, line.Currency, line.PolicyVersionId,
        line.ApprovalRequestId, line.TraceId);

    private static IReadOnlyList<PayrollHandoffProcessingRecordResponse> Map(
        IReadOnlyList<PayrollHandoffProcessingRecord> rows) => rows
        .OrderBy(x => x.ProcessedAt).ThenBy(x => x.Id)
        .Select(x => new PayrollHandoffProcessingRecordResponse(x.Id, x.ExternalProcessingReference,
            x.ProcessedByUserId, x.ProcessedAt)).ToArray();

    private static PayrollHandoffPreview Map(DateOnly start, DateOnly end, Projection projected) =>
        new(start, end, projected.Lock.LockEventId, projected.Lock.LockedAtUtc, projected.Lock.EmployeeIds.Count,
            projected.Lines.Select(x => x.Line).ToArray(), projected.Skipped,
            Totals(projected.Lines.Select(x => (x.Line.Component, x.Line.Amount))), Currency, projected.Fingerprint);

    /// <summary>Attendance deductions and loan repayments are negative signed money, so the net is a plain sum.</summary>
    private static PayrollHandoffTotals Totals(IEnumerable<(string Component, decimal Amount)> lines)
    {
        var overtime = lines.Where(x => x.Component == PayrollComponentKind.Overtime.ToString()).Sum(x => x.Amount);
        var allowance = lines.Where(x => x.Component == PayrollComponentKind.Allowance.ToString() || x.Component == PayrollComponentKind.FuelClaim.ToString()).Sum(x => x.Amount);
        var deduction = lines.Where(x => x.Component == PayrollComponentKind.AttendanceDeduction.ToString() || x.Component == PayrollComponentKind.LoanRepayment.ToString()).Sum(x => x.Amount);
        var adjustment = lines.Where(x => x.Component == PayrollComponentKind.OtherApprovedAdjustment.ToString()).Sum(x => x.Amount);
        return new(overtime, allowance, deduction, lines.Sum(x => x.Amount), adjustment);
    }

    private static readonly string[] BankAdviceHeaders =
    [
        "Employee ID", "Employee Name", "BaseCompensation", "AttendanceDeduction", "Overtime", "Allowance",
        "LoanRepayment", "FuelClaim", "OtherApprovedAdjustment", "Net Amount", "Currency", "Batch Reference",
        "Period Start", "Period End"
    ];

    private static decimal Component(IGrouping<Guid, PayrollHandoffLine> group, PayrollComponentKind component) =>
        group.Where(x => x.Component == component).Sum(x => x.Amount);

    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static void RequireSeparateActor(string preparedByUserId, AttendanceActor actor, string code)
    {
        if (string.Equals(preparedByUserId, actor.Identity.AccountId.ToString(), StringComparison.Ordinal) ||
            (Guid.TryParse(preparedByUserId, out var preparer) && preparer == actor.Identity.AccountId))
            throw new AuthenticationFailure(code, 403);
    }

    private static string BatchReference(DateOnly start) =>
        $"PAY-{start:yyyyMM}-{Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>The trace id must stay unique per line while remaining server-owned and bounded by
    /// the 100 character column.</summary>
    private static string TraceBase(string correlationId, Guid batchId)
    {
        var prefix = string.IsNullOrWhiteSpace(correlationId) ? batchId.ToString("N") : correlationId.Trim();
        return prefix.Length <= 96 ? prefix : prefix[..96];
    }

    private static DateTimeOffset PeriodStartUtc(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), PakistanOffset);

    private static string DecisionPurpose(Guid employeeId, Guid requestId) =>
        $"attendance:overtime:{employeeId:N}:{requestId:N}:decision";
    private static string LinePurpose(Guid batchId) => $"hr:payroll-handoff:{batchId:N}:line";
    private static string LockPurpose(Guid batchId) => $"hr:payroll-handoff:{batchId:N}:lock";
    private static string ProcessingPurpose(Guid batchId) => $"hr:payroll-handoff:{batchId:N}:processing";

    private sealed record ProjectedLine(PayrollHandoffPreviewLine Line, string SourceEvidenceReference);
    private sealed record LockIdentity(Guid LockEventId, DateTimeOffset LockedAtUtc, IReadOnlyList<Guid> EmployeeIds);
    private sealed record Projection(string Fingerprint, LockIdentity Lock, IReadOnlyList<ProjectedLine> Lines,
        IReadOnlyList<PayrollHandoffSkippedSource> Skipped);

    private sealed record PayrollHandoffLineEvidence(int SchemaVersion, Guid BatchId, Guid EmployeeId,
        string Component, string SourceType, Guid SourceId, int SourceVersion, Guid AttendancePeriodLockEventId,
        decimal Amount, string Currency, Guid? PolicyVersionId, Guid? ApprovalRequestId, string SourceEvidenceReference,
        Guid ActorAccountId, DateTimeOffset PreparedAtUtc);

    private sealed record PayrollHandoffLockEvidence(int SchemaVersion, Guid BatchId, string BatchReference,
        DateOnly PeriodStart, DateOnly PeriodEndExclusive, int LineCount, Guid ApproverAccountId, string Reason,
        DateTimeOffset LockedAtUtc);

    private sealed record PayrollHandoffProcessingEvidence(int SchemaVersion, Guid BatchId, string BatchReference,
        int LineCount, string ExternalProcessingReference, Guid ProcessedByAccountId, DateTimeOffset ProcessedAt);
}
