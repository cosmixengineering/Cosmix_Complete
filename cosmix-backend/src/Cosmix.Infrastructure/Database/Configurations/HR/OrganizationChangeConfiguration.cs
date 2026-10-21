using Cosmix.Domain.HR.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class OrganizationChangeConfiguration : IEntityTypeConfiguration<OrganizationChange>
{
    public void Configure(EntityTypeBuilder<OrganizationChange> builder)
    {
        builder.ToTable("OrganizationChanges", "hr", table =>
        {
            table.HasCheckConstraint("CK_OrganizationChanges_Json", "ISJSON([AfterJson]) = 1 AND ([BeforeJson] IS NULL OR ISJSON([BeforeJson]) = 1)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Resource).HasMaxLength(40);
        builder.Property(x => x.Operation).HasMaxLength(100);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.BeforeJson).HasMaxLength(524288);
        builder.Property(x => x.AfterJson).HasMaxLength(524288);
        builder.HasOne(x => x.AuditEvent).WithMany().HasForeignKey(x => x.AuditEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.AuditEventId).IsUnique();
        builder.HasIndex(x => new { x.Resource, x.ResourceId });
    }
}
