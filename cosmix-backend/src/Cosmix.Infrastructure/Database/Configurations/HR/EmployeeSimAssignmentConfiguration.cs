using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.EmployeeDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeSimAssignmentConfiguration : IEntityTypeConfiguration<EmployeeSimAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeSimAssignment> builder)
    {
        builder.ToTable("EmployeeSimAssignments", "hr", table =>
        {
            table.HasCheckConstraint("CK_EmployeeSimAssignments_1", "[ReturnedOn] IS NULL OR [ReturnedOn] >= [IssuedOn]");
            table.HasCheckConstraint("CK_EmployeeSimAssignments_2", "[Status] BETWEEN 1 AND 3");
            table.HasCheckConstraint("CK_EmployeeSimAssignments_3", "([Status] = 3 AND [ReturnedOn] IS NOT NULL) OR ([Status] <> 3 AND [ReturnedOn] IS NULL)");
            table.HasCheckConstraint("CK_EmployeeSimAssignments_Revision", "[Revision] > 0");
            table.HasCheckConstraint("CK_EmployeeSimAssignments_History", "([Revision] = 1 AND [SupersedesEmployeeSimAssignmentId] IS NULL) OR ([Revision] > 1 AND [SupersedesEmployeeSimAssignmentId] IS NOT NULL)");
            table.HasCheckConstraint("CK_EmployeeSimAssignments_Predecessor", "[SupersedesEmployeeSimAssignmentId] IS NULL OR [SupersedesEmployeeSimAssignmentId] <> [Id]");
            table.HasCheckConstraint("CK_EmployeeSimAssignments_Effective", "[EffectiveOn] >= [IssuedOn] AND ([ReturnedOn] IS NULL OR [EffectiveOn] = [ReturnedOn])");
            table.HasCheckConstraint("CK_EmployeeSimAssignments_ReturnEvidence", "([ReturnDocumentId] IS NULL AND [ReturnedToAccountId] IS NULL) OR ([Status] = 3 AND [ReturnDocumentId] IS NOT NULL AND [ReturnedToAccountId] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.SimReference).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AllowedCreditTierCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SupersedesEmployeeSimAssignment).WithMany()
            .HasForeignKey(x => new { x.SupersedesEmployeeSimAssignmentId, x.EmployeeId })
            .HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SupersedesEmployeeSimAssignmentId).IsUnique()
            .HasFilter("[SupersedesEmployeeSimAssignmentId] IS NOT NULL");
        builder.HasIndex(x => new { x.SimReference, x.EffectiveOn });
        builder.HasIndex(x => new { x.PhoneNumber, x.EffectiveOn });
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x => new { x.IssueDocumentId, x.EmployeeId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x => new { x.ReturnDocumentId, x.EmployeeId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ReturnedToAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeSimAssignment>().WithMany().HasForeignKey(x => x.TransferredFromAssignmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
