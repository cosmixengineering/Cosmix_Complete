using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Domain.HR.EmployeeAssignments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class OutstationDeploymentConfiguration : IEntityTypeConfiguration<OutstationDeployment>
{
    public void Configure(EntityTypeBuilder<OutstationDeployment> b)
    {
        b.ToTable("OutstationDeployments", "hr", t => {
            t.HasCheckConstraint("CK_OutstationDeployments_Interval", "[EndsAtUtc] > [StartsAtUtc]");
            t.HasCheckConstraint("CK_OutstationDeployments_Revision", "[Revision] > 0"); });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.Id, x.EmployeeId });
        b.HasIndex(x => new { x.EmployeeId, x.StartsAtUtc, x.EndsAtUtc });
        b.HasIndex(x => x.ApprovalRequestId).IsUnique();
        b.Property(x => x.EvidenceReference).HasMaxLength(500).IsRequired(); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<EmployeeAssignment>().WithMany().HasForeignKey(x => new { x.EmployeeAssignmentId, x.EmployeeId })
            .HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AllowanceClaim>().WithMany().HasForeignKey(x => x.FinalizedClaimId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class DeploymentHomeBreakConfiguration : IEntityTypeConfiguration<DeploymentHomeBreakRequest>
{
    public void Configure(EntityTypeBuilder<DeploymentHomeBreakRequest> b)
    {
        b.ToTable("DeploymentHomeBreakRequests", "hr", t => t.HasCheckConstraint("CK_DeploymentHomeBreak_Return", "[ReturnedAtUtc] IS NULL OR [ReturnedAtUtc] > [DepartedAtUtc]"));
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.Id, x.DeploymentId }); b.HasIndex(x => x.ApprovalRequestId).IsUnique();
        b.HasIndex(x => new { x.DeploymentId, x.DepartedAtUtc });
        b.Property(x => x.EvidenceReference).HasMaxLength(500).IsRequired();
        b.HasOne<OutstationDeployment>().WithMany().HasForeignKey(x => x.DeploymentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DeploymentHomeBreakRequest>().WithMany().HasForeignKey(x => new { x.SupersedesBreakId, x.DeploymentId })
            .HasPrincipalKey(x => new { x.Id, x.DeploymentId }).OnDelete(DeleteBehavior.Restrict);
    }
}
