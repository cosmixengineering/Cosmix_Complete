using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Zones;

/// <summary>Explicit management settings, immutable and independent of payroll policy.</summary>
public sealed class ZoneMonitoringConfigurationVersion : IAppendOnlyEntity
{
    private ZoneMonitoringConfigurationVersion() { }
    public ZoneMonitoringConfigurationVersion(Guid siteId, int versionNumber, DateTimeOffset fromUtc, DateTimeOffset toUtc,
        decimal maximumAccuracyMeters, decimal warningDistanceMeters, int minimumIntervalSeconds, int retentionDays,
        int maximumMonitoringSeconds, string visibleNotice, string approvedBy, string reason, DateTimeOffset approvedAtUtc,
        string authorityEvidenceReference,int? liveVisibilitySeconds=null)
    {
        AttendanceModelGuard.Window(fromUtc,toUtc);
        if (maximumAccuracyMeters is <= 0 or > 100000 || warningDistanceMeters is < 0 or > 100000 ||
            minimumIntervalSeconds is < 1 or > 86400 || retentionDays is < 1 or > 3650 ||
            maximumMonitoringSeconds is < 1 or > 172800 || minimumIntervalSeconds > maximumMonitoringSeconds ||
            liveVisibilitySeconds is null or <1 or >86400 || liveVisibilitySeconds<minimumIntervalSeconds || liveVisibilitySeconds>maximumMonitoringSeconds)
            throw new ArgumentException("Explicit bounded zone monitoring settings are required.");
        Id=Guid.NewGuid(); SiteId=ModelGuard.Id(siteId,nameof(siteId)); VersionNumber=AttendanceModelGuard.Revision(versionNumber);
        EffectiveFromUtc=ModelGuard.Utc(fromUtc); EffectiveToUtc=ModelGuard.Utc(toUtc);
        MaximumAccuracyMeters=maximumAccuracyMeters; WarningDistanceMeters=warningDistanceMeters;
        MinimumIntervalSeconds=minimumIntervalSeconds; RetentionDays=retentionDays; MaximumMonitoringSeconds=maximumMonitoringSeconds;
        LiveVisibilitySeconds=liveVisibilitySeconds.Value;
        VisibleNotice=ModelGuard.Required(visibleNotice,2000,nameof(visibleNotice)); ApprovedBy=ModelGuard.Required(approvedBy,200,nameof(approvedBy));
        Reason=ModelGuard.Required(reason,1000,nameof(reason)); ApprovedAtUtc=ModelGuard.Utc(approvedAtUtc);
        AuthorityEvidenceReference=ModelGuard.Required(authorityEvidenceReference,500,nameof(authorityEvidenceReference));
    }
    public Guid Id { get; private set; }
    public Guid SiteId { get; private set; }
    public int VersionNumber { get; private set; }
    public DateTimeOffset EffectiveFromUtc { get; private set; }
    public DateTimeOffset EffectiveToUtc { get; private set; }
    public decimal MaximumAccuracyMeters { get; private set; }
    public decimal WarningDistanceMeters { get; private set; }
    public int MinimumIntervalSeconds { get; private set; }
    public int RetentionDays { get; private set; }
    public int MaximumMonitoringSeconds { get; private set; }
    public int LiveVisibilitySeconds { get; private set; }
    public string VisibleNotice { get; private set; } = "";
    public string ApprovedBy { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public DateTimeOffset ApprovedAtUtc { get; private set; }
    public string AuthorityEvidenceReference { get; private set; } = "";
}
