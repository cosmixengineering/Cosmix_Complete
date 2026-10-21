using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.Attendance.Sites;

namespace Cosmix.Application.Attendance.Sites;

public sealed class SiteGeofenceService(ISiteRepository sites, ISiteGeofenceRepository repository,
    IOrganizationRepository organization, OrganizationOperation operation,
    IAccessManagementRepository transactions, ISiteAttendanceIdentity identity,
    MobileAuthenticationOptions options, TimeProvider clock)
{
    public Task<SiteGeofenceConfigurationResponse> GetAsync(Guid siteId, DateOnly? asOfDate, CancellationToken ct) =>
        operation.ReadAsync(Permissions.SitesRead, async token =>
        {
            var date = asOfDate ?? Today();
            if (date == default) throw OrganizationOperation.Invalid("invalid_geofence_date");
            var site = await FindSiteAsync(siteId, token);
            var latest = await repository.LatestAsync(siteId, token);
            var boundary = await repository.EffectiveAsync(siteId, date, token);
            // Current site activation is not evidence of its historical status. Retain boundary reads.
            return new SiteGeofenceConfigurationResponse(siteId, date, boundary is null ? "not_configured" : "configured",
                site.Version, latest?.VersionNumber ?? 0, boundary is null ? null : SiteGeofenceMapping.Map(boundary));
        }, ct);

    public Task<SiteGeofenceVersionResponse> VersionAsync(Guid siteId, Guid versionId, CancellationToken ct) =>
        operation.ReadAsync(Permissions.SitesRead, async token =>
        {
            await FindSiteAsync(siteId, token);
            var version = await repository.VersionAsync(siteId, versionId, token)
                ?? throw new AuthenticationFailure("geofence_version_not_found", 404);
            return SiteGeofenceMapping.Map(version);
        }, ct);

    public Task<PagedResult<SiteGeofenceVersionResponse>> VersionsAsync(Guid siteId,
        SiteGeofenceListRequest request, CancellationToken ct) =>
        operation.ReadAsync(Permissions.SitesRead, async token =>
        {
            OrganizationOperation.Validate(request);
            await FindSiteAsync(siteId, token);
            return await repository.ListAsync(siteId, request, token);
        }, ct);

    public Task<PagedResult<OrganizationChangeResponse>> HistoryAsync(Guid siteId,
        OrganizationListRequest request, CancellationToken ct) =>
        operation.ReadAsync(Permissions.SitesManage, async token =>
        {
            OrganizationOperation.Validate(request);
            await FindSiteAsync(siteId, token);
            return await organization.HistoryAsync("SiteGeofence", siteId, request, token);
        }, ct, requireSystemAdministrator: true);

    public Task<SiteGeofenceWriteResponse> CreateAsync(Guid siteId, CreateSiteGeofenceRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        Validate(request);
        return operation.WriteAsync(Permissions.SitesManage, "SiteGeofence", "attendance.site.geofence.created",
            key, new { SiteId = siteId, Input = request }, request.Reason.Trim(), context, async token =>
            {
                var site = await FindSiteAsync(siteId, token);
                OrganizationOperation.ExpectVersion(site.Version, request.ExpectedSiteVersion);
                if (!site.IsActive) throw OrganizationOperation.Conflict("site_not_active");
                // Date-only boundaries cannot represent an intra-day change without rewriting earlier evidence.
                if (request.EffectiveFrom <= Today())
                    throw OrganizationOperation.Invalid("geofence_requires_future_date");
                if (site.StartsOn > request.EffectiveFrom || site.EndsOn < request.EffectiveFrom)
                    throw OrganizationOperation.Invalid("geofence_outside_site_dates");
                var previous = await repository.LatestAsync(siteId, token);
                var latest = previous?.VersionNumber ?? 0;
                if (request.ExpectedLatestVersionNumber != latest)
                    throw OrganizationOperation.Conflict("geofence_version_conflict");
                if (previous is not null && request.EffectiveFrom <= previous.EffectiveFrom)
                    throw OrganizationOperation.Conflict("geofence_requires_later_effective_date");
                if (latest == int.MaxValue) throw OrganizationOperation.Conflict("geofence_version_limit_reached");
                if (await repository.HasLockedPeriodFromAsync(request.EffectiveFrom, token))
                    throw OrganizationOperation.Conflict("attendance_period_locked");

                var version = new SiteGeofenceVersion(siteId, latest + 1, request.EffectiveFrom,
                    request.Latitude!.Value, request.Longitude!.Value, request.RadiusMeters!.Value,
                    request.MaximumAccuracyMeters!.Value);
                await repository.AddAsync(version, token);
                var result = new SiteGeofenceWriteResponse(SiteGeofenceMapping.Map(version),
                    request.ApprovalReference.Trim(), true);
                return (siteId, (object?)(previous is null ? null : SiteGeofenceMapping.Map(previous)), result);
            }, ct, requireSystemAdministrator: true);
    }

    public Task<MySiteAttendanceConfigurationResponse> MeAsync(CancellationToken ct) =>
        transactions.InTransactionAsync(async token =>
        {
            var employeeId = await identity.RequireEmployeeAsync(token);
            var date = Today();
            var assignments = await repository.EmployeeSitesAsync(employeeId, date, token);
            if (assignments.Count > 1) throw OrganizationOperation.Conflict("employee_assignment_state_conflict");
            if (assignments.Count == 0 || assignments[0].SiteId is null)
                return new MySiteAttendanceConfigurationResponse(date, "not_assigned", null, null);
            var site = await FindSiteAsync(assignments[0].SiteId!.Value, token);
            var assigned = new AssignedAttendanceSite(site.Id, site.Code, site.Name);
            if (!site.IsActive) return new(date, "site_inactive", assigned, null);
            if (site.StartsOn > date || site.EndsOn < date) return new(date, "site_outside_active_dates", assigned, null);
            var boundary = await repository.EffectiveAsync(site.Id, date, token);
            return new(date, boundary is null ? "not_configured" : "configured", assigned,
                boundary is null ? null : SiteGeofenceMapping.Map(boundary));
        }, ct);

    private async Task<SiteResponse> FindSiteAsync(Guid id, CancellationToken ct) =>
        await sites.GetAsync(id, ct) ?? throw new AuthenticationFailure("site_not_found", 404);

    private static void Validate(CreateSiteGeofenceRequest request)
    {
        OrganizationOperation.Validate(request);
        OrganizationOperation.Version(request.ExpectedSiteVersion);
        if (request.EffectiveFrom == default) throw OrganizationOperation.Invalid("invalid_geofence_date");
        if (request.ManagementApproved != true) throw OrganizationOperation.Invalid("management_approval_required");
        if (string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.ApprovalReference) ||
            request.Reason.Any(char.IsControl) || request.ApprovalReference.Any(char.IsControl))
            throw OrganizationOperation.Invalid("invalid_geofence_evidence");
        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180 ||
            request.RadiusMeters is <= 0 or > 99999999.99m || request.MaximumAccuracyMeters is <= 0 or > 99999999.99m ||
            !Exact(request.Latitude!.Value, 7) || !Exact(request.Longitude!.Value, 7) ||
            !Exact(request.RadiusMeters!.Value, 2) || !Exact(request.MaximumAccuracyMeters!.Value, 2))
            throw OrganizationOperation.Invalid("invalid_geofence_values");
    }

    private static bool Exact(decimal value, int decimals) => decimal.Round(value, decimals) == value;

    private DateOnly Today()
    {
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(options.BusinessTimeZoneId)).DateTime); }
        catch (Exception e) when (e is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new AuthenticationFailure("business_timezone_not_configured", 503); }
    }
}
