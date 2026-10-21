using Cosmix.Domain.Attendance.Zones;
using Cosmix.Domain.Attendance.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class ZoneMonitoringConfigurationMapping : IEntityTypeConfiguration<ZoneMonitoringConfigurationVersion>
{
    public void Configure(EntityTypeBuilder<ZoneMonitoringConfigurationVersion> b)
    {
        AttendanceMapping.Record(b,"ZoneMonitoringConfigurationVersions",("Window","[EffectiveToUtc] > [EffectiveFromUtc]"),
            ("Values","[VersionNumber] > 0 AND [MaximumAccuracyMeters] > 0 AND [MaximumAccuracyMeters] <= 100000 AND [WarningDistanceMeters] BETWEEN 0 AND 100000 AND [MinimumIntervalSeconds] BETWEEN 1 AND 86400 AND [RetentionDays] BETWEEN 1 AND 3650 AND [MaximumMonitoringSeconds] BETWEEN 1 AND 172800 AND [MinimumIntervalSeconds] <= [MaximumMonitoringSeconds] AND [LiveVisibilitySeconds] BETWEEN 1 AND 86400 AND [LiveVisibilitySeconds] >= [MinimumIntervalSeconds] AND [LiveVisibilitySeconds] <= [MaximumMonitoringSeconds]"));
        AttendanceMapping.Strings(b,("VisibleNotice",2000),("ApprovedBy",200),("Reason",1000),("AuthorityEvidenceReference",500));
        b.Property(x=>x.MaximumAccuracyMeters).HasPrecision(10,2); b.Property(x=>x.WarningDistanceMeters).HasPrecision(10,2);
        b.HasIndex(x=>new{x.SiteId,x.VersionNumber}).IsUnique(); b.HasIndex(x=>new{x.SiteId,x.EffectiveFromUtc});
        b.HasOne<Site>().WithMany().HasForeignKey(x=>x.SiteId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ZoneAssignmentEndMapping : IEntityTypeConfiguration<ZoneAssignmentEnd>
{
    public void Configure(EntityTypeBuilder<ZoneAssignmentEnd> b)
    {
        AttendanceMapping.Record(b,"ZoneAssignmentEnds",("Future","[EffectiveToUtc] >= [RecordedAtUtc]"));
        AttendanceMapping.Strings(b,("Actor",200),("Reason",1000),("AuthorityEvidenceReference",500));
        b.HasIndex(x=>x.ZoneAssignmentId).IsUnique();
        b.HasOne<ZoneAssignment>().WithMany().HasForeignKey(x=>x.ZoneAssignmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ZoneMonitoringCheckpointMapping : IEntityTypeConfiguration<ZoneMonitoringCheckpoint>
{
    public void Configure(EntityTypeBuilder<ZoneMonitoringCheckpoint> b)
    {
        AttendanceMapping.Record(b,"ZoneMonitoringCheckpoints",("Position","([LastPosition] IS NULL AND [LastReceivedAtUtc] IS NULL AND [LocationId] IS NULL AND [LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [LiveVisibleUntilUtc] IS NULL) OR ([LastPosition] IS NOT NULL AND [LastPosition] BETWEEN 1 AND 3 AND [LastReceivedAtUtc] IS NOT NULL AND [LocationId] IS NOT NULL AND [LocationPrivateReference] IS NOT NULL AND [LocationExpiresAtUtc] IS NOT NULL AND [LiveVisibleUntilUtc] IS NOT NULL AND [LiveVisibleUntilUtc] > [LastReceivedAtUtc] AND [LocationExpiresAtUtc] >= [LiveVisibleUntilUtc])"));
        AttendanceMapping.Strings(b,("LocationPrivateReference",500));
        b.HasIndex(x=>x.DutyMonitoringWindowId).IsUnique();
        b.HasOne<DutyMonitoringWindow>().WithMany().HasForeignKey(x=>x.DutyMonitoringWindowId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ZoneLocationDeletionMapping : IEntityTypeConfiguration<ZoneLocationDeletion>
{
    public void Configure(EntityTypeBuilder<ZoneLocationDeletion> b)
    {
        AttendanceMapping.Record(b,"ZoneLocationDeletions",("Expiry","[DeletedAtUtc] >= [ExpiresAtUtc]"));
        AttendanceMapping.Strings(b,("ReferenceSha256",64));
        b.HasIndex(x=>x.ZoneEventId).IsUnique();
        b.HasOne<ZoneEvent>().WithMany().HasForeignKey(x=>x.ZoneEventId).OnDelete(DeleteBehavior.Restrict);
    }
}
