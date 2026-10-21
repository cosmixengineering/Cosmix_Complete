using Cosmix.Domain.HR.VehicleFuel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class VehicleMileageEntryConfiguration : IEntityTypeConfiguration<VehicleMileageEntry>
{
    public void Configure(EntityTypeBuilder<VehicleMileageEntry> builder)
    {
        builder.ToTable("VehicleMileageEntries", "hr", table =>
        {
            table.HasCheckConstraint("CK_VehicleMileageEntries_1", "[ApprovedBusinessKm] >= 0 AND [ExcludedCommuteKm] >= 0 AND [OutstationKm] >= 0 AND [OutstationKm] <= [ApprovedBusinessKm]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ApprovedBusinessKm).HasPrecision(12, 3);
        builder.Property(x => x.ExcludedCommuteKm).HasPrecision(12, 3);
        builder.Property(x => x.OutstationKm).HasPrecision(12, 3);
        builder.Property(x => x.MonthlyAdjustmentKm).HasPrecision(12, 3);
        builder.Property(x => x.CalculationPrivateReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.VehicleTrip).WithMany().HasForeignKey(x => new { x.VehicleTripId, x.EmployeeId, x.VehicleId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId, x.VehicleId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PolicyVersion).WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.VehicleId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.EmployeeId, x.BusinessDate });
    }
}
