using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.CompanySettings;

/// <summary>Immutable dated location evidence. A later EffectiveFrom changes the timeline without
/// editing previous rows; a same-date correction explicitly supersedes one retained version.</summary>
public sealed class CompanySiteJurisdictionVersion
{
    private CompanySiteJurisdictionVersion() { }
    public CompanySiteJurisdictionVersion(Guid companyProfileId, Guid siteId, DateOnly effectiveFrom,
        string jurisdictionCode, int configurationRevision, Guid? supersedesVersionId, DateTimeOffset createdAtUtc)
    {
        if (effectiveFrom.Year is < 1901 or > 9997 || configurationRevision < 2 || supersedesVersionId == Guid.Empty)
            throw new ArgumentException("Invalid jurisdiction revision.");
        Id = Guid.NewGuid(); CompanyProfileId = ModelGuard.Id(companyProfileId, nameof(companyProfileId));
        SiteId = ModelGuard.Id(siteId, nameof(siteId)); EffectiveFrom = effectiveFrom;
        JurisdictionCode = CompanyJurisdictions.Require(jurisdictionCode); ConfigurationRevision = configurationRevision;
        SupersedesVersionId = supersedesVersionId; CreatedAtUtc = ModelGuard.Utc(createdAtUtc);
    }
    public Guid Id { get; private set; }
    public Guid CompanyProfileId { get; private set; }
    public Guid SiteId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public string JurisdictionCode { get; private set; } = "";
    public int ConfigurationRevision { get; private set; }
    public Guid? SupersedesVersionId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public CompanyProfile CompanyProfile { get; private set; } = null!;
    public Site Site { get; private set; } = null!;
    public CompanySiteJurisdictionVersion? SupersedesVersion { get; private set; }
}
