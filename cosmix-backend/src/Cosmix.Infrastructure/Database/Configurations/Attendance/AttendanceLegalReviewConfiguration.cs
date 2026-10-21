using Cosmix.Domain.Attendance.LegalReview;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class AttendanceLegalReviewConfiguration : IEntityTypeConfiguration<AttendanceLegalReviewDossier>
{
    public void Configure(EntityTypeBuilder<AttendanceLegalReviewDossier> b)
    {
        AttendanceMapping.Record(b, "AttendanceLegalReviewDossiers",
            ("Revision", "[Revision] BETWEEN 1 AND 128 AND (([Revision] = 1 AND [SupersedesId] IS NULL) OR ([Revision] > 1 AND [SupersedesId] IS NOT NULL))"),
            ("Dates", "YEAR([EffectiveFrom]) BETWEEN 1901 AND 9997 AND ([EffectiveTo] IS NULL OR (YEAR([EffectiveTo]) BETWEEN 1901 AND 9997 AND [EffectiveTo] > [EffectiveFrom]))"),
            ("RecordOnly", "[State] = 'review_required' AND [VerificationMode] = 'recorded_not_legally_verified'"),
            ("Predecessor", "[SupersedesId] IS NULL OR [SupersedesId] <> [Id]"));
        AttendanceMapping.Strings(b, ("ProtectedEvidenceReference", 500), ("State", 32), ("VerificationMode", 48));
        foreach (var name in new[] { "PolicySha256", "BindingSha256", "EvidenceSha256" })
        {
            b.Property<string>(name).HasMaxLength(64).IsFixedLength().IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
            b.ToTable("AttendanceLegalReviewDossiers", "attendance", table =>
                table.HasCheckConstraint($"CK_AttendanceLegalReviewDossiers_{name}", $"LEN([{name}]) = 64 AND [{name}] NOT LIKE '%[^0-9A-F]%'"));
        }
        b.Property(x => x.State).IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.VerificationMode).IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
        b.Ignore(x => x.ActivationAllowed);
        b.HasIndex(x => new { x.BindingSha256, x.Revision }).IsUnique();
        b.HasIndex(x => x.SupersedesId).IsUnique().HasFilter("[SupersedesId] IS NOT NULL");
        b.HasOne<PolicyVersion>().WithMany().HasForeignKey(x => new { x.SourcePolicyId, x.SourcePolicyVersionId })
            .HasPrincipalKey(x => new { x.PolicyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RecordedByAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AttendanceLegalReviewDossier>().WithMany()
            .HasForeignKey(x => new { x.SourcePolicyVersionId, x.PolicySha256, x.BindingSha256, x.SupersedesId })
            .HasPrincipalKey(x => new { x.SourcePolicyVersionId, x.PolicySha256, x.BindingSha256, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
