using Cosmix.Domain.HR.VehicleFuel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class FuelClaimConfiguration : IEntityTypeConfiguration<FuelClaim>
{
    public void Configure(EntityTypeBuilder<FuelClaim> builder)
    {
        builder.ToTable("FuelClaims", "hr", table =>
        {
            table.HasCheckConstraint("CK_FuelClaims_1", "[EligibleDistanceKm] >= 0 AND [ExcludedCommuteKm] >= 0 AND [FuelLiters] >= 0 AND [ClaimedAmount] >= 0");
            table.HasCheckConstraint("CK_FuelClaims_2", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
            table.HasCheckConstraint("CK_FuelClaims_3", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EligibleDistanceKm).HasPrecision(12, 3);
        builder.Property(x => x.ExcludedCommuteKm).HasPrecision(12, 3);
        builder.Property(x => x.FuelLiters).HasPrecision(12, 3);
        builder.Property(x => x.ClaimedAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.CalculationPrivateReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ReceiptPrivateReference).HasMaxLength(500);
        builder.Property(x => x.RequestedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.VehicleTrip).WithMany().HasForeignKey(x => new { x.VehicleTripId, x.EmployeeId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.EmployeeCommuteDistance).WithMany().HasForeignKey(x => new { x.EmployeeCommuteDistanceId, x.EmployeeId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PolicyVersion).WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
