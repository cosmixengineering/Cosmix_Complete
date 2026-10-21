using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.EmployeeRecords;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeAssetHistoryConfiguration : IEntityTypeConfiguration<EmployeeAssetHistory>
{
    public void Configure(EntityTypeBuilder<EmployeeAssetHistory> builder)
    {
        builder.ToTable("EmployeeAssetHistory", "hr", table =>
        {
            table.HasCheckConstraint("CK_EmployeeAssetHistory_Action", "([Action] = 1 AND [Revision] = 1 AND [ReceiverAccountId] IS NULL) OR ([Action] = 2 AND [Revision] = 2 AND [ReceiverAccountId] IS NOT NULL AND [TransferredFromCustodyId] IS NULL)");
            table.HasCheckConstraint("CK_EmployeeAssetHistory_Time", "[RecordedAt] >= [EffectiveAt]");
        });
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.EmployeeAssetCustodyId, x.Revision }).IsUnique();
        builder.HasIndex(x => new { x.EmployeeId, x.RecordedAt });
        builder.Property(x => x.Condition).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ActorUserId).HasMaxLength(200).IsRequired();
        builder.HasOne<EmployeeAssetCustody>().WithMany().HasForeignKey(x => new { x.EmployeeAssetCustodyId, x.EmployeeId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeAssetCustody>().WithMany().HasForeignKey(x => x.TransferredFromCustodyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x => new { x.EvidenceDocumentId, x.EmployeeId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ReceiverAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
