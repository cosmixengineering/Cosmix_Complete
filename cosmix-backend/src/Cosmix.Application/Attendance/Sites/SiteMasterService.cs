using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.Organization;

namespace Cosmix.Application.Attendance.Sites;

public sealed class SiteMasterService(ISiteRepository repository, IOrganizationRepository organization,
    OrganizationOperation operation, ISiteOperationalRepository operational)
{
    public Task<PagedResult<SiteResponse>> ListAsync(OrganizationListRequest request, CancellationToken ct) =>
        operation.ReadAsync(Permissions.SitesRead, token =>
        {
            OrganizationOperation.Validate(request);
            return repository.ListAsync(request, token);
        }, ct);

    public Task<SiteResponse> GetAsync(Guid id, CancellationToken ct) =>
        operation.ReadAsync(Permissions.SitesRead, token => FindAsync(id, token), ct);

    public Task<SiteOperationalDetailsResponse> GetOperationalDetailsAsync(Guid id, CancellationToken ct) =>
        operation.ReadAsync(Permissions.SitesRead, async token =>
        {
            var result = await operational.GetSiteOperationalDetailsAsync(id, token);
            return result ?? throw new AuthenticationFailure("site_not_found", 404);
        }, ct);

    public Task<PagedResult<OrganizationChangeResponse>> HistoryAsync(Guid id, OrganizationListRequest request, CancellationToken ct) =>
        operation.ReadAsync(Permissions.SitesManage, async token =>
        {
            OrganizationOperation.Validate(request);
            await FindAsync(id, token);
            return await organization.HistoryAsync("Site", id, request, token);
        }, ct, requireSystemAdministrator: true);

    public Task<SiteResponse> CreateAsync(CreateSiteRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        OrganizationOperation.Validate(request);
        var details = Details(request.Name, request.Address, request.CityId, request.ClientName,
            request.ProjectId, request.StartsOn, request.EndsOn, request.SupervisorEmployeeId);
        var code = request.Code.ToUpperInvariant();
        return operation.WriteAsync(Permissions.SitesManage, "Site", "organization.sites.create", key,
            new { Code = code, Details = details }, "Created", context, async token =>
            {
                var city = await ValidateReferencesAsync(details, null, token);
                var result = await repository.CreateAsync(code, details, city, token);
                return (result.Id, (object?)null, result);
            }, ct, requireSystemAdministrator: true);
    }

    public Task<SiteResponse> UpdateAsync(Guid id, UpdateSiteRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        OrganizationOperation.Validate(request);
        var version = OrganizationOperation.Version(request.ExpectedVersion);
        var details = Details(request.Name, request.Address, request.CityId, request.ClientName,
            request.ProjectId, request.StartsOn, request.EndsOn, request.SupervisorEmployeeId);
        var reason = request.Reason.Trim();
        return operation.WriteAsync(Permissions.SitesManage, "Site", "organization.sites.update", key,
            new { Id = id, Details = details, request.ExpectedVersion, Reason = reason }, reason, context, async token =>
            {
                var before = await FindAsync(id, token);
                OrganizationOperation.ExpectVersion(before.Version, request.ExpectedVersion);
                var city = await ValidateReferencesAsync(details, before, token);
                var result = await repository.UpdateAsync(id, details, city, version, token);
                return (id, (object?)before, result);
            }, ct, requireSystemAdministrator: true);
    }

    public Task<SiteResponse> SetStatusAsync(Guid id, SetOrganizationStatusRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        OrganizationOperation.Validate(request);
        var version = OrganizationOperation.Version(request.ExpectedVersion);
        var reason = request.Reason.Trim();
        return operation.WriteAsync(Permissions.SitesManage, "Site", "organization.sites.status", key,
            new { Id = id, request.IsActive, request.ExpectedVersion, Reason = reason }, reason, context, async token =>
            {
                var before = await FindAsync(id, token);
                OrganizationOperation.ExpectVersion(before.Version, request.ExpectedVersion);
                if (request.IsActive == true)
                {
                    var details = Details(before.Name, before.Address, before.CityId, before.ClientName,
                        before.ProjectId, before.StartsOn ?? default, before.EndsOn, before.SupervisorEmployeeId);
                    await ValidateReferencesAsync(details, null, token);
                }
                var result = await repository.SetStatusAsync(id, request.IsActive!.Value, version, token);
                return (id, (object?)before, result);
            }, ct, requireSystemAdministrator: true);
    }

    private async Task<SiteResponse> FindAsync(Guid id, CancellationToken ct) =>
        await repository.GetAsync(id, ct) ?? throw new AuthenticationFailure("site_not_found", 404);

    private async Task<string> ValidateReferencesAsync(SiteDetails details, SiteResponse? previous, CancellationToken ct)
    {
        var city = await organization.GetAsync(OrganizationMasterKind.City, details.CityId, ct);
        if (city is null || (!city.IsActive && previous?.CityId != details.CityId))
            throw OrganizationOperation.Invalid("active_city_required");
        if (details.ProjectId.HasValue && previous?.ProjectId != details.ProjectId &&
            !await repository.IsActiveProjectAsync(details.ProjectId.Value, ct))
            throw OrganizationOperation.Invalid("active_project_required");
        if (details.SupervisorEmployeeId.HasValue && previous?.SupervisorEmployeeId != details.SupervisorEmployeeId &&
            !await repository.IsEligibleSupervisorAsync(details.SupervisorEmployeeId.Value, ct))
            throw OrganizationOperation.Invalid("approved_active_supervisor_required");
        return city.Name;
    }

    private static SiteDetails Details(string? name, string? address, Guid? city, string? client,
        Guid? project, DateOnly start, DateOnly? end, Guid? supervisor)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address) ||
            city is null || city == Guid.Empty || project == Guid.Empty || supervisor == Guid.Empty ||
            start == default || end < start)
            throw OrganizationOperation.Invalid("invalid_site_details");
        return new(name.Trim(), address.Trim(), city.Value, string.IsNullOrWhiteSpace(client) ? null : client.Trim(),
            project, start, end, supervisor);
    }
}
