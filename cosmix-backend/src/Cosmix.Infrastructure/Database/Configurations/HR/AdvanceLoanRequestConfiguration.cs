using Cosmix.Domain.HR.AdvancesLoans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class AdvanceLoanRequestConfiguration : IEntityTypeConfiguration<AdvanceLoanRequest>
{
    public void Configure(EntityTypeBuilder<AdvanceLoanRequest> builder)
    {
        builder.ToTable("AdvanceLoanRequests", "hr", table =>
        {
            table.HasCheckConstraint("CK_AdvanceLoanRequests_1", "[RequestedAmount] > 0");
            table.HasCheckConstraint("CK_AdvanceLoanRequests_2", "[Kind] BETWEEN 1 AND 2");
            table.HasCheckConstraint("CK_AdvanceLoanRequests_3", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
            table.HasCheckConstraint("CK_AdvanceLoanRequests_4", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestedAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.RequestedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
