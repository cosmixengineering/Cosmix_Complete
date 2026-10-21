using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.StaffCategories;

namespace Cosmix.Application.Attendance.PayrollInputs;

public sealed class AttendancePayrollInputService(AttendanceOperation operation, IAttendancePayrollInputProducer producer,
    IAttendancePayrollInputLedger ledger, IAttendancePayrollJurisdictionReader jurisdictions, IPrivateFileStore files,
    IAccountAccessReader access)
{
    public Task<AttendancePayrollInputPreview> PreviewAsync(Guid employeeId, DateOnly monthStart,
        AuthRequestContext context, CancellationToken ct)
    {
        Validate(employeeId, monthStart);
        return operation.ReadAsync(AttendancePayrollInputPermissions.Read, "attendance.payroll-inputs.preview", employeeId, context,
            async (actor, token) => (await Inspect(employeeId, monthStart, actor, AttendancePayrollInputPermissions.Read, token)).Preview, ct,
            resourceScopes: (_, token) => producer.ResourceScopesAsync(employeeId, monthStart, token));
    }

    public Task<AttendancePayrollInputDetail> SaveAsync(Guid employeeId, DateOnly monthStart,
        SaveAttendancePayrollInputsRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        Validate(employeeId, monthStart);
        if (request is null || request.ExpectedRevision < 0 || request.ExpectedPreviewFingerprint is not { Length: 64 } ||
            request.ExpectedPreviewFingerprint.Any(c => c is not (>= '0' and <= '9') and not (>= 'A' and <= 'F')))
            throw AttendanceOperation.Invalid("invalid_payroll_input_request");
        return operation.WriteAsync(AttendancePayrollInputPermissions.Calculate, "attendance.payroll-inputs.save", employeeId,
            key, new { monthStart, request }, request.Reason, context, async (actor, token) =>
            {
                var inspected = await Inspect(employeeId, monthStart, actor, AttendancePayrollInputPermissions.Calculate, token);
                if (inspected.Preview.IsLocked) throw AttendanceOperation.Conflict("attendance_period_locked");
                if (request.ExpectedRevision != inspected.Preview.ExpectedRevision)
                    throw AttendanceOperation.Conflict("payroll_input_revision_conflict");
                if (request.ExpectedPreviewFingerprint != inspected.Preview.PreviewFingerprint)
                    throw AttendanceOperation.Conflict("payroll_input_preview_stale");
                if (!inspected.Preview.CanSave) throw AttendanceOperation.Conflict("payroll_input_sources_unavailable");
                // A second key for identical inputs must not manufacture another deduction revision.
                if (inspected.Latest is { } latest && latest.InputFingerprint == inspected.Preview.PreviewFingerprint)
                {
                    var unchanged = await ReadEvidence(latest, actor, AttendancePayrollInputPermissions.Calculate, token);
                    return new AttendanceChange<AttendancePayrollInputDetail>(latest.Id, Map(latest), Detail(latest, unchanged, true));
                }
                var revision = checked(inspected.Preview.ExpectedRevision + 1);
                var evidence = new AttendancePayrollInputEvidence(1, employeeId, monthStart, revision,
                    inspected.Preview.PreviewFingerprint, inspected.Calculation, inspected.Jurisdictions,
                    inspected.Calculation.SourceManifest.OriginalScopes, actor.Identity.AccountId, request.Reason.Trim(), actor.Now);
                var bytes = JsonSerializer.SerializeToUtf8Bytes(evidence);
                if (bytes.Length > 8 * 1024 * 1024) throw AttendanceOperation.Conflict("payroll_input_evidence_limit");
                var reference = AttendanceOperation.Reference(await files.WriteAsync(bytes, Purpose(employeeId, monthStart, revision), token));
                var record = new AttendancePayrollInputRevision(employeeId, monthStart, revision, inspected.Latest?.Id,
                    AttendancePayrollInputRules.Version, evidence.PreviewFingerprint, reference, actor.Identity.AccountId, actor.Now);
                ledger.Add(record);
                var result = Detail(record, evidence, true);
                return new AttendanceChange<AttendancePayrollInputDetail>(record.Id, inspected.Latest is null ? null : Map(inspected.Latest), result);
            }, ct, resourceScopes: (_, token) => producer.ResourceScopesAsync(employeeId, monthStart, token));
    }

    public Task<PagedResult<AttendancePayrollInputRevisionResponse>> HistoryAsync(Guid employeeId, DateOnly monthStart,
        int page, int pageSize, AuthRequestContext context, CancellationToken ct)
    {
        Validate(employeeId, monthStart);
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(AttendancePayrollInputPermissions.Read, "attendance.payroll-inputs.history", employeeId, context,
            async (actor, token) =>
            {
                var rows = await ledger.HistoryAsync(employeeId, monthStart, page, pageSize, token);
                foreach (var row in rows.Items) await ReadEvidence(row, actor, AttendancePayrollInputPermissions.Read, token);
                return new PagedResult<AttendancePayrollInputRevisionResponse>(rows.Items.Select(Map).ToArray(), rows.TotalCount, page, pageSize);
            }, ct, resourceScopes: (_, token) => producer.ResourceScopesAsync(employeeId, monthStart, token));
    }

    public Task<AttendancePayrollInputDetail> DetailAsync(Guid employeeId, DateOnly monthStart, Guid id,
        AuthRequestContext context, CancellationToken ct)
    {
        Validate(employeeId, monthStart);
        if (id == Guid.Empty) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(AttendancePayrollInputPermissions.Read, "attendance.payroll-inputs.detail", employeeId, context,
            async (actor, token) =>
            {
                var record = await ledger.FindAsync(employeeId, id, token);
                if (record is null || record.PeriodStart != monthStart) throw AttendanceOperation.Missing();
                var saved = await ReadEvidence(record, actor, AttendancePayrollInputPermissions.Read, token);
                var result = Detail(record, saved, false);
                return result with { Inputs = result.Inputs with { IsLocked = await ledger.IsLockedAsync(monthStart, token) } };
            }, ct, resourceScopes: (_, token) => producer.ResourceScopesAsync(employeeId, monthStart, token));
    }

    // Internal month-lock consumer: caller already owns AttendanceOperation's live transaction.
    internal async Task<AttendancePayrollMonthSource> ReadForMonthLockAsync(Guid employeeId, DateOnly month,
        AttendanceActor actor, CancellationToken ct)
    {
        Validate(employeeId, month);
        var current = await producer.ResourceScopesAsync(employeeId, month, ct);
        if (!AttendanceScopeEvaluation.Allows(actor.Identity,
            await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, ct), AttendancePayrollInputPermissions.Read, current))
            throw AttendanceOperation.Denied();
        var inspected = await Inspect(employeeId, month, actor, AttendancePayrollInputPermissions.Read, ct);
        var issues = inspected.Calculation.Issues.Select(x => new MonthLockIssue(x.Code, employeeId, x.BusinessDate)).ToList();
        if (inspected.Calculation.State != "ready") issues.Add(new("payroll_input_sources_unavailable", employeeId));
        if (inspected.Latest is null)
        {
            issues.Add(new("payroll_input_revision_missing", employeeId));
            return new(null, null, inspected.Preview.PreviewFingerprint, issues);
        }
        var retained = await ReadEvidence(inspected.Latest, actor, AttendancePayrollInputPermissions.Read, ct);
        if (retained.Calculation.State != "ready" || retained.Calculation.Issues.Count != 0)
            throw AttendanceOperation.Conflict("payroll_input_evidence_invalid");
        if (inspected.Latest.InputFingerprint != inspected.Preview.PreviewFingerprint)
            issues.Add(new("payroll_input_revision_stale", employeeId));
        return new(inspected.Latest, retained, inspected.Preview.PreviewFingerprint, issues);
    }

    internal async Task<AttendancePayrollInputEvidence> ReadRetainedForMonthLockAsync(Guid employeeId, Guid id,
        AttendanceActor actor, CancellationToken ct)
    {
        var row = await ledger.FindAsync(employeeId, id, ct) ?? throw AttendanceOperation.Conflict("month_lock_payroll_revision_missing");
        var scopes = await producer.ResourceScopesAsync(employeeId, row.PeriodStart, ct);
        if (!AttendanceScopeEvaluation.Allows(actor.Identity,
                await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, ct), AttendancePayrollInputPermissions.Read, scopes))
            throw AttendanceOperation.Denied();
        return await ReadEvidence(row, actor, AttendancePayrollInputPermissions.Read, ct);
    }

    private async Task<Inspection> Inspect(Guid employeeId, DateOnly monthStart, AttendanceActor actor, string permission, CancellationToken ct)
    {
        var calculation = await producer.CalculateAsync(employeeId, monthStart, ct);
        ValidateCalculation(calculation, employeeId, monthStart);
        if (!AttendanceScopeEvaluation.Allows(actor.Identity,
                await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, ct), permission, calculation.SourceManifest.OriginalScopes))
            throw AttendanceOperation.Denied();
        var days = calculation.SourceManifest.Days.Where(x => x.Snapshot is not null || x.BranchId.HasValue)
            .Select(x => new AttendancePayrollJurisdictionDay(x.BusinessDate, x.Snapshot?.Employment.SiteId,
                x.BranchId ?? x.Snapshot?.Roster.BranchId, x.OfficeSource ?? x.Snapshot?.OfficeSource)).ToArray();
        var location = (await jurisdictions.ReadAsync(days, ct)).OrderBy(x => x.BusinessDate).ThenBy(x => x.SiteId).ToArray();
        if (location.Length != days.Length || days.Any(day => location.Count(x => x.BusinessDate == day.BusinessDate &&
            x.SiteId == day.SiteId && x.BranchId == day.BranchId &&
            (!day.BranchId.HasValue || x.State != "configured" || day.OfficeSource is { } office &&
                x.ConfigurationVersionId == office.OfficeGeofenceVersionId && x.JurisdictionCode == office.JurisdictionCode)) != 1))
            throw AttendanceOperation.Conflict("payroll_jurisdiction_contract_invalid");
        var latest = await ledger.LatestAsync(employeeId, monthStart, ct);
        var locked = await ledger.IsLockedAsync(monthStart, ct);
        var fingerprint = Fingerprint(calculation, location);
        return new(calculation, location, latest, Preview(calculation, location, fingerprint, latest?.Revision ?? 0, latest?.Id, locked));
    }

    private async Task<AttendancePayrollInputEvidence> ReadEvidence(AttendancePayrollInputRevision row, AttendanceActor actor,
        string permission, CancellationToken ct) => await ReadValidatedEvidenceAsync(row, actor, permission, operation, access, ct);

    internal static async Task<AttendancePayrollInputEvidence> ReadValidatedEvidenceAsync(AttendancePayrollInputRevision row,
        AttendanceActor actor, string permission, AttendanceOperation operation, IAccountAccessReader access, CancellationToken ct)
    {
        var evidence = await operation.ReadPrivateAsync<AttendancePayrollInputEvidence>(row.ProtectedEvidenceReference,
            Purpose(row.EmployeeId, row.PeriodStart, row.Revision), ct);
        if (evidence.SchemaVersion != 1 || evidence.EmployeeId != row.EmployeeId || evidence.MonthStart != row.PeriodStart ||
            evidence.Revision != row.Revision || evidence.ActorUserId != row.ActorUserId || evidence.CreatedAtUtc != row.CreatedAtUtc ||
            row.CalculationVersion != AttendancePayrollInputRules.Version || evidence.PreviewFingerprint != row.InputFingerprint ||
            Fingerprint(evidence.Calculation, evidence.Jurisdictions) != row.InputFingerprint)
            throw AttendanceOperation.Conflict("payroll_input_evidence_invalid");
        ValidateCalculation(evidence.Calculation, row.EmployeeId, row.PeriodStart);
        var officeDays = evidence.Calculation.SourceManifest.Days.Where(x => x.BranchId.HasValue || x.Snapshot?.Roster.BranchId.HasValue == true);
        if (officeDays.Any(day => evidence.Jurisdictions.Count(x => x.BusinessDate == day.BusinessDate && x.SiteId is null &&
            x.BranchId == (day.BranchId ?? day.Snapshot?.Roster.BranchId) && (x.State != "configured" ||
                (day.OfficeSource ?? day.Snapshot?.OfficeSource) is { } office &&
                x.ConfigurationVersionId == office.OfficeGeofenceVersionId && x.JurisdictionCode == office.JurisdictionCode)) != 1))
            throw AttendanceOperation.Conflict("payroll_input_evidence_invalid");
        if (JsonSerializer.Serialize(evidence.OriginalScopes) != JsonSerializer.Serialize(evidence.Calculation.SourceManifest.OriginalScopes) ||
            !AttendanceScopeEvaluation.Allows(actor.Identity, await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, ct), permission, evidence.OriginalScopes))
            throw AttendanceOperation.Denied();
        return evidence;
    }

    private static void ValidateCalculation(AttendancePayrollInputCalculation calculation, Guid employeeId, DateOnly monthStart)
    {
        if (calculation.EmployeeId != employeeId || calculation.MonthStart != monthStart || calculation.MonthEndExclusive != monthStart.AddMonths(1) ||
            calculation.InputFingerprint is not { Length: 64 } || calculation.SourceManifest.EmployeeId != employeeId ||
            calculation.SourceManifest.MonthStart != monthStart || calculation.SourceManifest.CalculationVersion != AttendancePayrollInputRules.Version ||
            !AttendanceScopeEvaluation.IsValid(calculation.SourceManifest.OriginalScopes) ||
            calculation.SourceManifest.OriginalScopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId != employeeId) ||
            calculation.Lines.Count > 732 || calculation.Lines.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() != calculation.Lines.Count ||
            calculation.Lines.Any(x => x.Amount < 0 || x.CandidateAmount < x.Amount || x.Currency != "PKR") ||
            calculation.State is not ("ready" or "unavailable") || calculation.State == "ready" && calculation.Issues.Count != 0)
            throw AttendanceOperation.Conflict("payroll_input_contract_invalid");
        foreach (var day in calculation.SourceManifest.Days)
        {
            var snapshot = day.Snapshot; var source = day.OfficeSource ?? snapshot?.OfficeSource;
            var branch = day.BranchId ?? snapshot?.Roster.BranchId;
            if (snapshot is not null && !RosterStaffTaxonomy.Valid(snapshot.Roster.Taxonomy, snapshot.Employment.StaffCategory,
                snapshot.Roster.BranchId.HasValue ? StaffWorkLocation.Office : StaffWorkLocation.Site,
                snapshot.Roster.BranchId.HasValue ? StaffScheduleBasis.Office : StaffScheduleBasis.Site))
                throw AttendanceOperation.Conflict(snapshot.Roster.BranchId.HasValue ?
                    "payroll_office_source_conflict" : "payroll_taxonomy_source_conflict");
            if (branch is null && source is null && snapshot?.Employment.StaffCategory != "OFFICE") continue;
            if (branch is null || branch == Guid.Empty || snapshot is null && source is not null ||
                snapshot is not null && (source is null || source.EmployeeId != employeeId || source.BusinessDate != day.BusinessDate ||
                    snapshot.EmployeeId != employeeId || snapshot.BusinessDate != day.BusinessDate ||
                    snapshot.Roster.SiteId is not null || snapshot.Employment.SiteId is not null ||
                    snapshot.Roster.BranchId != branch || source.BranchId != branch || snapshot.Employment.BranchId != branch ||
                    !RosterStaffTaxonomy.Valid(snapshot.Roster.Taxonomy, snapshot.Employment.StaffCategory,
                        StaffWorkLocation.Office, StaffScheduleBasis.Office) || snapshot.Employment.AssignmentId != source.EmployeeAssignmentId ||
                    snapshot.Roster.Explanation?.MembershipRecordId != source.EmployeeAssignmentId ||
                    snapshot.Roster.AssignmentId != source.OfficeRosterAssignmentId ||
                    snapshot.Roster.Explanation?.OfficeRosterAssignmentId != source.OfficeRosterAssignmentId ||
                    snapshot.Roster.ShiftVersionId != source.ShiftVersionId || snapshot.Roster.Holiday?.CalendarId != source.HolidayCalendarId ||
                    source.OfficeGeofenceVersionId == Guid.Empty || source.BoundaryVersionNumber < 1 ||
                    snapshot.OfficeSource != source))
                throw AttendanceOperation.Conflict("payroll_office_source_conflict");
        }
    }
    private static AttendancePayrollInputPreview Preview(AttendancePayrollInputCalculation calculation,
        IReadOnlyList<AttendancePayrollJurisdictionContext> location, string fingerprint, int revision, Guid? latestId, bool locked) =>
        new(calculation.EmployeeId, calculation.MonthStart, calculation.MonthEndExclusive, calculation.State,
            fingerprint, revision, latestId, calculation.State == "ready" && !locked, locked, false, false,
            location.Any(x => x.State != "configured") ? "jurisdiction_configuration_required" : "legal_policy_review_required",
            calculation.State == "ready" ? calculation.Lines.Sum(x => x.Amount) : null,
            calculation.State == "ready" ? "PKR" : null, calculation.Lines.Select(Line).ToArray(), calculation.Issues, location);
    private static AttendancePayrollInputDetail Detail(AttendancePayrollInputRevision row, AttendancePayrollInputEvidence saved, bool current) =>
        new(Map(row), Preview(saved.Calculation, saved.Jurisdictions, saved.PreviewFingerprint, row.Revision, row.Id, false)
            with { CanSave = false, State = current ? "prepared_review_required" : "retained_input_revision" });
    private static AttendancePayrollInputRevisionResponse Map(AttendancePayrollInputRevision row) => new(row.Id, row.EmployeeId,
        row.PeriodStart, row.Revision, row.SupersedesRevisionId, row.CalculationVersion, row.InputFingerprint, row.CreatedAtUtc,
        row.ActorUserId, "prepared_review_required");
    private static AttendancePayrollInputLineResponse Line(AttendancePayrollInputLine row) => new(row.Key, row.Kind,
        row.CompletionDate, row.SourceDates, row.PolicyVersionIds, row.CompensationRecordId, row.SalaryBasis.ToString(), row.MonthlySalary,
        row.SalaryDivisorDays, row.DeductionDays, row.ShortfallTicks, row.ScheduledTicks, row.CandidateAmount, row.Amount,
        row.Currency, row.Rounding.ToString(), row.ApplicationCode, row.OrdinaryComparisonAmount,
        row.SourceIds, row.SourceFingerprints, row.CompensationFingerprint,
        row.ExactAmountNumerator, row.ExactAmountDenominator);
    private static string Fingerprint(AttendancePayrollInputCalculation calculation, IReadOnlyList<AttendancePayrollJurisdictionContext> location) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Calculation = calculation, Jurisdictions = location })));
    private static string Purpose(Guid employeeId, DateOnly monthStart, int revision) =>
        $"attendance:payroll-inputs:{employeeId:N}:{monthStart:yyyy-MM}:{revision}";
    private static void Validate(Guid employeeId, DateOnly monthStart)
    {
        if (employeeId == Guid.Empty || monthStart.Day != 1 || monthStart.Year is < 1900 or > 9998)
            throw AttendanceOperation.Invalid("invalid_payroll_input_period");
    }
    private sealed record Inspection(AttendancePayrollInputCalculation Calculation,
        IReadOnlyList<AttendancePayrollJurisdictionContext> Jurisdictions, AttendancePayrollInputRevision? Latest,
        AttendancePayrollInputPreview Preview);
}

internal sealed record AttendancePayrollMonthSource(AttendancePayrollInputRevision? Row,
    AttendancePayrollInputEvidence? Evidence, string CurrentFingerprint, IReadOnlyList<MonthLockIssue> Issues);
