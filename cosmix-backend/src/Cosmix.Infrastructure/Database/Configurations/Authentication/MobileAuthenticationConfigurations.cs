using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Authentication;

public sealed class LoginPhoneBindingConfiguration : IEntityTypeConfiguration<LoginPhoneBinding>
{
    public void Configure(EntityTypeBuilder<LoginPhoneBinding> b)
    {
        b.ToTable("LoginPhoneBindings", "auth", t =>
        {
            t.HasCheckConstraint("CK_LoginPhoneBindings_Dates", "([VerifiedAt] IS NULL OR [VerifiedAt] >= [CreatedAt]) AND ([RevokedAt] IS NULL OR ([RevokedAt] >= [CreatedAt] AND ([VerifiedAt] IS NULL OR [RevokedAt] >= [VerifiedAt])))");
            t.HasCheckConstraint("CK_LoginPhoneBindings_Phone", "DATALENGTH([PhoneNumber]) BETWEEN 3 AND 16 AND LEFT([PhoneNumber], 1) = '+' AND SUBSTRING([PhoneNumber], 2, 1) BETWEEN '1' AND '9' AND SUBSTRING([PhoneNumber], 2, 15) NOT LIKE '%[^0-9]%'");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.UserAccountId, x.Id });
        b.HasOne(x => x.UserAccount).WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ApprovedSubmission).WithMany().HasForeignKey(x => x.ApprovedSubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LoginPhoneChange).WithMany().HasForeignKey(x => new { x.UserAccountId, x.LoginPhoneChangeId })
            .HasPrincipalKey(x => new { x.UserAccountId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.LoginPhoneChangeId).IsUnique().HasFilter("[LoginPhoneChangeId] IS NOT NULL");
        b.Property(x => x.PhoneNumber).HasMaxLength(16).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").IsRequired();
        b.HasIndex(x => x.PhoneNumber).IsUnique().HasFilter("[RevokedAt] IS NULL");
        b.HasIndex(x => x.UserAccountId).IsUnique().HasFilter("[RevokedAt] IS NULL");
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class MobileOtpChallengeConfiguration : IEntityTypeConfiguration<MobileOtpChallenge>
{
    public void Configure(EntityTypeBuilder<MobileOtpChallenge> b)
    {
        b.ToTable("MobileOtpChallenges", "auth", t =>
        {
            t.HasCheckConstraint("CK_MobileOtpChallenges_Dates", "[ExpiresAt] > [IssuedAt] AND ([ConsumedAt] IS NULL OR ([ConsumedAt] >= [IssuedAt] AND [ConsumedAt] < [ExpiresAt])) AND ([RevokedAt] IS NULL OR ([RevokedAt] >= [IssuedAt] AND ([ConsumedAt] IS NULL OR [RevokedAt] >= [ConsumedAt])))");
            t.HasCheckConstraint("CK_MobileOtpChallenges_Attempts", "[SecurityVersion] > 0 AND [MaximumAttempts] > 0 AND [FailedAttempts] BETWEEN 0 AND [MaximumAttempts] AND ([ConsumedAt] IS NULL OR [FailedAttempts] < [MaximumAttempts])");
            t.HasCheckConstraint("CK_MobileOtpChallenges_Purpose", "([Purpose] = 'MobileLogin' AND [Platform] IN ('android','ios')) OR ([Purpose] IN ('WebLogin','WebPasswordReset') AND [Platform] = 'web') OR ([Purpose] = 'PhoneRecovery' AND [Platform] IN ('web','android','ios'))");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.UserAccountId, x.Id });
        b.HasOne(x => x.PhoneBinding).WithMany().HasForeignKey(x => new { x.UserAccountId, x.PhoneBindingId })
            .HasPrincipalKey(x => new { x.UserAccountId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Purpose).HasMaxLength(30).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").IsRequired();
        b.Property(x => x.DeviceHash).HasMaxLength(256).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").IsRequired();
        b.Property(x => x.Platform).HasMaxLength(40).IsRequired(); b.Property(x => x.DeviceName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Verifier).HasMaxLength(256).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.UserAccountId, x.IssuedAt });
        b.HasIndex(x => new { x.PhoneBindingId, x.ExpiresAt });
    }
}

public sealed class LoginPhoneChangeConfiguration : IEntityTypeConfiguration<LoginPhoneChange>
{
    public void Configure(EntityTypeBuilder<LoginPhoneChange> b)
    {
        b.ToTable("LoginPhoneChanges", "auth", t =>
        {
            t.HasCheckConstraint("CK_LoginPhoneChanges_Approval", "[ApprovedByUserId] <> [UserAccountId] AND [ApprovedBySystemRole] IN (1,2) AND [SecurityVersion] > 1 AND [ExpiresAt] > [ApprovedAt] AND [ExpiresAt] <= DATEADD(hour,24,[ApprovedAt])");
            t.HasCheckConstraint("CK_LoginPhoneChanges_Completion", "([CompletedAt] IS NULL AND [CompletionChallengeId] IS NULL) OR ([CompletedAt] IS NOT NULL AND [CompletionChallengeId] IS NOT NULL AND [CompletedAt] >= [ApprovedAt] AND [CompletedAt] < [ExpiresAt] AND [SupersededAt] IS NULL)");
            t.HasCheckConstraint("CK_LoginPhoneChanges_Superseded", "[SupersededAt] IS NULL OR ([SupersededAt] >= [ApprovedAt] AND [CompletedAt] IS NULL)");
            t.HasCheckConstraint("CK_LoginPhoneChanges_Phone", "DATALENGTH([NewPhoneNumber]) BETWEEN 3 AND 16 AND LEFT([NewPhoneNumber],1)='+' AND SUBSTRING([NewPhoneNumber],2,1) BETWEEN '1' AND '9' AND SUBSTRING([NewPhoneNumber],2,15) NOT LIKE '%[^0-9]%'");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.UserAccountId, x.Id });
        b.HasOne(x => x.PreviousBinding).WithMany().HasForeignKey(x => new { x.UserAccountId, x.PreviousBindingId })
            .HasPrincipalKey(x => new { x.UserAccountId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ApprovedByAssignment).WithMany().HasForeignKey(x => new { x.ApprovedByUserId, x.ApprovedByAssignmentId })
            .HasPrincipalKey(x => new { x.UserAccountId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MobileOtpChallenge>().WithMany().HasForeignKey(x => new { x.UserAccountId, x.CompletionChallengeId })
            .HasPrincipalKey(x => new { x.UserAccountId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.PreviousBindingId).IsUnique();
        b.HasIndex(x => x.CompletionChallengeId).IsUnique().HasFilter("[CompletionChallengeId] IS NOT NULL");
        b.Property(x => x.NewPhoneNumber).HasMaxLength(16).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").IsRequired();
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired(); b.Property(x => x.ApprovalReference).HasMaxLength(500).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class OtpDeliveryConfiguration : IEntityTypeConfiguration<OtpDelivery>
{
    public void Configure(EntityTypeBuilder<OtpDelivery> b)
    {
        b.ToTable("OtpDeliveries", "auth", t =>
        {
            t.HasCheckConstraint("CK_OtpDeliveries_Lifetime", "[ExpiresAt] > [CreatedAt]");
            t.HasCheckConstraint("CK_OtpDeliveries_Status", "[Status] IN (0,1,2,3,4) AND ([Status] <> 0 OR [Attempts] = 0) AND ([Status] NOT IN (1,4) OR [Attempts] = 1)");
            t.HasCheckConstraint("CK_OtpDeliveries_Claim", "([Attempts] = 0 AND [ClaimedAt] IS NULL AND [LeaseUntil] IS NULL) OR ([Attempts] = 1 AND [ClaimedAt] IS NOT NULL AND [LeaseUntil] IS NOT NULL AND [ClaimedAt] >= [CreatedAt] AND [ClaimedAt] < [ExpiresAt] AND [LeaseUntil] > [ClaimedAt] AND [LeaseUntil] <= [ExpiresAt])");
            t.HasCheckConstraint("CK_OtpDeliveries_Payload", "([Status] IN (0,4) AND [ProtectedPayload] IS NOT NULL AND DATALENGTH([ProtectedPayload]) > 0) OR ([Status] IN (1,2,3) AND [ProtectedPayload] IS NULL)");
            t.HasCheckConstraint("CK_OtpDeliveries_Submitted", "([Status] = 1 AND [SubmittedAt] IS NOT NULL AND [ClaimedAt] IS NOT NULL AND [LeaseUntil] IS NOT NULL AND [SubmittedAt] >= [ClaimedAt] AND [SubmittedAt] < [LeaseUntil] AND [SubmittedAt] < [ExpiresAt]) OR ([Status] <> 1 AND [SubmittedAt] IS NULL)");
        });
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Challenge).WithOne().HasForeignKey<OtpDelivery>(x => x.ChallengeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.ChallengeId).IsUnique();
        b.Property(x => x.ProtectedPayload).HasMaxLength(8000).IsUnicode(false);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.Status, x.ExpiresAt, x.CreatedAt });
        b.HasIndex(x => new { x.Status, x.LeaseUntil });
    }
}

public sealed class AuthRateBucketConfiguration : IEntityTypeConfiguration<AuthRateBucket>
{
    public void Configure(EntityTypeBuilder<AuthRateBucket> b)
    {
        b.ToTable("AuthRateBuckets", "auth", t =>
        {
            t.HasCheckConstraint("CK_AuthRateBuckets_Window", "[WindowEndsAt] > [WindowStartsAt]");
            t.HasCheckConstraint("CK_AuthRateBuckets_Count", "[Count] >= 0");
        });
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(150).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.WindowEndsAt);
    }
}
