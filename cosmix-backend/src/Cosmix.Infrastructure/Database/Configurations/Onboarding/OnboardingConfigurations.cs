using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Onboarding;

public sealed class EmployeeSubmissionConfiguration : IEntityTypeConfiguration<EmployeeSubmission>
{
    public void Configure(EntityTypeBuilder<EmployeeSubmission> b)
    {
        b.ToTable("EmployeeSubmissions", "hr", t =>
        {
            t.HasCheckConstraint("CK_EmployeeSubmissions_Versions", "[Version] > 0 AND [DraftRevision] > 0 AND [SnapshotSchemaVersion] > 0");
            t.HasCheckConstraint("CK_EmployeeSubmissions_MobileEnrollment", "[MobileAccessRequested] = 0 OR [ApprovedLoginPhoneNumber] IS NOT NULL");
            t.HasCheckConstraint("CK_EmployeeSubmissions_Provenance",
                "([Provenance] = 0 AND [SubmittedByUserId] IS NOT NULL AND [SubmittedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [ExternalSubmitterReference] IS NULL) OR " +
                "([Provenance] = 1 AND [SubmittedByUserId] IS NULL AND [ExternalSubmitterReference] IS NOT NULL AND LEN(LTRIM(RTRIM([ExternalSubmitterReference]))) > 0 AND [Version] = 1 AND [MobileAccessRequested] = 0 AND [ApprovedLoginPhoneNumber] IS NOT NULL)");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.EmployeeId, x.Id });
        b.HasAlternateKey(x => new { x.EmployeeId, x.Id, x.Provenance });
        b.Property(x => x.Provenance).HasConversion<int>().HasDefaultValue(OnboardingProvenance.HrWorkflow);
        b.Property(x => x.ExternalSubmitterReference).HasMaxLength(200);
        b.HasIndex(x => x.Provenance).IsUnique().HasFilter("[Provenance] = 1");
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmployeeId, x.Version }).IsUnique();
        b.HasIndex(x => new { x.SubmittedAt, x.Id });
        b.Property(x => x.SnapshotStorageKey).HasMaxLength(500).IsRequired();
        b.Property(x => x.SnapshotObjectVersion).HasMaxLength(200).IsRequired();
        b.Property(x => x.SnapshotHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.EncryptionKeyReference).HasMaxLength(200).IsRequired();
        b.Property(x => x.ApprovedLoginPhoneNumber).HasMaxLength(16).IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.MobileAccessRequested).IsRequired();
        // Bootstrap evidence explicitly names an external actor; it never invents a user subject.
    }
}

public sealed class EmployeeOnboardingDecisionConfiguration : IEntityTypeConfiguration<EmployeeOnboardingDecision>
{
    public void Configure(EntityTypeBuilder<EmployeeOnboardingDecision> b)
    {
        b.ToTable("EmployeeOnboardingDecisions", "hr", t =>
        {
            t.HasCheckConstraint("CK_EmployeeOnboardingDecisions_Kind", "[Decision] IN (1,2,3)");
            t.HasCheckConstraint("CK_EmployeeOnboardingDecisions_Reason", "[Decision] = 1 OR ([Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason]))) > 0)");
            t.HasCheckConstraint("CK_EmployeeOnboardingDecisions_Provenance",
                "([Provenance] = 0 AND [ActorUserId] IS NOT NULL AND [ActorUserId] <> '00000000-0000-0000-0000-000000000000' AND [ActorRoleId] IS NOT NULL AND [ActorRoleId] <> '00000000-0000-0000-0000-000000000000' AND [ExternalApproverReference] IS NULL AND [OwnerApprovalReference] IS NULL) OR " +
                "([Provenance] = 1 AND [Decision] = 1 AND [ActorUserId] IS NULL AND [ActorRoleId] IS NULL AND [ExternalApproverReference] IS NOT NULL AND LEN(LTRIM(RTRIM([ExternalApproverReference]))) > 0 AND [OwnerApprovalReference] IS NOT NULL AND LEN(LTRIM(RTRIM([OwnerApprovalReference]))) > 0)");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.EmployeeId, x.Id });
        b.Property(x => x.Provenance).HasConversion<int>().HasDefaultValue(OnboardingProvenance.HrWorkflow);
        b.Property(x => x.ExternalApproverReference).HasMaxLength(200);
        b.Property(x => x.OwnerApprovalReference).HasMaxLength(500);
        b.HasOne(x => x.Submission).WithOne().HasForeignKey<EmployeeOnboardingDecision>(x => new { x.EmployeeId, x.SubmissionId, x.Provenance })
            .HasPrincipalKey<EmployeeSubmission>(x => new { x.EmployeeId, x.Id, x.Provenance }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.SubmissionId).IsUnique();
        b.HasOne<Role>().WithMany().HasForeignKey(x => x.ActorRoleId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Reason).HasMaxLength(2000);
        b.HasIndex(x => new { x.EmployeeId, x.DecidedAt });
    }
}
