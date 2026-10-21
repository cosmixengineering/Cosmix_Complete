using Cosmix.Domain.Attendance.Zones;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Scopes;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.Attendance.Punches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class ZoneTypeConfiguration : IEntityTypeConfiguration<ZoneType>
{
    public void Configure(EntityTypeBuilder<ZoneType> b)
    {
        AttendanceMapping.Record(b, "ZoneTypes", ("EntryRule", "[EntryRule] BETWEEN 1 AND 3"));
        AttendanceMapping.Strings(b, ("Code", 30), ("Name", 100), ("DisplayColor", 30)); b.HasIndex(x => x.Code).IsUnique();
    }
}
public sealed class OperationalZoneConfiguration : IEntityTypeConfiguration<OperationalZone>
{
    public void Configure(EntityTypeBuilder<OperationalZone> b)
    {
        AttendanceMapping.Record(b, "OperationalZones");
        AttendanceMapping.Strings(b, ("Code", 50), ("Name", 150)); b.HasIndex(x => x.Code).IsUnique();
        b.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ZoneVersionConfiguration : IEntityTypeConfiguration<ZoneVersion>
{
    public void Configure(EntityTypeBuilder<ZoneVersion> b)
    {
        AttendanceMapping.Record(b, "ZoneVersions", ("Version", "[VersionNumber] > 0"), ("Window", "[EffectiveToUtc] > [EffectiveFromUtc]"),
            ("EntryRule", "[EntryRule] BETWEEN 1 AND 3"),
            ("Authority", "[ApprovalRequestId] IS NOT NULL OR [DirectAuthorityEvidenceReference] IS NOT NULL"),
            ("Boundary", "([BoundaryKind] = 1 AND [Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [RadiusMeters] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180 AND [RadiusMeters] > 0 AND [PolygonGeoJson] IS NULL) OR ([BoundaryKind] = 2 AND [Latitude] IS NULL AND [Longitude] IS NULL AND [RadiusMeters] IS NULL AND [PolygonGeoJson] IS NOT NULL AND ISJSON([PolygonGeoJson]) = 1)"));
        AttendanceMapping.Strings(b, ("EmployeeInstruction", 2000), ("ReasonCode", 100), ("PrivateRestrictionReasonReference", 500), ("ApprovedBy", 200), ("PolygonGeoJson", 32000), ("DirectAuthorityEvidenceReference", 500));
        AttendanceMapping.Coordinates(b); b.Property(x => x.RadiusMeters).HasPrecision(10, 2);
        b.HasIndex(x => new { x.OperationalZoneId, x.VersionNumber }).IsUnique(); b.HasIndex(x => new { x.OperationalZoneId, x.EffectiveFromUtc });
        b.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.OperationalZoneId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ZoneType).WithMany().HasForeignKey(x => x.ZoneTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ZoneAssignmentConfiguration : IEntityTypeConfiguration<ZoneAssignment>
{
    public void Configure(EntityTypeBuilder<ZoneAssignment> b)
    {
        AttendanceMapping.Record(b, "ZoneAssignments", ("Window", "[EffectiveToUtc] > [EffectiveFromUtc]"),
            ("Population", "([PopulationKind] = 1 AND [EmployeeId] IS NOT NULL AND [RoleId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NULL) OR ([PopulationKind] = 2 AND [EmployeeId] IS NULL AND [RoleId] IS NOT NULL AND [TeamId] IS NULL AND [SiteId] IS NULL) OR ([PopulationKind] = 3 AND [EmployeeId] IS NULL AND [RoleId] IS NULL AND [TeamId] IS NOT NULL AND [SiteId] IS NULL) OR ([PopulationKind] = 4 AND [EmployeeId] IS NULL AND [RoleId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NOT NULL)"),
            ("Override", "[IsEmployeeOverride] = 0 OR [PopulationKind] = 1"),
            ("Authority", "[ApprovalRequestId] IS NOT NULL OR [DirectAuthorityEvidenceReference] IS NOT NULL"));
        AttendanceMapping.Strings(b, ("ApprovedBy", 200), ("DirectAuthorityEvidenceReference", 500));
        b.HasIndex(x => new { x.EmployeeId, x.EffectiveFromUtc }); b.HasIndex(x => new { x.RoleId, x.EffectiveFromUtc });
        b.HasIndex(x => new { x.TeamId, x.EffectiveFromUtc }); b.HasIndex(x => new { x.SiteId, x.EffectiveFromUtc });
        b.HasOne(x => x.ZoneVersion).WithMany().HasForeignKey(x => x.ZoneVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class DutyMonitoringWindowConfiguration : IEntityTypeConfiguration<DutyMonitoringWindow>
{
    public void Configure(EntityTypeBuilder<DutyMonitoringWindow> b)
    {
        AttendanceMapping.Record(b, "DutyMonitoringWindows", ("Window", "[EndsAtUtc] > [StartsAtUtc] AND [NoticeShownAtUtc] <= [StartsAtUtc]"),
            ("Provenance", "([RetentionPolicyVersionId] IS NOT NULL AND [ConfigurationVersionId] IS NULL AND [AcceptedCheckInId] IS NULL AND [WorkSessionKey] IS NULL AND [RegisteredDeviceId] IS NULL) OR ([RetentionPolicyVersionId] IS NULL AND [ConfigurationVersionId] IS NOT NULL AND [AcceptedCheckInId] IS NOT NULL AND [WorkSessionKey] IS NOT NULL AND [RegisteredDeviceId] IS NOT NULL)"));
        AttendanceMapping.Strings(b, ("DutyReference", 200)); b.HasIndex(x => new { x.EmployeeId, x.StartsAtUtc });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ZoneAssignment>().WithMany().HasForeignKey(x => x.ZoneAssignmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PolicyVersion>().WithMany().HasForeignKey(x => x.RetentionPolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ZoneMonitoringConfigurationVersion>().WithMany().HasForeignKey(x => x.ConfigurationVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RawPunchEvent>().WithMany().HasForeignKey(x => x.AcceptedCheckInId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RegisteredDevice>().WithMany().HasForeignKey(x => x.RegisteredDeviceId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ZoneEventConfiguration : IEntityTypeConfiguration<ZoneEvent>
{
    public void Configure(EntityTypeBuilder<ZoneEvent> b)
    {
        AttendanceMapping.Record(b, "ZoneEvents", ("Kind", "[Kind] BETWEEN 1 AND 5"), ("Source", "[Source] BETWEEN 1 AND 3"), ("Coordinates", AttendanceMapping.CoordinatesCheck),
            ("LocationRetention", "([LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [AccuracyMeters] IS NOT NULL) OR ([LocationPrivateReference] IS NOT NULL AND [LocationExpiresAtUtc] IS NOT NULL AND [LocationExpiresAtUtc] > [ReceivedAtUtc] AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL) OR ([WrittenClaimPrivateReference] IS NOT NULL AND [WrittenClaimExpiresAtUtc] IS NOT NULL AND [LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL)"),
            ("WrittenClaim", "([WrittenClaimPrivateReference] IS NULL AND [WrittenClaimExpiresAtUtc] IS NULL) OR ([WrittenClaimPrivateReference] IS NOT NULL AND [WrittenClaimExpiresAtUtc] IS NOT NULL AND [Source] = 3 AND [IsOffline] = 1 AND [DeviceCapturedAtUtc] IS NOT NULL AND [DeviceCapturedAtUtc] <= [ReceivedAtUtc] AND [WrittenClaimExpiresAtUtc] > [ReceivedAtUtc] AND [WrittenClaimExpiresAtUtc] > [DeviceCapturedAtUtc] AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL AND [LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL)"));
        AttendanceMapping.Strings(b, ("IdempotencyKey", 100), ("ShownInstruction", 2000), ("LocationPrivateReference", 500), ("WrittenClaimPrivateReference", 500)); AttendanceMapping.Coordinates(b);
        b.Property(x => x.IdempotencyKey).UseCollation("Latin1_General_100_BIN2"); b.HasIndex(x => new { x.EmployeeId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => new { x.EmployeeId, x.ReceivedAtUtc });
        b.HasIndex(x => new { x.LocationExpiresAtUtc, x.Id });
        b.HasIndex(x => new { x.WrittenClaimExpiresAtUtc, x.Id });
        b.HasOne<DutyMonitoringWindow>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.ZoneAssignmentId, x.DutyMonitoringWindowId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.ZoneAssignmentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ZoneAssignment>().WithMany().HasForeignKey(x => new { x.ZoneVersionId, x.ZoneAssignmentId }).HasPrincipalKey(x => new { x.ZoneVersionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ZoneVersion).WithMany().HasForeignKey(x => x.ZoneVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.UserAccountId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RegisteredDevice>().WithMany().HasForeignKey(x => new { x.UserAccountId, x.RegisteredDeviceId }).HasPrincipalKey(x => new { x.UserAccountId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ZoneEventResponseConfiguration : IEntityTypeConfiguration<ZoneEventResponse>
{
    public void Configure(EntityTypeBuilder<ZoneEventResponse> b)
    {
        AttendanceMapping.Record(b, "ZoneEventResponses", ("Kind", "[Kind] BETWEEN 1 AND 3"), ("Reason", "[Kind] = 1 OR [Reason] IS NOT NULL"));
        AttendanceMapping.Strings(b, ("Actor", 200), ("Reason", 2000), ("PrivateEvidenceReference", 500));
        b.HasIndex(x => new { x.ZoneEventId, x.AtUtc });
        b.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.ZoneEventId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ZoneExceptionRequestConfiguration : IEntityTypeConfiguration<ZoneExceptionRequest>
{
    public void Configure(EntityTypeBuilder<ZoneExceptionRequest> b)
    {
        AttendanceMapping.Record(b, "ZoneExceptionRequests", ("Window", "[EndsAtUtc] > [StartsAtUtc]"));
        AttendanceMapping.Strings(b, ("Reason", 2000), ("RequestedBy", 200), ("TaskReference", 200));
        b.HasIndex(x => new { x.EmployeeId, x.StartsAtUtc });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ZoneVersion>().WithMany().HasForeignKey(x => x.ZoneVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
