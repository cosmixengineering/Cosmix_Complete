using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Branches;

namespace Cosmix.Domain.HR.CompanySettings;

public static class CompanyJurisdictions
{
    // Internal location identifiers only; they do not certify legal applicability or statutory rates.
    public static IReadOnlyList<string> Codes { get; } = Array.AsReadOnly<string>(["PK-PB", "PK-SD", "PK-KP", "PK-BA", "PK-ICT"]);
    public static string Require(string code) => Codes.Contains(code, StringComparer.Ordinal) ? code : throw new ArgumentException("Unsupported jurisdiction.", nameof(code));
}
public sealed class CompanyProfile
{
    public static readonly Guid SingletonId = Guid.Parse("0871aaf1-f14d-4dc6-a9ba-03eeb2c3047f");
    private CompanyProfile() { }
    public CompanyProfile(string companyName, string businessType, Guid branchId, Guid cityId, string address,
        string jurisdictionCode, decimal? latitude, decimal? longitude)
    {
        Id = SingletonId; SingletonKey = 1;
        Update(companyName, businessType, branchId, cityId, address, jurisdictionCode, latitude, longitude);
    }
    public Guid Id { get; private set; }
    public byte SingletonKey { get; private set; }
    public string CompanyName { get; private set; } = "";
    public string BusinessType { get; private set; } = "";
    public Guid HeadOfficeBranchId { get; private set; }
    public Guid HeadOfficeCityId { get; private set; }
    public string Address { get; private set; } = "";
    public string JurisdictionCode { get; private set; } = "";
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public Branch HeadOfficeBranch { get; private set; } = null!;
    public City HeadOfficeCity { get; private set; } = null!;
    public void Update(string companyName, string businessType, Guid branchId, Guid cityId, string address,
        string jurisdictionCode, decimal? latitude, decimal? longitude)
    {
        var name = ModelGuard.Required(companyName, 200, nameof(companyName));
        var type = ModelGuard.Required(businessType, 300, nameof(businessType));
        var location = ModelGuard.Required(address, 500, nameof(address));
        CompanyJurisdictions.Require(jurisdictionCode); ModelGuard.Id(branchId, nameof(branchId)); ModelGuard.Id(cityId, nameof(cityId));
        if (latitude.HasValue != longitude.HasValue || latitude is < -90 or > 90 || longitude is < -180 or > 180 ||
            latitude.HasValue && decimal.Round(latitude.Value, 6) != latitude || longitude.HasValue && decimal.Round(longitude.Value, 6) != longitude)
            throw new ArgumentException("Coordinates require a valid pair with at most six decimal places.");
        CompanyName = name; BusinessType = type; Address = location; HeadOfficeBranchId = branchId; HeadOfficeCityId = cityId;
        JurisdictionCode = jurisdictionCode; Latitude = latitude; Longitude = longitude; AdvanceRevision();
    }
    public void AdvanceRevision() => Revision = checked(Revision + 1);
}
