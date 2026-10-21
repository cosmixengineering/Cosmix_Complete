using Cosmix.Domain.HR.VehicleFuel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class VehicleCustodyConfiguration : IEntityTypeConfiguration<VehicleCustody>
{
    public void Configure(EntityTypeBuilder<VehicleCustody> builder)
    {
        builder.ToTable("VehicleCustodies", "hr", table =>
        {
            table.HasCheckConstraint("CK_VehicleCustodies_1", "[IssueOdometerKm] >= 0");
            table.HasCheckConstraint("CK_VehicleCustodies_2", "[ReturnedAt] IS NULL OR ([ReturnedAt] >= [IssuedAt] AND [ReturnOdometerKm] >= [IssueOdometerKm])");
            table.HasCheckConstraint("CK_VehicleCustodies_3", "([ReturnedAt] IS NULL AND [ReturnOdometerKm] IS NULL AND [ReturnConditionPrivateReference] IS NULL AND [ReturnedToUserId] IS NULL) OR ([ReturnedAt] IS NOT NULL AND [ReturnOdometerKm] IS NOT NULL AND [ReturnConditionPrivateReference] IS NOT NULL AND [ReturnedToUserId] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IssueOdometerKm).HasPrecision(12, 3);
        builder.Property(x => x.IssueConditionPrivateReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.ReturnConditionPrivateReference).HasMaxLength(500);
        builder.Property(x => x.ReturnedToUserId).HasMaxLength(200);
        builder.Property(x => x.ReturnOdometerKm).HasPrecision(12, 3);
        builder.HasIndex(x => x.VehicleId).IsUnique().HasFilter("[ReturnedAt] IS NULL");
    }
}
