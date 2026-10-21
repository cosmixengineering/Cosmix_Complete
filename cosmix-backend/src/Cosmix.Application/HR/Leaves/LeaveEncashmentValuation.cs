using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.HR.Compensation;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.Leaves;

public sealed record EncashmentPolicySource(Guid PolicyId, Guid VersionId, string Schema, string RulesJson,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo, DateTimeOffset PublishedAt, string PublishedBy,
    Guid StaffCategoryId, DateOnly JoinedOn, IReadOnlyList<Guid> AssignmentIds);
public sealed record EncashmentLedgerSource(Guid Id, Guid LeaveTypeId, Guid PolicyVersionId, LeaveLedgerEntryType EntryType,
    int MinutesDelta, Guid? ReversesEntryId, string Key);
public sealed record EncashmentCalculation(int SchemaVersion, Guid EmployeeId, Guid RequesterAccountId,
    SubmitLeaveEncashmentRequest Request, DateOnly ValuationDate, DateTimeOffset CapturedAt,
    EncashmentPolicySource Policy, LeaveKind Kind, LeaveBalanceUsage Balance, IReadOnlyList<EncashmentLedgerSource> Ledger,
    CompensationEntry Salary, FinalizedKpiScore Kpi, string VerifiedKpiSourceFingerprint, IReadOnlyList<Guid> ResolvedDisputeIds,
    decimal Amount);

/// <summary>Uses the owning modules' verified sources inside AttendanceOperation's transaction. Grants no authority.</summary>
public sealed class LeaveEncashmentValuation(AttendanceOperation operation, ILeaveRepository leaves,
    ILeaveEncashmentRepository repository, IFinalizedKpiReader finalized, IEmployeeCompensationRepository compensation,
    IKpiDisputeRepository disputes, KpiDisputeEvidenceReader disputeEvidence, IKpiRepository kpiRequests, IKpiSourceReader kpiSources)
{
    public static string Purpose(Guid employee) => $"leave:encashment:{employee:N}";

    // The existing entity column is decimal(9,4). Exact KPI remains in the immutable calculation.
    public static decimal PersistedKpiPercent(decimal exactScore) => decimal.Round(exactScore, 4, MidpointRounding.ToEven);

    public async Task<EncashmentCalculation> CalculateAsync(Guid employee, Guid requester, SubmitLeaveEncashmentRequest request,
        DateTimeOffset now, CancellationToken ct, LeaveLedgerEntry? ownReservation = null, DateTimeOffset? submittedAt = null)
    {
        if (request.LeaveTypeId is null || request.PolicyVersionId is null)
            throw AttendanceOperation.Conflict("encashment_policy_selection_required");
        if (request.LeaveYearStart.Year is < 1901 or > 9997 || request.LeaveYearEnd != request.LeaveYearStart.AddYears(1))
            throw AttendanceOperation.Conflict("encashment_leave_year_invalid");
        var submitted = submittedAt ?? now;
        if (submitted > now) throw AttendanceOperation.Conflict("encashment_submission_evidence_invalid");
        var submissionDate = LeaveRules.LocalDate(submitted);
        if (request.LeaveYearEnd > submissionDate) throw AttendanceOperation.Conflict("encashment_completed_leave_year_required");
        var asOf = request.LeaveYearEnd.AddDays(-1);
        if (!await leaves.EmployeeExistsAsync(employee, ct)) throw AttendanceOperation.Conflict("leave_employee_inactive");
        var type = await leaves.TypeAsync(request.LeaveTypeId.Value, ct);
        if (type is null || !type.IsActive || !type.ReducesRegularBalance) throw AttendanceOperation.Conflict("encashment_leave_type_ineligible");
        var policy = await leaves.PolicyAsync(employee, type.Id, request.PolicyVersionId.Value, asOf, asOf, ct)
            ?? throw AttendanceOperation.Conflict("encashment_policy_unavailable");
        var version = policy.Version;
        if (!policy.ProvenanceValid || version.Id != request.PolicyVersionId || version.Authority != PolicyAuthority.DirectHr ||
            version.ApprovalRequestId is not null || version.Policy.Category != PolicyCategory.Leave || version.PublishedAt > now ||
            version.EffectiveFrom > asOf || version.EffectiveTo <= asOf)
            throw AttendanceOperation.Conflict("encashment_policy_unavailable");
        var settings = LeavePolicySettings.Parse(version.RulesJson, version.RulesSchemaVersion);
        var rules = settings.Encashment ?? throw AttendanceOperation.Conflict("encashment_policy_not_configured");
        if (LeavePolicySettings.Year(asOf, settings) != (request.LeaveYearStart, request.LeaveYearEnd))
            throw AttendanceOperation.Conflict("encashment_leave_year_invalid");
        if (submissionDate.DayNumber - request.LeaveYearEnd.DayNumber >= rules.RequestWindowDaysAfterYearEnd)
            throw AttendanceOperation.Conflict("encashment_submission_window_closed");
        if (!rules.EligibleKinds.Contains(type.Kind) || !settings.PaidKinds.Contains(type.Kind) ||
            !settings.EligibleStaffCategoryIds.Contains(policy.StaffCategoryId) || policy.JoinedOn is null ||
            !settings.ServiceEligible(policy.JoinedOn.Value, asOf) ||
            !(settings with { MinimumServiceMonths = rules.MinimumServiceMonths }).ServiceEligible(policy.JoinedOn.Value, asOf))
            throw AttendanceOperation.Conflict("encashment_policy_ineligible");
        if (policy.AssignmentIds is not { Count: > 0 } || policy.AssignmentIds.Any(x => x == Guid.Empty))
            throw AttendanceOperation.Conflict("encashment_assignment_evidence_unavailable");
        var ledger = await repository.LedgerAsync(employee, request.LeaveYearStart, request.LeaveYearEnd, ct);
        var usage = policy.Usage;
        if (ownReservation is not null)
        {
            // The current balance includes this request's reservation. Remove exactly that one
            // for revalidation, then require enough balance to put the same units back.
            if (!ledger.Any(x => x.Id == ownReservation.Id) || usage.Units is null || ownReservation.LeaveTypeId != type.Id)
                throw AttendanceOperation.Conflict("encashment_reservation_conflict");
            usage = new(usage.AnnualMinutes + ownReservation.MinutesDelta, usage.TypeMinutes + ownReservation.MinutesDelta,
                usage.Units.Append(new(ownReservation.MinutesDelta, settings.StandardDayMinutes, type.Kind)).ToArray()) { Credits = usage.Credits };
        }
        // Retained consumed-only offsets neutralize their exact debits, not new cash
        // capacity. Dropping them would incorrectly recover the benefit from base quota.
        usage = usage with { Credits = usage.Credits?.Where(x => x.Encashable || x.ConsumptionOffset).ToArray() };
        var typeLimit = type.Kind switch { LeaveKind.Sick => settings.SickDays, LeaveKind.Casual => settings.CasualDays, _ => settings.AnnualDays };
        if (LeaveTreatmentRules.ExceedsDays(usage, type.Kind, request.RequestedMinutes, settings.StandardDayMinutes, settings.AnnualDays, typeLimit))
            throw AttendanceOperation.Conflict("insufficient_leave_balance_for_encashment");
        var encashmentMinutes = ledger.Where(x => x.EntryType is LeaveLedgerEntryType.Reservation or LeaveLedgerEntryType.Encashment ||
            x.EntryType == LeaveLedgerEntryType.Release && ledger.Any(r => r.Id == x.ReversesEntryId && r.EntryType == LeaveLedgerEntryType.Reservation))
            .Sum(x => -(long)x.MinutesDelta) + (ownReservation?.MinutesDelta ?? 0);
        if (encashmentMinutes < 0 || encashmentMinutes + request.RequestedMinutes > rules.MaximumMinutesPerYear)
            throw AttendanceOperation.Conflict("encashment_annual_limit_exceeded");

        var kpi = await finalized.ReadAsync(employee, request.LeaveYearStart, ct)
            ?? throw AttendanceOperation.Conflict("encashment_finalized_kpi_required");
        KpiDisputeRules.Final(kpi, employee, request.LeaveYearStart);
        if (kpi.ToExclusive != request.LeaveYearEnd || kpi.FinalizedAtUtc > now || kpi.ExactScore < rules.MinimumKpiPercent)
            throw AttendanceOperation.Conflict("encashment_kpi_ineligible");
        var pending = await kpiRequests.PendingAsync(employee, ct);
        if (pending.Count > 128) throw AttendanceOperation.Conflict("kpi_revision_limit");
        foreach (var item in pending)
        {
            var prepared = await operation.ReadPrivateAsync<KpiRequestSnapshot>(item.SnapshotPrivateReference, AttendanceKpiService.RequestPurpose(employee), ct);
            if (prepared.EmployeeId != employee || prepared.Source is null) throw AttendanceOperation.Conflict("encashment_kpi_pending_evidence_conflict");
            if (prepared.Source.From < request.LeaveYearEnd && prepared.Source.ToExclusive > request.LeaveYearStart)
                throw AttendanceOperation.Conflict("encashment_kpi_review_pending");
        }
        var disputeRows = await disputes.YearAsync(employee, request.LeaveYearStart, null, null, ct);
        if (disputeRows.Count > KpiDisputeRules.YearLimit) throw AttendanceOperation.Conflict("kpi_dispute_history_limit");
        var verifiedDisputes = new List<KpiDisputeEvidence>();
        foreach (var row in disputeRows) verifiedDisputes.Add(await disputeEvidence.ReadAsync(employee, row.Id, ct));
        foreach (var item in verifiedDisputes)
        {
            var resolved = item.Response?.Outcome == KpiDisputeOutcome.ReviewedNoChange ||
                item.Response?.Outcome == KpiDisputeOutcome.CorrectionRequired && item.Amendment is not null && item.Amendment.AmendedFinal.Revision <= kpi.Revision ||
                item.Response?.Outcome == KpiDisputeOutcome.Returned && verifiedDisputes.Any(x => x.Draft.ResubmissionOf == item.Row.Request.Id);
            if (!resolved) throw AttendanceOperation.Conflict("encashment_kpi_dispute_unresolved");
        }
        var source = await kpiSources.ReadAsync(employee, asOf, ct);
        KpiDisputeRules.Source(source, kpi);
        if (!KpiDisputeRules.MatchesFinal(source, kpi)) throw AttendanceOperation.Conflict("encashment_kpi_source_changed");

        if (await compensation.HasConflictingHistoryAsync(employee, ct)) throw AttendanceOperation.Conflict("encashment_compensation_conflict");
        var window = await compensation.WindowAsync(employee, asOf, ct);
        var salary = window.Candidate;
        if (salary is null || salary.EmployeeId != employee || salary.EffectiveFrom > asOf || salary.EffectiveTo <= asOf ||
            salary.RecordedAt > now || salary.BaseAmount <= 0)
            throw AttendanceOperation.Conflict("encashment_compensation_unavailable");
        if (salary.WageBasis != WageBasis.Monthly || salary.Currency != rules.Currency)
            throw AttendanceOperation.Conflict("encashment_compensation_terms_ineligible");
        var effectiveTo = salary.EffectiveTo;
        if (window.Next is not null && (effectiveTo is null || window.Next.EffectiveFrom < effectiveTo)) effectiveTo = window.Next.EffectiveFrom;
        var retainedSalary = new CompensationEntry(salary.Id, salary.Revision, salary.SupersedesCompensationRecordId,
            new(salary.BaseAmount, salary.GrossAmount, salary.Currency, salary.WageBasis, salary.PaymentMethod),
            salary.EffectiveFrom, effectiveTo, salary.EffectiveTo, salary.ApprovalRequestId, salary.RecordedByUserId, salary.RecordedAt);
        return new(2, employee, requester, request, asOf, now,
            new(version.PolicyId, version.Id, version.RulesSchemaVersion, version.RulesJson, version.EffectiveFrom, version.EffectiveTo,
                version.PublishedAt, version.PublishedByUserId, policy.StaffCategoryId, policy.JoinedOn.Value, policy.AssignmentIds),
            type.Kind, usage, ledger.Select(x => new EncashmentLedgerSource(x.Id, x.LeaveTypeId, x.PolicyVersionId, x.EntryType, x.MinutesDelta, x.ReversesEntryId, x.IdempotencyKey)).ToArray(),
            retainedSalary, kpi, source.Fingerprint, verifiedDisputes.Select(x => x.Row.Request.Id).Order().ToArray(),
            Amount(settings, salary.BaseAmount, kpi.ExactScore, request.RequestedMinutes));
    }

    public static decimal Amount(LeavePolicySettings settings, decimal monthlyBase, decimal exactKpi, int minutes)
    {
        var rules = settings.Encashment ?? throw AttendanceOperation.Conflict("encashment_policy_not_configured");
        rules.Validate();
        if (monthlyBase <= 0 || exactKpi is < 0 or > 100 || minutes <= 0) throw AttendanceOperation.Conflict("encashment_valuation_inputs_invalid");
        try
        {
            var amount = decimal.Round(monthlyBase * minutes * rules.RateMultiplier * exactKpi /
                (rules.SalaryDivisorDays * (decimal)settings.StandardDayMinutes * 100m), rules.AmountDecimalPlaces, rules.Rounding);
            if (amount > 9999999999999999.99m) throw AttendanceOperation.Conflict("encashment_amount_out_of_range");
            return amount;
        }
        catch (OverflowException) { throw AttendanceOperation.Conflict("encashment_amount_out_of_range"); }
    }

    public async Task<EncashmentCalculation> VerifyApprovalAsync(LeaveEncashmentRequest row, LeaveLedgerEntry? reservation,
        DateTimeOffset now, CancellationToken ct)
    {
        var saved = await operation.ReadPrivateAsync<EncashmentCalculation>(row.CalculationPrivateReference, Purpose(row.EmployeeId), ct);
        if (saved.SchemaVersion != 2 || reservation is null || saved.Policy is null || saved.Request is null || saved.Salary is null || saved.Kpi is null ||
            saved.EmployeeId != row.EmployeeId || saved.RequesterAccountId.ToString() != row.RequestedByUserId || saved.CapturedAt != row.CreatedAt ||
            saved.Request.LeaveTypeId != reservation.LeaveTypeId || saved.Request.PolicyVersionId != row.PolicyVersionId || saved.Policy.VersionId != row.PolicyVersionId ||
            saved.Request.LeaveYearStart != row.LeaveYearStart || saved.Request.LeaveYearEnd != row.LeaveYearEnd || saved.Request.RequestedMinutes != row.RequestedMinutes ||
            PersistedKpiPercent(saved.Kpi.ExactScore) != row.AssessedKpiPercent || saved.Amount != row.ProposedAmount || saved.Salary.Terms.Currency != row.Currency ||
            saved.Amount != Amount(LeavePolicySettings.Parse(saved.Policy.RulesJson, saved.Policy.Schema), saved.Salary.Terms.BaseAmount, saved.Kpi.ExactScore, row.RequestedMinutes))
            throw AttendanceOperation.Conflict("encashment_calculation_unverified");
        var current = await CalculateAsync(row.EmployeeId, saved.RequesterAccountId, saved.Request, now, ct, reservation, saved.CapturedAt);
        if (LeaveProvenance.Digest(current.Policy) != LeaveProvenance.Digest(saved.Policy) || current.Kind != saved.Kind ||
            LeaveProvenance.Digest(current.Salary) != LeaveProvenance.Digest(saved.Salary) || LeaveProvenance.Digest(current.Kpi) != LeaveProvenance.Digest(saved.Kpi) ||
            current.Amount != saved.Amount || current.ValuationDate != saved.ValuationDate || current.VerifiedKpiSourceFingerprint != saved.VerifiedKpiSourceFingerprint)
            throw AttendanceOperation.Conflict("encashment_valuation_source_changed");
        return saved;
    }
}
