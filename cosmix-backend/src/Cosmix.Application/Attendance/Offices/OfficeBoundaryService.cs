using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.CompanySettings;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.HR.CompanySettings;

namespace Cosmix.Application.Attendance.Offices;

public sealed class OfficeBoundaryService(OrganizationOperation operation, IOfficeBoundaryRepository repository,
    ICurrentAuthentication current, TimeProvider clock)
{
    public Task<OfficeBoundaryConfigurationResponse> ReadAsync(Guid branchId, DateOnly? asOfDate, CancellationToken ct) =>
        operation.ReadAsync(CompanySettingsPermissions.Read, async token =>
        {
            var date = asOfDate ?? RosterSchedule.Today(clock.GetUtcNow());
            if (branchId == Guid.Empty || date.Year is < 1901 or > 9997)
                throw OrganizationOperation.Invalid("invalid_office_boundary_date");
            var branch = await repository.FindBranchAsync(branchId, token) ?? throw OrganizationOperation.Missing();
            var rows = await repository.VersionsAsync(branchId, token);
            ValidateHistory(branchId, rows);
            var boundary = rows.Where(x => x.EffectiveFrom <= date).MaxBy(x => x.VersionNumber);
            return new OfficeBoundaryConfigurationResponse(branchId, date, boundary is null ? "not_configured" : "configured",
                Convert.ToBase64String(branch.RowVersion), rows.Count == 0 ? 0 : rows.Max(x => x.VersionNumber), boundary);
        }, ct, requireSuperAdministrator: true);

    public Task<OfficeBoundaryVersionResponse> PublishAsync(Guid branchId, CreateOfficeBoundaryRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        Validate(request);
        if (branchId == Guid.Empty) throw OrganizationOperation.Invalid("invalid_office_id");
        var input = request with { ApprovalReference = request.ApprovalReference.Trim(), Reason = request.Reason.Trim() };
        return operation.WriteAsync(CompanySettingsPermissions.Manage, "OfficeGeofence", "attendance.office.geofence.publish",
            key, new { branchId, input }, input.Reason, context, async token =>
            {
                // Exact receipt replay precedes date/version checks; it never publishes a second version.
                if (input.EffectiveFrom <= RosterSchedule.Today(clock.GetUtcNow()))
                    throw OrganizationOperation.Conflict("office_boundary_requires_future_date");
                var branch = await repository.FindBranchAsync(branchId, token) ?? throw OrganizationOperation.Missing();
                if (!branch.IsActive) throw OrganizationOperation.Conflict("office_not_active");
                OrganizationOperation.ExpectVersion(Convert.ToBase64String(branch.RowVersion), input.ExpectedBranchVersion);
                var rows = await repository.VersionsAsync(branchId, token);
                ValidateHistory(branchId, rows);
                var latest = rows.MaxBy(x => x.VersionNumber);
                if (input.ExpectedLatestVersionNumber != (latest?.VersionNumber ?? 0))
                    throw OrganizationOperation.Conflict("office_boundary_version_conflict");
                if (rows.Count == 5000 || latest?.VersionNumber == int.MaxValue)
                    throw OrganizationOperation.Conflict("office_boundary_history_limit");
                if (latest is not null && input.EffectiveFrom <= latest.EffectiveFrom)
                    throw OrganizationOperation.Conflict("office_boundary_requires_later_date");
                if (await repository.AnyLockedFromAsync(input.EffectiveFrom, token))
                    throw OrganizationOperation.Conflict("attendance_period_locked");
                var version = new OfficeBoundaryVersionResponse(Guid.NewGuid(), branchId, (latest?.VersionNumber ?? 0) + 1,
                    input.EffectiveFrom, input.Latitude, input.Longitude, input.RadiusMeters, input.MaximumAccuracyMeters,
                    input.JurisdictionCode, input.ApprovalReference, current.Require().AccountId.ToString(), clock.GetUtcNow());
                await repository.AddAsync(version, token);
                return (branchId, (object?)latest, version);
            }, ct, requireSuperAdministrator: true);
    }

    internal static void ValidateHistory(Guid branchId, IReadOnlyList<OfficeBoundaryVersionResponse> rows)
    {
        if (rows.Count > 5000 || rows.Any(x => x.Id == Guid.Empty || x.BranchId != branchId || x.EffectiveFrom == default ||
                !ValidGeometry(x.Latitude, x.Longitude, x.RadiusMeters, x.MaximumAccuracyMeters) ||
                !CompanyJurisdictions.Codes.Contains(x.JurisdictionCode, StringComparer.Ordinal) || !Text(x.ApprovalReference) ||
                !Text(x.RecordedBy) || x.RecordedAtUtc.Offset != TimeSpan.Zero) ||
            rows.Select(x => x.Id).Distinct().Count() != rows.Count ||
            rows.Select(x => x.EffectiveFrom).Distinct().Count() != rows.Count ||
            rows.OrderBy(x => x.EffectiveFrom).Where((x, i) => x.VersionNumber != i + 1).Any())
            throw OrganizationOperation.Conflict("office_boundary_history_unavailable");
    }
    private static void Validate(CreateOfficeBoundaryRequest request)
    {
        OrganizationOperation.Validate(request);
        OrganizationOperation.Version(request.ExpectedBranchVersion);
        if (!request.ManagementApproved) throw OrganizationOperation.Invalid("office_boundary_approval_required");
        if (request.EffectiveFrom.Year is < 1901 or > 9997 || request.ExpectedLatestVersionNumber < 0 ||
            !ValidGeometry(request.Latitude, request.Longitude, request.RadiusMeters, request.MaximumAccuracyMeters) ||
            !CompanyJurisdictions.Codes.Contains(request.JurisdictionCode, StringComparer.Ordinal) ||
            !Text(request.ApprovalReference) || !Text(request.Reason))
            throw OrganizationOperation.Invalid("invalid_office_boundary_values");
    }
    internal static bool ValidGeometry(decimal latitude, decimal longitude, decimal radius, decimal accuracy) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 &&
        radius is > 0 and <= 99999999.99m && accuracy is > 0 and <= 99999999.99m &&
        decimal.Round(latitude, 7) == latitude && decimal.Round(longitude, 7) == longitude &&
        decimal.Round(radius, 2) == radius && decimal.Round(accuracy, 2) == accuracy;
    private static bool Text(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 500 && !value.Any(char.IsControl);
}
