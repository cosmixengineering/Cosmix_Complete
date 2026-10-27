using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.Compensation;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Domain.HR.Compensation;

namespace Cosmix.Application.Attendance.Overtime;

/// <summary>Caller owns live authorization/transaction; this helper grants no access by itself.</summary>
public sealed class OvertimeSources(IOvertimeRepository repository, IAttendanceSummaryRepository summaries,
    IIndividualRosterResolver rosters, IOvertimeEligibilityReader eligibility, IOvertimeDurationPolicy policy,
    IEmployeeCompensationRepository compensation, ICorrectionReviewRepository corrections, AttendanceOperation operation,
    IStaffTaxonomyResolver? taxonomy = null)
{
    public async Task<OvertimeFinancialEvidence> CalculateAmountAsync(OvertimeSourceSnapshot source, long approvedTicks, CancellationToken ct)
    {
        if (source.CompensationRecordId is null || await compensation.HasConflictingHistoryAsync(source.Source.EmployeeId, ct))
            throw AttendanceOperation.Conflict("overtime_compensation_version_unavailable");
        var window = await compensation.WindowAsync(source.Source.EmployeeId, source.Source.BusinessDate, ct);
        var salary = window.Candidate;
        if (salary is null || salary.Id != source.CompensationRecordId || salary.EffectiveTo <= source.Source.BusinessDate)
            throw AttendanceOperation.Conflict("overtime_compensation_version_unavailable");
        if (salary.WageBasis != WageBasis.Monthly) throw AttendanceOperation.Conflict("overtime_monthly_salary_required");
        if (salary.Currency != "PKR") throw AttendanceOperation.Conflict("overtime_pkr_salary_required");
        OvertimeFinancialAwardRules.RequireSupportedFormula(source.Source);
        return new(source.Source.DurationPolicy.RateRuleCode!, salary.Id, source.Source.BusinessDate, salary.BaseAmount,
            ConfiguredOvertimeFormula.RetainedDivisorDays(source.Source), approvedTicks,
            ConfiguredOvertimeFormula.RetainedAmount(source.Source, salary.BaseAmount, approvedTicks), "PKR");
    }
    public async Task<OvertimeSourceSnapshot> LoadAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        RosterSchedule.ValidateDate(date);
        if (employeeId == Guid.Empty) throw AttendanceOperation.Invalid();
        var employment = await repository.EmploymentAsync(employeeId, date, ct) ?? throw AttendanceOperation.Conflict("overtime_employment_source_unavailable");
        var sessions = (await repository.SessionSourcesAsync(employeeId, date, ct)).OrderBy(x => x.SessionKey).ThenBy(x => x.Id).ToArray();
        if (sessions.Length > 512 || sessions.Select(x => x.SessionKey).Distinct().Count() != sessions.Length)
            throw AttendanceOperation.Conflict("overtime_source_limit");
        foreach (var session in sessions.Where(x => x.CorrectionId.HasValue))
        {
            var row = await corrections.FindAsync(employeeId, session.CorrectionId!.Value, ct);
            if (row is null || !Guid.TryParse(row.Approval.RequesterUserId, out var requester) ||
                row.Correction.AttendanceDate != date || row.AppliedSessionRevisionId != session.Id || row.AppliedSummaryRevisionId is null)
                throw AttendanceOperation.Conflict("overtime_correction_source_unavailable");
            var evidence = await operation.ReadPrivateAsync<CorrectionEvidenceSnapshot>(row.Correction.PrivateEvidenceReference,
                AttendanceCorrectionService.Purpose(employeeId), ct);
            if (!AttendanceCorrectionService.Matches(row, evidence, requester) || evidence.SchemaVersion != 3)
                throw AttendanceOperation.Conflict("overtime_correction_source_unavailable");
        }
        var roster = await rosters.ResolveAsync(employeeId, date, ct);
        var scopes = await summaries.ResourceScopesAsync(employeeId, date, date, ct);
        if (!AttendanceScopeEvaluation.IsValid(scopes) || scopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId != employeeId))
            throw AttendanceOperation.Conflict("overtime_source_scope_conflict");
        var canonical = scopes.Select(x => new AttendanceResourceScope(x.Alternatives.Distinct().OrderBy(r => r.EmployeeId)
                .ThenBy(r => r.DepartmentId).ThenBy(r => r.TeamId).ThenBy(r => r.SiteId).ThenBy(r => r.ProjectId).ToArray()))
            .OrderBy(x => JsonSerializer.Serialize(x), StringComparer.Ordinal).ToArray();
        var entitlement = await eligibility.ResolveAsync(employeeId, date, employment, ct);
        var shell = new OvertimeSourceSnapshot(new(employeeId, date, employment.StaffCategory, employment.SiteId,
            roster, sessions, null, null, false, entitlement, new("duration_policy_unavailable", null, null, null, []), ""),
            null, employment, canonical);
        OvertimeTaxonomy.ValidateRetained(shell);
        var mapping = await RosterStaffTaxonomy.ResolveAsync(taxonomy, employment.StaffCategory, date, OvertimeTaxonomy.EndExclusive(shell), ct);
        if (!RosterStaffTaxonomy.Matches(employment.StaffCategory, mapping, roster.Taxonomy))
            throw AttendanceOperation.Conflict("overtime_taxonomy_source_conflict");
        entitlement = OvertimeTaxonomy.Restriction(mapping) ?? entitlement;
        if (entitlement.Allowed == true && (entitlement.Status != "configured" || entitlement.VersionId is null || entitlement.VersionId == Guid.Empty))
            throw AttendanceOperation.Conflict("invalid_overtime_eligibility_contract");
        var duration = await policy.EvaluateAsync(employeeId, date, sessions, roster, ct);
        OvertimeRules.ValidatePolicy(duration);
        if (duration.PolicyVersionId.HasValue && !await repository.ApprovedPolicyAsync(duration.PolicyVersionId.Value, date, ct))
            throw AttendanceOperation.Conflict("overtime_policy_provenance_unavailable");
        if (await compensation.HasConflictingHistoryAsync(employeeId, ct)) throw AttendanceOperation.Conflict("overtime_compensation_history_conflict");
        var window = await compensation.WindowAsync(employeeId, date, ct);
        var retainedCompensation = window.Candidate is { } salary && (!salary.EffectiveTo.HasValue || date < salary.EffectiveTo) ? salary.Id : (Guid?)null;
        var summary = await summaries.LatestAsync(employeeId, date, ct);
        var response = new OvertimeSourceResponse(employeeId, date, employment.StaffCategory, employment.SiteId, roster, sessions,
            summary?.Id, summary?.InputFingerprint, await summaries.IsLockedAsync(date, ct), entitlement, duration, "");
        var captured = new OvertimeSourceSnapshot(response, retainedCompensation, employment, canonical);
        return captured with { Source = response with { SourceFingerprint = OvertimeRules.Fingerprint(captured) } };
    }

    public async Task RevalidateTaxonomyAsync(OvertimeSourceSnapshot retained, CancellationToken ct)
    {
        OvertimeTaxonomy.ValidateRetained(retained);
        if (retained.Source.Roster.Taxonomy is null && taxonomy is null) return; // Legacy standalone adapters retain their baseline contract.
        var employment = await repository.EmploymentAsync(retained.Source.EmployeeId, retained.Source.BusinessDate, ct);
        if (employment is null || employment != retained.Employment) throw AttendanceOperation.Conflict("overtime_source_changed");
        var mapping = await RosterStaffTaxonomy.ResolveAsync(taxonomy, employment.StaffCategory,
            retained.Source.BusinessDate, OvertimeTaxonomy.EndExclusive(retained), ct);
        if (!RosterStaffTaxonomy.Matches(employment.StaffCategory, mapping, retained.Source.Roster.Taxonomy))
            throw AttendanceOperation.Conflict("overtime_source_changed");
    }

    public async Task ValidateSubmissionReplayAsync(Guid employeeId, SubmitOvertimeRequest proposal, CancellationToken ct)
    {
        if (taxonomy is null) return;
        var current = await LoadAsync(employeeId, proposal.BusinessDate, ct);
        if (!string.Equals(current.Source.SourceFingerprint, proposal.ExpectedSourceFingerprint, StringComparison.OrdinalIgnoreCase))
            throw AttendanceOperation.Conflict("overtime_source_changed");
    }
}
