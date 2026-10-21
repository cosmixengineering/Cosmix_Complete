using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Application.HR.StaffCategories;

namespace Cosmix.Application.Attendance.Overtime;

public static class OvertimeEligibilityRules
{
    public static OvertimeEligibilityVersion? Effective(IReadOnlyList<OvertimeEligibilityVersion> versions, DateOnly date) =>
        versions.Where(x => x.EffectiveFrom <= date).OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Revision).FirstOrDefault();
    public static OvertimeEligibility Resolve(string category, DateOnly date, IReadOnlyList<OvertimeEligibilityVersion> global,
        IReadOnlyList<OvertimeEligibilityVersion> employee)
    {
        if (category == "OFFICE") return new("office_ineligible", null, false);
        if (category != "SITE") return new("category_unavailable", null, null);
        var baseRule = Effective(global, date); var restriction = Effective(employee, date);
        if (baseRule is null) return new("eligibility_not_configured", restriction?.Id, null, null, restriction?.Id);
        return new("configured", restriction?.Id ?? baseRule.Id, baseRule.IsEnabled && (restriction?.IsEnabled ?? true), baseRule.Id, restriction?.Id);
    }
    public static OvertimeEligibility Resolve(StaffTaxonomyResolution taxonomy, DateOnly date,
        IReadOnlyList<OvertimeEligibilityVersion> global, IReadOnlyList<OvertimeEligibilityVersion> employee) =>
        OvertimeTaxonomy.Restriction(taxonomy) ?? Resolve("SITE", date, global, employee);
}

public sealed class OvertimeEligibilityService(AttendanceOperation operation, IOvertimeEligibilityRepository repository,
    IOvertimeRepository employment, IAttendanceSummaryRepository summaries, IPrivateFileStore files,
    IStaffTaxonomyResolver? taxonomy = null) : IOvertimeEligibilityReader
{
    internal static string Purpose(OvertimeEligibilityTarget target, Guid? employeeId) => $"attendance:overtime:eligibility:{(int)target}:{employeeId?.ToString("N") ?? "global"}";
    public async Task<OvertimeEligibility> ResolveAsync(Guid employeeId, DateOnly date, OvertimeEmploymentSource assignment, CancellationToken ct)
    {
        var mapping = await Mapping(assignment, date, ct);
        if (OvertimeTaxonomy.Restriction(mapping) is { } restriction) return restriction;
        var global = await Timeline(OvertimeEligibilityTarget.GlobalSite, null, ct);
        var local = await Timeline(OvertimeEligibilityTarget.Employee, employeeId, ct);
        return OvertimeEligibilityRules.Resolve(mapping, date, global.Rows, local.Rows);
    }
    public Task<OvertimeEligibilityPreview> PreviewAsync(Guid? employeeId, DateOnly asOf, AuthRequestContext context, CancellationToken ct)
    {
        RosterSchedule.ValidateDate(asOf); if (employeeId == Guid.Empty) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(OvertimePermissions.EligibilityRead, "attendance.overtime.eligibility.preview", employeeId, context,
            async (_, token) => await Preview(employeeId, asOf, token), ct,
            resourceScopes: employeeId.HasValue ? (_, token) => Scopes(employeeId.Value, asOf, token) : null);
    }
    public Task<PagedResult<OvertimeEligibilityEntry>> HistoryAsync(Guid? employeeId, int page, int pageSize, AuthRequestContext context, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || page is < 1 or > 1000 || pageSize is < 1 or > 100) throw AttendanceOperation.Invalid("invalid_overtime_eligibility_page");
        return operation.ReadAsync(OvertimePermissions.EligibilityRead, "attendance.overtime.eligibility.history", employeeId, context,
            async (_, token) =>
            {
                var target = Target(employeeId); var rows = (await Timeline(target, employeeId, token)).Rows.OrderByDescending(x => x.Revision).ToArray();
                return new PagedResult<OvertimeEligibilityEntry>(rows.Skip((page - 1) * pageSize).Take(pageSize).Select(Map).ToArray(), rows.Length, page, pageSize);
            }, ct, resourceScopes: employeeId.HasValue ? (actor, token) => Scopes(employeeId.Value, RosterSchedule.Today(actor.Now), token) : null);
    }
    public Task<OvertimeEligibilityEntry> SetAsync(Guid? employeeId, SetOvertimeEligibilityRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        RosterSchedule.ValidateDate(request.EffectiveFrom);
        if (employeeId == Guid.Empty || request.ExpectedRevision < 0 || !OvertimeRules.Hash(request.ExpectedTimeline)) throw AttendanceOperation.Invalid();
        return operation.WriteAsync(OvertimePermissions.EligibilityManage, "attendance.overtime.eligibility.set", employeeId, key, request, request.Reason, context,
            async (actor, token) =>
            {
                var today = RosterSchedule.Today(actor.Now);
                if (request.EffectiveFrom < today) throw AttendanceOperation.Conflict("overtime_eligibility_past_date");
                if (await summaries.IsLockedAsync(request.EffectiveFrom, token)) throw AttendanceOperation.Conflict("attendance_period_locked");
                var assignment = employeeId.HasValue ? await employment.EmploymentAsync(employeeId.Value, request.EffectiveFrom, token) : null;
                var mapping = assignment is null ? null : await Mapping(assignment, request.EffectiveFrom, token);
                if (employeeId.HasValue && (mapping is null || OvertimeTaxonomy.Restriction(mapping) is not null))
                    throw AttendanceOperation.Conflict("overtime_site_staff_required");
                var target = Target(employeeId); var current = await Timeline(target, employeeId, token);
                var previous = current.Rows.LastOrDefault();
                if ((previous?.Revision ?? 0) != request.ExpectedRevision || Fingerprint(current.Rows) != request.ExpectedTimeline.ToUpperInvariant())
                    throw AttendanceOperation.Conflict("overtime_eligibility_version_conflict");
                // The receipt reauthorizes the full existing history. This new immutable setting
                // pins only its own dated resources, never recursively copies prior scope proofs.
                var original = employeeId.HasValue ? await summaries.ResourceScopesAsync(employeeId.Value, request.EffectiveFrom, request.EffectiveFrom, token) : null;
                var snapshot = new OvertimeEligibilityEvidence(1, Guid.NewGuid(), target, employeeId, checked(request.ExpectedRevision + 1), previous?.Id,
                    request.EffectiveFrom, request.IsEnabled, actor.Identity.AccountId.ToString(), actor.Now, request.Reason.Trim(), original);
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), Purpose(target, employeeId), token));
                if (assignment is not null)
                {
                    var fresh = await employment.EmploymentAsync(employeeId!.Value, request.EffectiveFrom, token);
                    if (fresh != assignment || !RosterStaffTaxonomy.Matches(assignment.StaffCategory,
                        await Mapping(assignment, request.EffectiveFrom, token), RosterStaffTaxonomy.Capture(mapping!)))
                        throw AttendanceOperation.Conflict("overtime_source_changed");
                }
                var row = new OvertimeEligibilityVersion(snapshot.Id, target, employeeId, snapshot.Revision, previous?.Id, request.EffectiveFrom,
                    request.IsEnabled, snapshot.RecordedBy, actor.Now, snapshot.Reason, reference);
                repository.Add(row); await repository.SaveChangesAsync(token);
                return new AttendanceChange<OvertimeEligibilityEntry>(row.Id, previous is null ? null : Map(previous), Map(row));
            }, ct, resourceScopes: employeeId.HasValue ? (_, token) => Scopes(employeeId.Value, request.EffectiveFrom, token) : null,
            beforeReplay: employeeId.HasValue ? async (_, token) =>
            {
                var assignment = await employment.EmploymentAsync(employeeId.Value, request.EffectiveFrom, token);
                if (assignment is null || OvertimeTaxonomy.Restriction(await Mapping(assignment, request.EffectiveFrom, token)) is not null)
                    throw AttendanceOperation.Conflict("overtime_site_staff_required");
            } : null, requireSystemRole: true);
    }
    private async Task<OvertimeEligibilityPreview> Preview(Guid? employeeId, DateOnly date, CancellationToken ct)
    {
        var target = Target(employeeId); var timeline = await Timeline(target, employeeId, ct);
        var effective = OvertimeEligibilityRules.Effective(timeline.Rows, date);
        OvertimeEligibility? result = null;
        if (employeeId.HasValue)
        {
            var assignment = await employment.EmploymentAsync(employeeId.Value, date, ct) ?? throw AttendanceOperation.Missing();
            var global = await Timeline(OvertimeEligibilityTarget.GlobalSite, null, ct);
            result = OvertimeEligibilityRules.Resolve(await Mapping(assignment, date, ct), date, global.Rows, timeline.Rows);
        }
        return new(target.ToString(), employeeId, date, timeline.Rows.LastOrDefault()?.Revision ?? 0, Fingerprint(timeline.Rows),
            effective is null ? null : Map(effective), effective is null ? "not_configured" : effective.IsEnabled ? "enabled" : "disabled", result);
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> Scopes(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var history = await Timeline(OvertimeEligibilityTarget.Employee, employeeId, ct);
        var dates = history.Rows.Select(x => x.EffectiveFrom).Append(date).ToArray();
        var current = await summaries.ResourceScopesAsync(employeeId, dates.Min(), dates.Max(), ct);
        return history.Proofs.SelectMany(x => x.OriginalScopes ?? []).Concat(current)
            .GroupBy(x => JsonSerializer.Serialize(x), StringComparer.Ordinal).Select(x => x.First()).ToArray();
    }
    private async Task<EligibilityTimeline> Timeline(OvertimeEligibilityTarget target, Guid? employeeId, CancellationToken ct)
    {
        var rows = (await repository.TimelineAsync(target, employeeId, ct)).OrderBy(x => x.Revision).ToArray();
        if (rows.Length > 512) throw AttendanceOperation.Conflict("overtime_eligibility_history_limit");
        var proofs = new List<OvertimeEligibilityEvidence>();
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (row.Target != target || row.EmployeeId != employeeId || row.Revision != index + 1 ||
                row.SupersedesVersionId != (index == 0 ? null : rows[index - 1].Id) || index > 0 && row.RecordedAtUtc < rows[index - 1].RecordedAtUtc)
                throw AttendanceOperation.Conflict("overtime_eligibility_history_conflict");
            var proof = await operation.ReadPrivateAsync<OvertimeEligibilityEvidence>(row.ProtectedEvidenceReference, Purpose(target, employeeId), ct);
            if (proof.SchemaVersion != 1 || proof.Id != row.Id || proof.Target != target || proof.EmployeeId != employeeId || proof.Revision != row.Revision ||
                proof.SupersedesVersionId != row.SupersedesVersionId || proof.EffectiveFrom != row.EffectiveFrom || proof.IsEnabled != row.IsEnabled ||
                proof.RecordedBy != row.RecordedBy || proof.RecordedAtUtc != row.RecordedAtUtc || proof.Reason != row.Reason ||
                (employeeId.HasValue ? !AttendanceScopeEvaluation.IsValid(proof.OriginalScopes) || proof.OriginalScopes!.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId != employeeId) : proof.OriginalScopes is not null))
                throw AttendanceOperation.Conflict("overtime_eligibility_evidence_unavailable");
            proofs.Add(proof);
        }
        return new(rows, proofs);
    }
    private static string Fingerprint(IReadOnlyList<OvertimeEligibilityVersion> rows) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(rows.Select(row => new { row.Id, row.Target, row.EmployeeId, row.Revision, row.SupersedesVersionId,
            row.EffectiveFrom, row.IsEnabled, row.RecordedBy, row.RecordedAtUtc, row.Reason }).ToArray())));
    private static OvertimeEligibilityTarget Target(Guid? employeeId) => employeeId.HasValue ? OvertimeEligibilityTarget.Employee : OvertimeEligibilityTarget.GlobalSite;
    private static OvertimeEligibilityEntry Map(OvertimeEligibilityVersion row) => new(row.Id, row.Revision, row.SupersedesVersionId,
        row.EffectiveFrom, row.IsEnabled, row.Target == OvertimeEligibilityTarget.GlobalSite ? row.IsEnabled ? "global_allow" : "global_deny" : row.IsEnabled ? "inherit_global" : "employee_deny",
        row.RecordedBy, row.RecordedAtUtc, row.Reason);
    private sealed record EligibilityTimeline(IReadOnlyList<OvertimeEligibilityVersion> Rows, IReadOnlyList<OvertimeEligibilityEvidence> Proofs);
    private async Task<StaffTaxonomyResolution> Mapping(OvertimeEmploymentSource assignment, DateOnly date, CancellationToken ct)
    {
        var resolution = await RosterStaffTaxonomy.ResolveAsync(taxonomy, assignment.StaffCategory, date, date.AddDays(1), ct);
        if (resolution.Available && resolution.Mapping?.Code != assignment.StaffCategory)
            throw AttendanceOperation.Conflict("overtime_taxonomy_source_conflict");
        return resolution;
    }
}
