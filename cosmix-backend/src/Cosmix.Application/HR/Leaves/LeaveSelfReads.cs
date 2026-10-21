using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.Leaves;

public sealed partial class LeaveService
{
    public Task<LeaveSelfPoliciesResponse> SelfPoliciesAsync(LeaveSelfPolicyRequest request, AuthRequestContext context, CancellationToken ct)
    {
        if (request is null || request.LeaveTypeId == Guid.Empty) throw AttendanceOperation.Invalid("invalid_leave_policy_selection");
        LeaveRules.List(new(request.From, request.To));
        return operation.ReadAsync(LeavePermissions.Self, "attendance.leave.self-policies", null, context, async (actor, token) =>
        {
            try
            {
                if (!await repository.EmployeeExistsAsync(actor.Identity.EmployeeId, token))
                    return new LeaveSelfPoliciesResponse("unavailable", [], ["leave_employee_inactive"], actor.Now);
                var type = await repository.TypeAsync(request.LeaveTypeId, token);
                if (type is null || !type.IsActive) return new LeaveSelfPoliciesResponse("unavailable", [], ["leave_type_unavailable"], actor.Now);
                var versions = await repository.PolicyVersionsAsync(token);
                if (versions.Count > 500) throw AttendanceOperation.Conflict("leave_policy_history_limit");
                var candidates = versions.Where(x => PublishedFor(x, request.From, request.To)).ToArray();
                if (candidates.Length > 50) throw AttendanceOperation.Conflict("leave_policy_selection_limit");
                var result = new List<LeavePolicyResponse>();
                var transitionUnavailable = false;
                foreach (var candidate in candidates)
                {
                    try
                    {
                        var policy = await repository.PolicyAsync(actor.Identity.EmployeeId, type.Id, candidate.Id, request.From, request.To, token);
                        if (policy is null || !policy.ProvenanceValid || policy.Version.Id != candidate.Id) continue;
                        var settings = LeavePolicySettings.Parse(policy.Version.RulesJson, policy.Version.RulesSchemaVersion);
                        if (settings.EligibleStaffCategoryIds.Contains(policy.StaffCategoryId)) result.Add(SelectedPolicy(policy.Version, settings));
                    }
                    catch (AuthenticationFailure failure) when (failure.StatusCode == 409 && failure.Code == "leave_policy_year_transition_unavailable")
                    { transitionUnavailable = true; }
                }
                var status = result.Count > 0 ? "available" : transitionUnavailable ? "conflict" : "unavailable";
                return new LeaveSelfPoliciesResponse(status, result, transitionUnavailable ? ["leave_policy_year_transition_unavailable"] :
                    result.Count == 0 ? ["leave_policy_unavailable"] : [], actor.Now);
            }
            catch (AuthenticationFailure failure) when (failure.StatusCode is 400 or 409)
            { return new LeaveSelfPoliciesResponse("conflict", [], [failure.Code], actor.Now); }
        }, ct, self: true);
    }

    public Task<LeaveSelfBalanceResponse> BalanceAsync(LeaveSelfBalanceRequest request, AuthRequestContext context, CancellationToken ct)
    {
        if (request is null || request.LeaveTypeId == Guid.Empty || request.PolicyVersionId == Guid.Empty || request.AsOf?.Year is < 1901 or > 9997)
            throw AttendanceOperation.Invalid("invalid_leave_balance_request");
        return operation.ReadAsync(LeavePermissions.Self, "attendance.leave.self-balance", null, context, async (actor, token) =>
            (await ReadBalance(actor.Identity.EmployeeId, request.LeaveTypeId, request.PolicyVersionId,
                request.AsOf ?? LeaveRules.LocalDate(actor.Now), request.AsOf ?? LeaveRules.LocalDate(actor.Now), actor.Now, token)).Response, ct, self: true);
    }

    public Task<LeavePreviewResponse> PreviewAsync(PreviewLeaveRequest request, AuthRequestContext context, CancellationToken ct)
    {
        if (request is null || request.PolicyVersionId == Guid.Empty || !Enum.IsDefined(request.Treatment))
            throw AttendanceOperation.Invalid("invalid_leave_preview");
        // The same bounded allocation parser used by intake computes all durations. There is no
        // employee, reason, authority, attachment or client-computed total in this read contract.
        var days = LeaveRules.Allocate(Guid.NewGuid(), new(request.LeaveTypeId, request.Days, "Leave preview"));
        return operation.ReadAsync(LeavePermissions.Self, "attendance.leave.self-preview", null, context, async (actor, token) =>
        {
            var result = await ReadBalance(actor.Identity.EmployeeId, request.LeaveTypeId, request.PolicyVersionId,
                days[0].Date, days[^1].Date, actor.Now, token);
            var reasons = result.Response.Status == "available" ? new List<string>() : result.Response.Reasons.ToList();
            var required = new List<LeaveEvidenceKind>();
            var requiresEvidence = result.Type?.RequiresEvidence == true;
            if (result.Type?.Kind == LeaveKind.Sick) required.Add(LeaveEvidenceKind.Medical);
            if (result.Type?.Kind == LeaveKind.Exam) required.AddRange([LeaveEvidenceKind.AdmitCard, LeaveEvidenceKind.DateSheet]);
            if (result.Type?.Kind == LeaveKind.SiteInjuryRest) required.Add(LeaveEvidenceKind.InjuryMedical);
            var settings = result.Response.Policy?.Settings;
            if (result.Type?.Kind == LeaveKind.Emergency && settings is not null && days.Any(x => x.Minutes * 2 == settings.StandardDayMinutes))
            { requiresEvidence = true; }
            if (request.Treatment == LeavePayTreatment.PendingReview) reasons.Add("leave_treatment_requires_explicit_policy");
            if (result.Type is not null && result.Evidence is not null)
            {
                await Capture(async () =>
                {
                    await Unlocked(days[0].Date, days[^1].Date, token);
                    await NoOverlap(actor.Identity.EmployeeId, days, null, token);
                    foreach (var day in days) ValidateSchedule(day, await rosters.ResolveAsync(actor.Identity.EmployeeId, day.Date, token));
                    if (result.Type.RequiresAdvanceApproval && days[0].StartsAtUtc <= actor.Now)
                        throw AttendanceOperation.Conflict("advance_leave_required");
                }, reasons);
                if (request.Treatment != LeavePayTreatment.PendingReview)
                    await Capture(() =>
                    {
                        var proposed = new LeaveRequest(actor.Identity.EmployeeId, result.Type.Id, days[0].Date, days[^1].Date,
                            days.Sum(x => x.Minutes), "Leave preview", null, null, null, null, LeavePayTreatment.PendingReview,
                            null, actor.Identity.AccountId.ToString(), actor.Now);
                        foreach (var day in days) proposed.Days.Add(day);
                        LeaveTreatmentRules.ValidatePolicyTreatment(new(proposed, result.Type, null, [], [], null), request.Treatment, result.Evidence);
                        return Task.CompletedTask;
                    }, reasons);
            }
            var status = reasons.Count > 0 ? result.Response.Status == "unavailable" ? "unavailable" : "conflict" : requiresEvidence ? "conditional" : "available";
            return new LeavePreviewResponse(status, actor.Now, false, days.Sum(x => x.Minutes),
                settings is null ? null : LeaveTreatmentRules.RequestedDays(days.Sum(x => x.Minutes), settings.StandardDayMinutes),
                result.Response, requiresEvidence, required, reasons.Distinct(StringComparer.Ordinal).ToArray());
        }, ct, self: true);
    }

    private async Task<(LeaveSelfBalanceResponse Response, LeaveType? Type, LeavePolicyEvidence? Evidence)> ReadBalance(
        Guid employeeId, Guid typeId, Guid? policyId, DateOnly from, DateOnly to, DateTimeOffset now, CancellationToken ct)
    {
        var year = LeavePolicySettings.Year(from);
        var response = new LeaveSelfBalanceResponse("unavailable", from, year.From, year.To, now,
            "current_recorded_ledger_for_selected_fiscal_year", policyId, null, null, null, null, null, null, []);
        LeaveType? type = null;
        try
        {
            if (!await repository.EmployeeExistsAsync(employeeId, ct)) return (response with { Reasons = ["leave_employee_inactive"] }, null, null);
            type = await repository.TypeAsync(typeId, ct);
            if (type is null || !type.IsActive) return (response with { Reasons = ["leave_type_unavailable"] }, type, null);
            response = response with { LeaveType = LeaveRules.Type(type) };
            if (!policyId.HasValue) return (response with { Reasons = ["leave_policy_selection_required"] }, type, null);
            var policy = await repository.PolicyAsync(employeeId, typeId, policyId.Value, from, to, ct);
            if (policy is null) return (response with { Reasons = ["leave_policy_unavailable"] }, type, null);
            if (policy.Version.Id != policyId || !PublishedFor(policy.Version, from, to) || !policy.ProvenanceValid)
                throw AttendanceOperation.Conflict("leave_policy_provenance_unavailable");
            var settings = LeavePolicySettings.Parse(policy.Version.RulesJson, policy.Version.RulesSchemaVersion);
            year = LeavePolicySettings.Year(from, settings);
            response = response with { FiscalYearStart = year.From, FiscalYearEndExclusive = year.To };
            if (LeavePolicySettings.Year(to, settings) != year) throw AttendanceOperation.Conflict("split_leave_at_fiscal_year_boundary");
            if (!settings.EligibleStaffCategoryIds.Contains(policy.StaffCategoryId)) throw AttendanceOperation.Conflict("leave_policy_not_applicable");
            var typeLimit = type.Kind switch { LeaveKind.Sick => settings.SickDays, LeaveKind.Casual => settings.CasualDays, _ => settings.AnnualDays };
            var balances = LeaveTreatmentRules.Balances(policy.Usage, type.Kind, settings.AnnualDays, typeLimit);
            bool? serviceEligible = !type.ReducesRegularBalance ? true : policy.JoinedOn is null ? null : settings.ServiceEligible(policy.JoinedOn.Value, from);
            response = response with { Status = "available", Policy = SelectedPolicy(policy.Version, settings),
                PaidKindConfigured = settings.PaidKinds.Contains(type.Kind), ServiceEligible = serviceEligible,
                AnnualBalance = balances.Annual, TypeBalance = type.ReducesRegularBalance ? balances.Type : null,
                Reasons = serviceEligible is null ? ["leave_service_eligibility_unavailable"] : [] };
            return (response, type, policy);
        }
        catch (AuthenticationFailure failure) when (failure.StatusCode is 400 or 409)
        { return (response with { Status = "conflict", Reasons = [failure.Code] }, type, null); }
    }
    private static bool PublishedFor(PolicyVersion version, DateOnly from, DateOnly to) =>
        version.Authority == PolicyAuthority.DirectHr && version.ApprovalRequestId is null &&
        version.Policy.Category == PolicyCategory.Leave && LeavePolicySettings.SupportsSchema(version.RulesSchemaVersion) &&
        version.EffectiveFrom <= from && (version.EffectiveTo is null || to < version.EffectiveTo);
    private static LeavePolicyResponse SelectedPolicy(PolicyVersion version, LeavePolicySettings settings) =>
        new(version.PolicyId, version.Id, version.Policy.Code, version.Policy.Name, version.EffectiveFrom, version.EffectiveTo, settings, version.Authority.ToString());
    private static async Task Capture(Func<Task> action, List<string> reasons)
    {
        try { await action(); }
        catch (AuthenticationFailure failure) when (failure.StatusCode is 400 or 409) { reasons.Add(failure.Code); }
    }
}
