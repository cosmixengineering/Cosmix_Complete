using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.HR.Holidays;
using Cosmix.Domain.HR.Branches;
using Cosmix.Domain.Attendance.Offices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class RawPunchEventConfiguration : IEntityTypeConfiguration<RawPunchEvent>
{
    public void Configure(EntityTypeBuilder<RawPunchEvent> builder)
    {
        builder.ToTable("RawPunchEvents", "attendance", table =>
        {
            table.HasCheckConstraint("CK_RawPunchEvents_Action", "[Action] IN (1, 2)");
            table.HasCheckConstraint("CK_RawPunchEvents_Coordinates",
                "([Latitude] IS NULL AND [Longitude] IS NULL) OR " +
                "([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL " +
                "AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)");
            table.HasCheckConstraint("CK_RawPunchEvents_Accuracy", "[AccuracyMeters] IS NULL OR [AccuracyMeters] >= 0");
            table.HasCheckConstraint("CK_RawPunchEvents_GeofenceSite", "[GeofenceVersionId] IS NULL OR [SiteId] IS NOT NULL");
            table.HasCheckConstraint("CK_RawPunchEvents_OfficeContext", "([BranchId] IS NULL AND [OfficeRosterAssignmentId] IS NULL AND [OfficeGeofenceVersionId] IS NULL) OR " +
                "([BranchId] IS NOT NULL AND [BusinessDate] IS NOT NULL AND [SiteId] IS NULL AND [GeofenceVersionId] IS NULL AND [SiteRosterAssignmentId] IS NULL AND [TemporaryShiftChangeId] IS NULL)");
            table.HasCheckConstraint("CK_RawPunchEvents_RequestSha256",
                "LEN([RequestSha256]) = 64 AND [RequestSha256] NOT LIKE '%[^0-9A-F]%'");
            table.HasCheckConstraint("CK_RawPunchEvents_ReceivedUtc", "DATEPART(TZOFFSET, [ReceivedAtUtc]) = 0");
            table.HasCheckConstraint("CK_RawPunchEvents_DeviceCapturedUtc",
                "[DeviceCapturedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DeviceCapturedAtUtc]) = 0");
        });
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.EvidenceFile).WithMany().HasForeignKey(x => x.EvidenceFileId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Action).HasConversion<int>();
        builder.Property(x => x.RegisteredDeviceId).HasMaxLength(200).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(x => x.RequestSha256).HasMaxLength(64).IsFixedLength().IsUnicode(false)
            .UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(x => x.ReceivedAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(x => x.DeviceCapturedAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(x => x.Latitude).HasPrecision(10, 7);
        builder.Property(x => x.Longitude).HasPrecision(10, 7);
        builder.Property(x => x.AccuracyMeters).HasPrecision(10, 2);
        builder.Property(x => x.VerificationAttemptReference).HasMaxLength(200);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.HasIndex(x => new { x.EmployeeId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.EmployeeId, x.ReceivedAtUtc });
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GeofenceVersion).WithMany()
            .HasForeignKey(x => new { x.SiteId, x.GeofenceVersionId })
            .HasPrincipalKey(x => new { x.SiteId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.EmployeeAssignment).WithMany()
            .HasForeignKey(x => new { x.EmployeeId, x.EmployeeAssignmentId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ShiftVersion).WithMany().HasForeignKey(x => x.ShiftVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.BusinessDate).HasColumnType("date");
        builder.HasOne<ShiftPopulationAssignment>().WithMany().HasForeignKey(x => x.SiteRosterAssignmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TemporaryShiftChange>().WithMany().HasForeignKey(x => x.TemporaryShiftChangeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<HolidayCalendar>().WithMany().HasForeignKey(x => x.HolidayCalendarId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OfficeRosterAssignment>().WithMany().HasForeignKey(x => new { x.BranchId, x.OfficeRosterAssignmentId })
            .HasPrincipalKey(x => new { x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OfficeGeofenceVersion>().WithMany().HasForeignKey(x => new { x.BranchId, x.OfficeGeofenceVersionId })
            .HasPrincipalKey(x => new { x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
