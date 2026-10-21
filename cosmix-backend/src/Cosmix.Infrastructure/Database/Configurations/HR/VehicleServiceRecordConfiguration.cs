using Cosmix.Domain.HR.VehicleFuel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class VehicleServiceRecordConfiguration : IEntityTypeConfiguration<VehicleServiceRecord>
{
    public void Configure(EntityTypeBuilder<VehicleServiceRecord> builder)
    {
        builder.ToTable("VehicleServiceRecords", "hr", table =>
        {
            table.HasCheckConstraint("CK_VehicleServiceRecords_1", "[OdometerKm] >= 0 AND [ApprovedOutstationKm] >= 0 AND [ClaimedAmount] >= 0");
            table.HasCheckConstraint("CK_VehicleServiceRecords_2", "[ServiceKind] BETWEEN 1 AND 3");
            table.HasCheckConstraint("CK_VehicleServiceRecords_3", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
            table.HasCheckConstraint("CK_VehicleServiceRecords_4", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OdometerKm).HasPrecision(12, 3);
        builder.Property(x => x.ApprovedOutstationKm).HasPrecision(12, 3);
        builder.Property(x => x.AuthorizedWorkshopReference).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EvidencePrivateReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ClaimedAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.EligibilityCalculationPrivateReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RequestedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PolicyVersion).WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
