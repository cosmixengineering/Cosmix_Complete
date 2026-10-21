using Cosmix.Domain.HR.EmployeeRecords;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeMedicalRecordConfiguration : IEntityTypeConfiguration<EmployeeMedicalRecord>
{
    public void Configure(EntityTypeBuilder<EmployeeMedicalRecord> builder)
    {
        builder.ToTable("EmployeeMedicalRecords", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EncryptedPayloadReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.EncryptionKeyReference).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.EmployeeDocument).WithMany().HasForeignKey(x => new { x.EmployeeDocumentId, x.EmployeeId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
    }
}
