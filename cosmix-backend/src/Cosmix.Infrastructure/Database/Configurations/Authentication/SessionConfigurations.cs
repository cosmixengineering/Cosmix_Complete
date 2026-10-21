using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Authentication;

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> b)
    {
        b.ToTable("UserSessions", "auth", t =>
        {
            t.HasCheckConstraint("CK_UserSessions_Lifetime", "[ExpiresAt] > [CreatedAt] AND ([RevokedAt] IS NULL OR [RevokedAt] >= [CreatedAt])");
            t.HasCheckConstraint("CK_UserSessions_Client", "[Client] IN (1,2) AND ([Client] <> 2 OR [RegisteredDeviceId] IS NOT NULL)");
            t.HasCheckConstraint("CK_UserSessions_Version", "[SecurityVersion] > 0");
            t.HasCheckConstraint("CK_UserSessions_SmsProof", "[SmsOtpChallengeId] IS NULL OR ([Client] = 1 AND [MfaChallengeId] IS NULL AND [RegisteredDeviceId] IS NULL)");
        });
        b.HasKey(x => x.Id); b.HasOne(x => x.UserAccount).WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RegisteredDevice).WithMany().HasForeignKey(x => new { x.UserAccountId, x.RegisteredDeviceId })
            .HasPrincipalKey(x => new { x.UserAccountId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MfaChallenge>().WithMany().HasForeignKey(x => new { x.UserAccountId, x.MfaChallengeId })
            .HasPrincipalKey(x => new { x.UserAccountId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MobileOtpChallenge>().WithMany().HasForeignKey(x => new { x.UserAccountId, x.SmsOtpChallengeId })
            .HasPrincipalKey(x => new { x.UserAccountId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.SmsOtpChallengeId).IsUnique().HasFilter("[SmsOtpChallengeId] IS NOT NULL");
        b.HasIndex(x => x.RefreshTokenFamilyId).IsUnique(); b.HasIndex(x => x.MfaChallengeId).IsUnique().HasFilter("[MfaChallengeId] IS NOT NULL");
        b.Property(x => x.RevocationReason).HasMaxLength(1000); b.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => new { x.UserAccountId, x.RevokedAt, x.ExpiresAt });
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens", "auth", t =>
        {
            t.HasCheckConstraint("CK_RefreshTokens_Lifetime", "[ExpiresAt] > [IssuedAt] AND ([ConsumedAt] IS NULL OR ([ConsumedAt] >= [IssuedAt] AND [ConsumedAt] < [ExpiresAt]))");
            t.HasCheckConstraint("CK_RefreshTokens_Rotation", "([ConsumedAt] IS NULL AND [ReplacedByTokenId] IS NULL) OR ([ConsumedAt] IS NOT NULL AND [ReplacedByTokenId] IS NOT NULL AND [ReplacedByTokenId] <> [Id])");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.UserSessionId, x.Id });
        b.HasOne(x => x.UserSession).WithMany().HasForeignKey(x => x.UserSessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RefreshToken>().WithMany().HasForeignKey(x => new { x.UserSessionId, x.ReplacedByTokenId })
            .HasPrincipalKey(x => new { x.UserSessionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.TokenHash).HasMaxLength(256).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").IsRequired(); b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.ReplacedByTokenId).IsUnique().HasFilter("[ReplacedByTokenId] IS NOT NULL");
        b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => new { x.UserSessionId, x.ExpiresAt });
    }
}

public sealed class MfaCredentialConfiguration : IEntityTypeConfiguration<MfaCredential>
{
    public void Configure(EntityTypeBuilder<MfaCredential> b)
    {
        b.ToTable("MfaCredentials", "auth", t =>
        {
            t.HasCheckConstraint("CK_MfaCredentials_Secret", "([Method] = 2 AND [RecoveryCodeHash] IS NOT NULL AND [ProtectedSecretReference] IS NULL) OR ([Method] IN (1,3) AND [ProtectedSecretReference] IS NOT NULL AND [RecoveryCodeHash] IS NULL)");
            t.HasCheckConstraint("CK_MfaCredentials_Dates", "([ConfirmedAt] IS NULL OR [ConfirmedAt] >= [EnrolledAt]) AND ([RevokedAt] IS NULL OR [RevokedAt] >= [EnrolledAt]) AND ([RecoveryCodeConsumedAt] IS NULL OR ([Method] = 2 AND [ConfirmedAt] IS NOT NULL AND [RecoveryCodeConsumedAt] >= [ConfirmedAt]))");
            t.HasCheckConstraint("CK_MfaCredentials_Counter", "[LastAcceptedCounter] IS NULL OR ([Method] = 1 AND [LastAcceptedCounter] >= 0)");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.UserAccountId, x.Id });
        b.HasOne(x => x.UserAccount).WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.ProtectedSecretReference).HasMaxLength(500); b.Property(x => x.RecoveryCodeHash).HasMaxLength(256).IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
        b.HasIndex(x => x.RecoveryCodeHash).IsUnique().HasFilter("[RecoveryCodeHash] IS NOT NULL");
        b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => new { x.UserAccountId, x.RevokedAt });
    }
}

public sealed class MfaChallengeConfiguration : IEntityTypeConfiguration<MfaChallenge>
{
    public void Configure(EntityTypeBuilder<MfaChallenge> b)
    {
        b.ToTable("MfaChallenges", "auth", t =>
        {
            t.HasCheckConstraint("CK_MfaChallenges_Lifetime", "[ExpiresAt] > [IssuedAt] AND ([VerifiedAt] IS NULL OR ([VerifiedAt] >= [IssuedAt] AND [VerifiedAt] < [ExpiresAt])) AND ([ConsumedAt] IS NULL OR ([VerifiedAt] IS NOT NULL AND [ConsumedAt] >= [VerifiedAt] AND [ConsumedAt] < [ExpiresAt]))");
            t.HasCheckConstraint("CK_MfaChallenges_Verified", "([VerifiedAt] IS NULL AND [VerifiedCredentialId] IS NULL) OR ([VerifiedAt] IS NOT NULL AND [VerifiedCredentialId] IS NOT NULL)");
            t.HasCheckConstraint("CK_MfaChallenges_Counters", "[SecurityVersion] > 0 AND [FailedAttempts] >= 0");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.UserAccountId, x.Id });
        b.HasOne(x => x.UserAccount).WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RegisteredDevice>().WithMany().HasForeignKey(x => new { x.UserAccountId, x.RegisteredDeviceId })
            .HasPrincipalKey(x => new { x.UserAccountId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MfaCredential>().WithMany().HasForeignKey(x => new { x.UserAccountId, x.VerifiedCredentialId })
            .HasPrincipalKey(x => new { x.UserAccountId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.ChallengeHash).HasMaxLength(256).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").IsRequired(); b.HasIndex(x => x.ChallengeHash).IsUnique();
        b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => new { x.UserAccountId, x.IssuedAt });
    }
}
