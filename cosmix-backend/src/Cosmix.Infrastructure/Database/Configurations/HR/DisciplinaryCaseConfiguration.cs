using Cosmix.Domain.HR.PolicyEffects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class DisciplinaryCaseConfiguration : IEntityTypeConfiguration<DisciplinaryCase>
{
    public void Configure(EntityTypeBuilder<DisciplinaryCase> builder)
    {
        builder.ToTable("DisciplinaryCases", "hr", table =>
        {
            table.HasCheckConstraint("CK_DisciplinaryCases_1", "[State] BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_DisciplinaryCases_2", "[ClosedAt] IS NULL OR [ClosedAt] >= [OpenedAt]");
            table.HasCheckConstraint("CK_DisciplinaryCases_3", "([State] <> 2 AND [ClosedAt] IS NULL AND [ApprovedOutcomePrivateReference] IS NULL) OR ([State] = 2 AND [ClosedAt] IS NOT NULL AND [ApprovedOutcomePrivateReference] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReasonCode).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ProtectedEvidenceReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.OpenedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PolicyImpactAssessment).WithMany().HasForeignKey(x => new { x.PolicyImpactAssessmentId, x.EmployeeId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.ApprovedOutcomePrivateReference).HasMaxLength(500);
    }
}
