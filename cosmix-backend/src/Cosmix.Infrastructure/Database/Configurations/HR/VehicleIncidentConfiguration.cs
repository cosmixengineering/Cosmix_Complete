using Cosmix.Domain.HR.VehicleFuel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class VehicleIncidentConfiguration : IEntityTypeConfiguration<VehicleIncident>
{
    public void Configure(EntityTypeBuilder<VehicleIncident> builder)
    {
        builder.ToTable("VehicleIncidents", "hr", table =>
        {
            table.HasCheckConstraint("CK_VehicleIncidents_1", "([ProposedEmployeeLiability] IS NULL AND [Currency] IS NULL) OR ([ProposedEmployeeLiability] IS NOT NULL AND [ProposedEmployeeLiability] >= 0 AND [Currency] IS NOT NULL)");
            table.HasCheckConstraint("CK_VehicleIncidents_2", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
            table.HasCheckConstraint("CK_VehicleIncidents_3", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IncidentEvidencePrivateReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ThirdPartyDetailsEncryptedReference).HasMaxLength(500);
        builder.Property(x => x.InsuranceClaimReference).HasMaxLength(200);
        builder.Property(x => x.ProposedEmployeeLiability).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.RequestedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.VehicleCustody).WithMany().HasForeignKey(x => x.VehicleCustodyId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
