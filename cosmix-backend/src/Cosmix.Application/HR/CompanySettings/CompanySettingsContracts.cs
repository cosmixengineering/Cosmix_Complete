using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.HR.CompanySettings;

namespace Cosmix.Application.HR.CompanySettings;

public static class CompanySettingsPermissions
{
    public const string Read = "org.company.read";
    public const string Manage = "org.company.manage";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Read, "Read company profile, Head Office and dated site jurisdiction history as current SuperAdmin with explicit All permission", false, true, false),
        new(Manage, "Configure company profile, Head Office and dated site jurisdictions as current SuperAdmin with explicit All permission", false, true, false)
    ];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveCompanyProfileRequest(string CompanyName, string BusinessType, Guid HeadOfficeBranchId,
    Guid HeadOfficeCityId, string Address, string JurisdictionCode, decimal? Latitude, decimal? Longitude,
    string? ExpectedVersion, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishCompanySiteJurisdictionRequest(Guid SiteId, DateOnly EffectiveFrom, string JurisdictionCode,
    string ExpectedCompanyVersion, string Reason, Guid? SupersedesVersionId = null);
public sealed record CompanyJurisdictionOption(string Code, string Label);
public sealed record CompanyProfileResponse(Guid Id, string CompanyName, string BusinessType, Guid HeadOfficeBranchId,
    Guid HeadOfficeCityId, string Address, string JurisdictionCode, decimal? Latitude, decimal? Longitude,
    int Revision, string Version, string LegalStatus);
public sealed record CompanySettingsResponse(string State, CompanyProfileResponse? Profile,
    IReadOnlyList<CompanyJurisdictionOption> Jurisdictions, string LegalStatus);
public sealed record CompanySiteJurisdictionResponse(Guid Id, Guid SiteId, DateOnly EffectiveFrom,
    string JurisdictionCode, int ConfigurationRevision, Guid? SupersedesVersionId, DateTimeOffset CreatedAtUtc);
public sealed record CompanySiteJurisdictionResult(CompanySiteJurisdictionResponse Version, string CompanyVersion);
public interface ICompanySettingsRepository
{
    Task<CompanyProfile?> FindProfileAsync(CancellationToken ct);
    Task<bool> HeadOfficeReferencesValidAsync(Guid branchId, Guid cityId, CancellationToken ct);
    Task<bool> SiteExistsAsync(Guid siteId, CancellationToken ct);
    Task<bool> AnyLockedAsync(DateOnly from, DateOnly? toExclusive, CancellationToken ct);
    Task<IReadOnlyList<CompanySiteJurisdictionVersion>> SiteVersionsAsync(Guid siteId, CancellationToken ct);
    Task<IReadOnlyList<CompanySiteJurisdictionVersion>> VersionsForSitesAsync(IReadOnlyList<Guid> siteIds, DateOnly through, CancellationToken ct);
    void Add(CompanyProfile profile);
    void Add(CompanySiteJurisdictionVersion version);
    void ExpectVersion(CompanyProfile profile, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
