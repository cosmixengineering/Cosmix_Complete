using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeDocumentHistoryConfiguration : IEntityTypeConfiguration<EmployeeDocumentHistory>
{
    public void Configure(EntityTypeBuilder<EmployeeDocumentHistory> builder)
    {
        builder.ToTable("EmployeeDocumentHistory", "hr", table =>
        {
            table.HasCheckConstraint("CK_EmployeeDocumentHistory_Revision", "[Revision] > 0");
            table.HasCheckConstraint("CK_EmployeeDocumentHistory_Action", "[Action] BETWEEN 1 AND 9");
        });
        builder.HasKey(h => h.Id);
        builder.HasOne<EmployeeDocument>().WithMany().HasForeignKey(h => new { h.EmployeeDocumentId, h.EmployeeId })
            .HasPrincipalKey(d => new { d.Id, d.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(h => h.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(h => new { h.EmployeeDocumentId, h.Revision }).IsUnique();
        builder.HasIndex(h => new { h.EmployeeId, h.RecordedAt });
        builder.Property(h => h.ActorUserId).HasMaxLength(200).IsRequired();
        builder.Property(h => h.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(h => h.BeforeState).HasMaxLength(20000).IsRequired();
        builder.Property(h => h.AfterState).HasMaxLength(20000).IsRequired();
    }
}
