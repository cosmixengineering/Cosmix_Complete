using Cosmix.Domain.HR.PolicyEffects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class PolicyImpactAssessmentConfiguration : IEntityTypeConfiguration<PolicyImpactAssessment>
{
    public void Configure(EntityTypeBuilder<PolicyImpactAssessment> builder)
    {
        builder.ToTable("PolicyImpactAssessments", "hr", table =>
        {
            table.HasCheckConstraint("CK_PolicyImpactAssessments_1", "[PeriodEnd] >= [PeriodStart]");
            table.HasCheckConstraint("CK_PolicyImpactAssessments_2", "[AffectedMinutes] >= 0");
            table.HasCheckConstraint("CK_PolicyImpactAssessments_3", "[Kind] BETWEEN 1 AND 6");
            table.HasCheckConstraint("CK_PolicyImpactAssessments_4", "[ProposedKpiPercent] IS NULL OR [ProposedKpiPercent] BETWEEN 0 AND 100");
            table.HasCheckConstraint("CK_PolicyImpactAssessments_5", "([ProposedMoneyImpact] IS NULL AND [Currency] IS NULL) OR ([ProposedMoneyImpact] IS NOT NULL AND [ProposedMoneyImpact] >= 0 AND [Currency] IS NOT NULL)");
            table.HasCheckConstraint("CK_PolicyImpactAssessments_6", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProposedMoneyImpact).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.ProposedKpiPercent).HasPrecision(9, 4);
        builder.Property(x => x.CalculationVersion).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ProtectedCalculationReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PolicyVersion).WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
