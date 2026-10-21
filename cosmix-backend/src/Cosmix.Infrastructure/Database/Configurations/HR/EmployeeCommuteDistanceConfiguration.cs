using Cosmix.Domain.HR.VehicleFuel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeCommuteDistanceConfiguration : IEntityTypeConfiguration<EmployeeCommuteDistance>
{
    public void Configure(EntityTypeBuilder<EmployeeCommuteDistance> builder)
    {
        builder.ToTable("EmployeeCommuteDistances", "hr", table =>
        {
            table.HasCheckConstraint("CK_EmployeeCommuteDistances_1", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
            table.HasCheckConstraint("CK_EmployeeCommuteDistances_2", "[UsualHomeOfficeKm] >= 0");
            table.HasCheckConstraint("CK_EmployeeCommuteDistances_Revision", "[Revision] > 0");
            table.HasCheckConstraint("CK_EmployeeCommuteDistances_History", "([Revision] = 1 AND [SupersedesEmployeeCommuteDistanceId] IS NULL) OR ([Revision] > 1 AND [SupersedesEmployeeCommuteDistanceId] IS NOT NULL)");
            table.HasCheckConstraint("CK_EmployeeCommuteDistances_Predecessor", "[SupersedesEmployeeCommuteDistanceId] IS NULL OR [SupersedesEmployeeCommuteDistanceId] <> [Id]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UsualHomeOfficeKm).HasPrecision(12, 3);
        builder.Property(x => x.RoutePrivateReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SupersedesEmployeeCommuteDistance).WithMany()
            .HasForeignKey(x => new { x.SupersedesEmployeeCommuteDistanceId, x.EmployeeId })
            .HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SupersedesEmployeeCommuteDistanceId).IsUnique()
            .HasFilter("[SupersedesEmployeeCommuteDistanceId] IS NOT NULL");
    }
}
