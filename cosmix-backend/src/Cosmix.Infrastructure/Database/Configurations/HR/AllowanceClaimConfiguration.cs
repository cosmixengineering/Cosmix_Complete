using Cosmix.Domain.HR.Allowances;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class AllowanceClaimConfiguration : IEntityTypeConfiguration<AllowanceClaim>
{
    public void Configure(EntityTypeBuilder<AllowanceClaim> builder)
    {
        builder.ToTable("AllowanceClaims", "hr", table =>
        {
            table.HasCheckConstraint("CK_AllowanceClaims_1", "[EligibilityPeriodEnd] > [EligibilityPeriodStart]");
            table.HasCheckConstraint("CK_AllowanceClaims_2", "[Kind] BETWEEN 1 AND 6");
            table.HasCheckConstraint("CK_AllowanceClaims_3", "[ClaimedAmount] >= 0");
            table.HasCheckConstraint("CK_AllowanceClaims_4", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
            table.HasCheckConstraint("CK_AllowanceClaims_5", "[Kind] <> 5 OR [DeploymentApprovalRequestId] IS NOT NULL");
            table.HasCheckConstraint("CK_AllowanceClaims_6", "[Kind] NOT BETWEEN 1 AND 4 OR [WorkSessionId] IS NOT NULL");
            table.HasCheckConstraint("CK_AllowanceClaims_7", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
            table.HasCheckConstraint("CK_AllowanceClaims_SubmissionVersion", "([SubmissionVersion] = 0 AND [SubmittedAt] IS NULL) OR ([SubmissionVersion] > 0 AND [SubmittedAt] IS NOT NULL)");
            table.HasCheckConstraint("CK_AllowanceClaims_Settlement", "([SettlementState] = 0 AND [LastSettledAt] IS NULL) OR ([SettlementState] BETWEEN 1 AND 2 AND [LastSettledAt] IS NOT NULL AND [SubmittedAt] IS NOT NULL AND [LastSettledAt] >= [SubmittedAt])");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeploymentCategoryCode).HasMaxLength(60).IsRequired();
        builder.Property(x => x.EligibilityKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ClaimedAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.CalculationPrivateReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.EvidencePrivateReference).HasMaxLength(500);
        builder.Property(x => x.RequestedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PolicyVersion).WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.EmployeeAssignment).WithMany().HasForeignKey(x => new { x.EmployeeAssignmentId, x.EmployeeId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DeploymentApprovalRequest).WithMany().HasForeignKey(x => x.DeploymentApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.WorkSession).WithMany().HasForeignKey(x => new { x.WorkSessionId, x.EmployeeId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.EmployeeId, x.Kind, x.EligibilityKey }).IsUnique();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Navigation(x => x.Submissions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
