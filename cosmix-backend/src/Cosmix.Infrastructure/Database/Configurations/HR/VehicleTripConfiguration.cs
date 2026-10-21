using Cosmix.Domain.HR.VehicleFuel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class VehicleTripConfiguration : IEntityTypeConfiguration<VehicleTrip>
{
    public void Configure(EntityTypeBuilder<VehicleTrip> builder)
    {
        builder.ToTable("VehicleTrips", "hr", table =>
        {
            table.HasCheckConstraint("CK_VehicleTrips_1", "[EndedAt] >= [StartedAt]");
            table.HasCheckConstraint("CK_VehicleTrips_2", "[OdometerEndKm] >= [OdometerStartKm] AND [OdometerStartKm] >= 0 AND [MapDistanceKm] >= 0");
            table.HasCheckConstraint("CK_VehicleTrips_3", "[Purpose] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_VehicleTrips_4", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OriginLabel).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DestinationLabel).HasMaxLength(200).IsRequired();
        builder.Property(x => x.OdometerStartKm).HasPrecision(12, 3);
        builder.Property(x => x.OdometerEndKm).HasPrecision(12, 3);
        builder.Property(x => x.MapDistanceKm).HasPrecision(12, 3);
        builder.Property(x => x.RouteEvidencePrivateReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.WorkReference).HasMaxLength(150).IsRequired();
        builder.Property(x => x.RequestedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
