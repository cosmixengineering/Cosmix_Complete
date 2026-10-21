using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeArchiveChangeConfiguration : IEntityTypeConfiguration<EmployeeArchiveChange>
{
    public void Configure(EntityTypeBuilder<EmployeeArchiveChange> builder)
    {
        builder.ToTable("EmployeeArchiveChanges", "hr", table =>
        {
            table.HasCheckConstraint("CK_EmployeeArchiveChanges_State", "[PreviousArchived] <> [IsArchived] AND [EmploymentStatus] IN (4, 5, 6)");
            table.HasCheckConstraint("CK_EmployeeArchiveChanges_RecordedUtc", "DATEPART(TZOFFSET, [RecordedAt]) = 0");
        });
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ActorUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ActorRoleCode).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.EmployeeId, x.RecordedAt, x.Id });
    }
}
