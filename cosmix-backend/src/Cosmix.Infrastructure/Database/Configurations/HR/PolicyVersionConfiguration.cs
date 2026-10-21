using Cosmix.Domain.HR.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class PolicyVersionConfiguration : IEntityTypeConfiguration<PolicyVersion>
{
    public void Configure(EntityTypeBuilder<PolicyVersion> builder)
    {
        builder.ToTable("PolicyVersions", "hr", table =>
        {
            table.HasCheckConstraint("CK_PolicyVersions_1", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
            table.HasCheckConstraint("CK_PolicyVersions_2", "[VersionNumber] > 0");
            table.HasCheckConstraint("CK_PolicyVersions_3", "ISJSON([RulesJson]) = 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Authority).HasConversion<int>().HasDefaultValue(PolicyAuthority.ApprovedWorkflow).HasSentinel(PolicyAuthority.ApprovedWorkflow);
        builder.ToTable(t => t.HasCheckConstraint("CK_PolicyVersion_Authority", "([Authority] = 1 AND [ApprovalRequestId] IS NOT NULL) OR ([Authority] = 2 AND [ApprovalRequestId] IS NULL)"));
        builder.Property(x => x.RulesSchemaVersion).HasMaxLength(60).IsRequired();
        builder.Property(x => x.RulesJson).HasMaxLength(32000).IsRequired();
        builder.Property(x => x.PolicyDocumentPrivateReference).HasMaxLength(500);
        builder.Property(x => x.PublishedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Policy).WithMany(x => x.Versions).HasForeignKey(x => x.PolicyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.PolicyId, x.VersionNumber }).IsUnique();
    }
}
