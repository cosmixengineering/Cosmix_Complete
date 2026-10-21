using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.HR.CompanySettings;

namespace Cosmix.Application.HR.CompanySettings;

public sealed class CompanySettingsService(OrganizationOperation operation, ICompanySettingsRepository repository,
    IOrganizationRepository history, TimeProvider clock)
{
    private const string Resource = "CompanyProfile";
    private static readonly IReadOnlyList<CompanyJurisdictionOption> Jurisdictions = Array.AsReadOnly<CompanyJurisdictionOption>([
        new("PK-PB", "Punjab"), new("PK-SD", "Sindh"), new("PK-KP", "Khyber Pakhtunkhwa"), new("PK-BA", "Balochistan"), new("PK-ICT", "Islamabad Capital Territory")]);
    public Task<CompanySettingsResponse> GetAsync(CancellationToken ct) => operation.ReadAsync(CompanySettingsPermissions.Read, async token =>
    {
        var profile = await repository.FindProfileAsync(token);
        return new CompanySettingsResponse(profile is null ? "unavailable" : "configured", profile is null ? null : Map(profile), Jurisdictions, "review_required");
    }, ct, requireSuperAdministrator: true);
    public Task<PagedResult<OrganizationChangeResponse>> HistoryAsync(OrganizationListRequest request, CancellationToken ct) =>
        operation.ReadAsync(CompanySettingsPermissions.Read, token =>
        { OrganizationOperation.Validate(request); return history.HistoryAsync(Resource, CompanyProfile.SingletonId, request, token); }, ct, requireSuperAdministrator: true);
    public Task<IReadOnlyList<CompanySiteJurisdictionResponse>> SiteHistoryAsync(Guid siteId, CancellationToken ct) =>
        operation.ReadAsync(CompanySettingsPermissions.Read, async token =>
        {
            if (siteId == Guid.Empty) throw OrganizationOperation.Invalid("invalid_company_site");
            var rows = await repository.SiteVersionsAsync(siteId, token); Bound(rows);
            return (IReadOnlyList<CompanySiteJurisdictionResponse>)rows.OrderBy(x => x.EffectiveFrom).ThenBy(x => x.ConfigurationRevision).Select(Map).ToArray();
        }, ct, requireSuperAdministrator: true);

    public Task<CompanyProfileResponse> SaveAsync(SaveCompanyProfileRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        Validate(request);
        var input = request with { CompanyName = request.CompanyName.Trim(), BusinessType = request.BusinessType.Trim(), Address = request.Address.Trim(), Reason = request.Reason.Trim() };
        if (input.ExpectedVersion is not null) OrganizationOperation.Version(input.ExpectedVersion);
        return operation.WriteAsync(CompanySettingsPermissions.Manage, Resource, "organization.company.profile.save", key,
            input, input.Reason, context, async token =>
            {
                var profile = await repository.FindProfileAsync(token);
                CompanyProfileResponse? before = null;
                if (profile is not null)
                {
                    if (input.ExpectedVersion is null) throw OrganizationOperation.Conflict("company_version_required");
                    before = Map(profile); Expect(profile, input.ExpectedVersion);
                }
                else if (input.ExpectedVersion is not null) throw OrganizationOperation.Conflict("company_not_configured");
                if (!await repository.HeadOfficeReferencesValidAsync(input.HeadOfficeBranchId, input.HeadOfficeCityId, token))
                    throw OrganizationOperation.Invalid("company_head_office_reference_invalid");
                if (profile is null)
                {
                    profile = new(input.CompanyName, input.BusinessType, input.HeadOfficeBranchId, input.HeadOfficeCityId,
                        input.Address, input.JurisdictionCode, input.Latitude, input.Longitude);
                    repository.Add(profile);
                }
                else profile.Update(input.CompanyName, input.BusinessType, input.HeadOfficeBranchId, input.HeadOfficeCityId,
                    input.Address, input.JurisdictionCode, input.Latitude, input.Longitude);
                await repository.SaveChangesAsync(token);
                return (profile.Id, (object?)before, Map(profile));
            }, ct, requireSuperAdministrator: true);
    }
    public Task<CompanySiteJurisdictionResult> PublishSiteAsync(PublishCompanySiteJurisdictionRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        if (request is null || request.SiteId == Guid.Empty || request.EffectiveFrom.Year is < 1901 or > 9997 || request.SupersedesVersionId == Guid.Empty)
            throw OrganizationOperation.Invalid("invalid_company_site_jurisdiction");
        if (!CompanyJurisdictions.Codes.Contains(request.JurisdictionCode, StringComparer.Ordinal)) throw OrganizationOperation.Invalid("invalid_company_jurisdiction");
        Reason(request.Reason); OrganizationOperation.Version(request.ExpectedCompanyVersion);
        var input = request with { Reason = request.Reason.Trim() };
        return operation.WriteAsync(CompanySettingsPermissions.Manage, Resource, "organization.company.site-jurisdiction.publish", key,
            input, input.Reason, context, async token =>
            {
                // This runs after exact receipt replay, so an original successful retry remains
                // replayable after its publication date; a new retrospective write is rejected.
                if (input.EffectiveFrom < Cosmix.Application.Attendance.Rosters.RosterSchedule.Today(clock.GetUtcNow()))
                    throw OrganizationOperation.Conflict("company_site_retrospective_change_forbidden");
                var profile = await repository.FindProfileAsync(token) ?? throw OrganizationOperation.Conflict("company_not_configured");
                Expect(profile, input.ExpectedCompanyVersion);
                if (!await repository.SiteExistsAsync(input.SiteId, token)) throw OrganizationOperation.Invalid("company_site_unavailable");
                var rows = await repository.SiteVersionsAsync(input.SiteId, token); Bound(rows);
                if (rows.Count == 5000) throw OrganizationOperation.Conflict("company_jurisdiction_history_limit");
                var superseded = rows.Where(x => x.SupersedesVersionId.HasValue).Select(x => x.SupersedesVersionId!.Value).ToHashSet();
                CompanySiteJurisdictionVersion? previous = null;
                if (input.SupersedesVersionId.HasValue)
                {
                    previous = rows.SingleOrDefault(x => x.Id == input.SupersedesVersionId.Value);
                    if (previous is null || previous.CompanyProfileId != profile.Id || previous.EffectiveFrom != input.EffectiveFrom || superseded.Contains(previous.Id))
                        throw OrganizationOperation.Conflict("company_site_supersession_invalid");
                }
                if (rows.Any(x => !superseded.Contains(x.Id) && x.EffectiveFrom == input.EffectiveFrom && x.Id != previous?.Id))
                    throw OrganizationOperation.Conflict("company_site_jurisdiction_conflict");
                var nextDate = rows.Where(x => !superseded.Contains(x.Id) && x.EffectiveFrom > input.EffectiveFrom)
                    .Select(x => (DateOnly?)x.EffectiveFrom).Min();
                if (await repository.AnyLockedAsync(input.EffectiveFrom, nextDate, token))
                    throw OrganizationOperation.Conflict("company_site_period_locked");
                profile.AdvanceRevision();
                var version = new CompanySiteJurisdictionVersion(profile.Id, input.SiteId, input.EffectiveFrom,
                    input.JurisdictionCode, profile.Revision, input.SupersedesVersionId, clock.GetUtcNow());
                repository.Add(version); await repository.SaveChangesAsync(token);
                return (profile.Id, previous is null ? (object?)null : Map(previous), new CompanySiteJurisdictionResult(Map(version), Convert.ToBase64String(profile.RowVersion)));
            }, ct, requireSuperAdministrator: true);
    }
    private void Expect(CompanyProfile profile, string expected)
    { OrganizationOperation.ExpectVersion(Convert.ToBase64String(profile.RowVersion), expected); repository.ExpectVersion(profile, OrganizationOperation.Version(expected)); }
    internal static void Bound(IReadOnlyList<CompanySiteJurisdictionVersion> rows)
    { if (rows.Count > 5000) throw OrganizationOperation.Conflict("company_jurisdiction_history_limit"); }
    private static void Validate(SaveCompanyProfileRequest request)
    {
        if (request is null || !Text(request.CompanyName, 200) || !Text(request.BusinessType, 300) || !Text(request.Address, 500) ||
            request.HeadOfficeBranchId == Guid.Empty || request.HeadOfficeCityId == Guid.Empty ||
            !CompanyJurisdictions.Codes.Contains(request.JurisdictionCode, StringComparer.Ordinal) ||
            request.Latitude.HasValue != request.Longitude.HasValue || request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180 ||
            request.Latitude.HasValue && decimal.Round(request.Latitude.Value, 6) != request.Latitude ||
            request.Longitude.HasValue && decimal.Round(request.Longitude.Value, 6) != request.Longitude)
            throw OrganizationOperation.Invalid("invalid_company_profile");
        Reason(request.Reason);
    }
    private static bool Text(string? value, int length) => !string.IsNullOrWhiteSpace(value) && value.Length <= length && !value.Any(char.IsControl);
    private static void Reason(string value) { if (!Text(value, 500)) throw OrganizationOperation.Invalid("invalid_company_reason"); }
    private static CompanyProfileResponse Map(CompanyProfile p) => new(p.Id, p.CompanyName, p.BusinessType, p.HeadOfficeBranchId,
        p.HeadOfficeCityId, p.Address, p.JurisdictionCode, p.Latitude, p.Longitude, p.Revision, Convert.ToBase64String(p.RowVersion), "review_required");
    private static CompanySiteJurisdictionResponse Map(CompanySiteJurisdictionVersion v) => new(v.Id, v.SiteId, v.EffectiveFrom,
        v.JurisdictionCode, v.ConfigurationRevision, v.SupersedesVersionId, v.CreatedAtUtc);
}
