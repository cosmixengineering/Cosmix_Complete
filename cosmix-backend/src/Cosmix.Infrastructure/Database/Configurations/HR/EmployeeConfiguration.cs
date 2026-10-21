using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees", "hr", table =>
        {
            table.HasCheckConstraint("CK_Employees_Status", "[Status] BETWEEN 0 AND 6");
            table.HasCheckConstraint("CK_Employees_Archive", "([IsArchived] = 0 AND [ArchivedAt] IS NULL) OR ([IsArchived] = 1 AND [ArchivedAt] IS NOT NULL AND [Status] IN (4, 5, 6))");
            table.HasCheckConstraint("CK_Employees_ArchiveUtc", "[ArchivedAt] IS NULL OR DATEPART(TZOFFSET, [ArchivedAt]) = 0");
            table.HasCheckConstraint("CK_Employees_OnboardingStatus", "[OnboardingStatus] BETWEEN 0 AND 4");
            table.HasCheckConstraint("CK_Employees_SubmittedVersion", "[SubmittedVersion] >= 0 AND [DraftRevision] > 0");
            table.HasCheckConstraint("CK_Employees_OnboardingApproval", "([OnboardingStatus] = 2 AND [ApprovedOnboardingDecisionId] IS NOT NULL) OR ([OnboardingStatus] <> 2 AND [ApprovedOnboardingDecisionId] IS NULL)");
            table.HasCheckConstraint("CK_Employees_Number", "[EmployeeNumber] > 0");
            table.HasCheckConstraint("CK_Employees_MobileEnrollment", "[MobileAccessRequested] = 0 OR [ApprovedLoginPhoneNumber] IS NOT NULL");
            table.HasCheckConstraint("CK_Employees_IdentityType",
                "[IdentityDocumentType] IS NULL OR [IdentityDocumentType] IN (1, 2)");
            table.HasCheckConstraint("CK_Employees_IdentityPair",
                "([IdentityDocumentNumber] IS NULL AND [NormalizedIdentityDocumentNumber] IS NULL) OR " +
                "([IdentityDocumentNumber] IS NOT NULL AND [NormalizedIdentityDocumentNumber] IS NOT NULL)");
            table.HasCheckConstraint("CK_Employees_OfficialEmailPair",
                "([OfficialEmail] IS NULL AND [NormalizedOfficialEmail] IS NULL) OR " +
                "([OfficialEmail] IS NOT NULL AND [NormalizedOfficialEmail] IS NOT NULL)");
            table.HasCheckConstraint("CK_Employees_ProbationDates",
                "[JoinedOn] IS NULL OR [ProbationEndsOn] IS NULL OR [ProbationEndsOn] >= [JoinedOn]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsArchived).HasDefaultValue(false);
        builder.HasOne<EmployeeStatusChange>().WithMany().HasForeignKey(x => new { x.LastAppliedStatusChangeId, x.Id })
            .HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.IsArchived, x.CreatedAt, x.Id });
        builder.Property(x => x.DraftRevision).HasDefaultValue(1);
        builder.HasOne<EmployeeOnboardingDecision>().WithMany().HasForeignKey(x => new { x.Id, x.ApprovedOnboardingDecisionId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.OnboardingStatus, x.CreatedAt, x.Id });
        var number = builder.Property(x => x.EmployeeNumber)
            .HasDefaultValueSql("NEXT VALUE FOR [hr].[EmployeeNumbers]").ValueGeneratedOnAdd();
        number.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Throw);
        number.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.HasIndex(x => x.EmployeeNumber).IsUnique();
        builder.Property(x => x.FullLegalName).HasMaxLength(200);
        builder.Property(x => x.FatherOrSpouseName).HasMaxLength(200);
        builder.Property(x => x.IdentityDocumentNumber).HasMaxLength(50);
        builder.Property(x => x.NormalizedIdentityDocumentNumber).HasMaxLength(50);
        builder.HasIndex(x => new { x.IdentityDocumentType, x.NormalizedIdentityDocumentNumber })
            .IsUnique().HasFilter("[IdentityDocumentType] IS NOT NULL AND [NormalizedIdentityDocumentNumber] IS NOT NULL");
        builder.Property(x => x.Gender).HasMaxLength(40);
        builder.Property(x => x.MaritalStatus).HasMaxLength(40);
        builder.Property(x => x.PersonalPhone).HasMaxLength(30);
        builder.Property(x => x.CompanyPhone).HasMaxLength(30);
        builder.Property(x => x.ApprovedLoginPhoneNumber).HasMaxLength(16).IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
        builder.Property(x => x.MobileAccessRequested).IsRequired();
        builder.Property(x => x.SimStatus).HasMaxLength(40);
        builder.Property(x => x.SimCreditTier).HasMaxLength(50);
        builder.Property(x => x.PersonalEmail).HasMaxLength(320);
        builder.Property(x => x.OfficialEmail).HasMaxLength(320);
        builder.Property(x => x.NormalizedOfficialEmail).HasMaxLength(320);
        builder.HasIndex(x => x.NormalizedOfficialEmail).IsUnique()
            .HasFilter("[NormalizedOfficialEmail] IS NOT NULL");
        builder.Property(x => x.CreatedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.Status, x.FullLegalName, x.Id });
    }
}
