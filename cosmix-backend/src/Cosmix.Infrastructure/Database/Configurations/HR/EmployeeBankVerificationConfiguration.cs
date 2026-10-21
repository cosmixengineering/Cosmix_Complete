using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeBankVerificationConfiguration : IEntityTypeConfiguration<EmployeeBankVerification>
{
    public void Configure(EntityTypeBuilder<EmployeeBankVerification> b)
    {
        b.ToTable("EmployeeBankVerifications", "hr", t =>
        {
            t.HasCheckConstraint("CK_EmployeeBankVerifications_Version", "[Version] > 0");
            t.HasCheckConstraint("CK_EmployeeBankVerifications_History", "([Version] = 1 AND [PreviousVerificationId] IS NULL) OR ([Version] > 1 AND [PreviousVerificationId] IS NOT NULL)");
            t.HasCheckConstraint("CK_EmployeeBankVerifications_Predecessor", "[PreviousVerificationId] IS NULL OR [PreviousVerificationId] <> [Id]");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        b.Property(x => x.PrivateEvidenceReference).HasMaxLength(500).IsRequired();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmployeeBankDetails>().WithMany().HasForeignKey(x => new { x.EmployeeBankDetailsId, x.EmployeeId })
            .HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        b.HasAlternateKey(x => new { x.Id, x.EmployeeId, x.EmployeeBankDetailsId });
        b.HasOne<EmployeeBankVerification>().WithMany().HasForeignKey(x => new { x.PreviousVerificationId, x.EmployeeId, x.EmployeeBankDetailsId })
            .HasPrincipalKey(x => new { x.Id, x.EmployeeId, x.EmployeeBankDetailsId }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmployeeBankDetailsId, x.Version }).IsUnique();
        b.HasIndex(x => x.PreviousVerificationId).IsUnique().HasFilter("[PreviousVerificationId] IS NOT NULL");
    }
}
