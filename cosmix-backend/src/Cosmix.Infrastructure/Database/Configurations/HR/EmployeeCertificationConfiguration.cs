using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.EmployeeDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeCertificationConfiguration : IEntityTypeConfiguration<EmployeeCertification>
{
    public void Configure(EntityTypeBuilder<EmployeeCertification> builder)
    {
        builder.ToTable("EmployeeCertifications", "hr", table =>
        {
            table.HasCheckConstraint("CK_EmployeeCertifications_1", "[ExpiresOn] IS NULL OR [ExpiresOn] >= [IssuedOn]");
            table.HasCheckConstraint("CK_EmployeeCertifications_Revision", "[Revision] > 0 AND (([Revision] = 1 AND [SupersedesEmployeeCertificationId] IS NULL) OR ([Revision] > 1 AND [SupersedesEmployeeCertificationId] IS NOT NULL))");
            table.HasCheckConstraint("CK_EmployeeCertifications_Status", "[Status] BETWEEN 1 AND 3 AND [EffectiveOn] >= [IssuedOn]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CertificateType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CertificateNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Issuer).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.EmployeeDocument).WithMany().HasForeignKey(x => new { x.EmployeeDocumentId, x.EmployeeId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.EmployeeId, x.ExpiresOn });
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.HasIndex(x => x.SupersedesEmployeeCertificationId).IsUnique().HasFilter("[SupersedesEmployeeCertificationId] IS NOT NULL");
        builder.HasOne<EmployeeCertification>().WithMany().HasForeignKey(x => new { x.SupersedesEmployeeCertificationId, x.EmployeeId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x => new { x.ActionDocumentId, x.EmployeeId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
