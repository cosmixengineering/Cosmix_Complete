using Cosmix.Domain.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Authentication;

public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> b)
    {
        b.ToTable("UserAccounts", "auth", t =>
        {
            t.HasCheckConstraint("CK_UserAccounts_Status", "[Status] BETWEEN 0 AND 5");
            t.HasCheckConstraint("CK_UserAccounts_Counters", "[SecurityVersion] > 0 AND [FailedLoginCount] >= 0");
            t.HasCheckConstraint("CK_UserAccounts_Credentials", "([PasswordHash] IS NULL AND [PasswordHashFormat] IS NULL AND [PasswordChangedAt] IS NULL) OR ([PasswordHash] IS NOT NULL AND [PasswordHashFormat] IS NOT NULL AND [PasswordChangedAt] IS NOT NULL)");
            t.HasCheckConstraint("CK_UserAccounts_InitialAccess", "([InitialRoleAssignmentId] IS NULL AND [InitialAccessScopeId] IS NULL) OR ([InitialRoleAssignmentId] IS NOT NULL AND [InitialAccessScopeId] IS NOT NULL)");
            t.HasCheckConstraint("CK_UserAccounts_Active", "[Status] <> 3 OR (([PasswordHash] IS NOT NULL OR [PhoneVerifiedAt] IS NOT NULL) AND [ActivatedAt] IS NOT NULL AND [InitialRoleAssignmentId] IS NOT NULL)");
            t.HasCheckConstraint("CK_UserAccounts_PhoneVerified", "[PhoneVerifiedAt] IS NULL OR ([ActivatedAt] IS NOT NULL AND [PhoneVerifiedAt] >= [CreatedAt])");
            t.HasCheckConstraint("CK_UserAccounts_WebAccess", "[WebAccessEnabled] = 0 OR [PasswordHash] IS NOT NULL");
            t.HasCheckConstraint("CK_UserAccounts_Invitation", "[Status] <> 2 OR ([CurrentSetupInvitationId] IS NOT NULL AND [InitialAccessScopeId] IS NOT NULL)");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.EmployeeId, x.Id });
        b.HasOne(x => x.Employee).WithOne().HasForeignKey<UserAccount>(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ApprovalDecision).WithMany().HasForeignKey(x => new { x.EmployeeId, x.ApprovalDecisionId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserRoleAssignment>().WithMany().HasForeignKey(x => new { x.Id, x.InitialRoleAssignmentId })
            .HasPrincipalKey(x => new { x.UserAccountId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccessScope>().WithMany().HasForeignKey(x => new { x.InitialRoleAssignmentId, x.InitialAccessScopeId })
            .HasPrincipalKey(x => new { x.UserRoleAssignmentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AccountSetupInvitation>().WithMany().HasForeignKey(x => new { x.Id, x.CurrentSetupInvitationId })
            .HasPrincipalKey(x => new { x.UserAccountId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.LoginName).HasMaxLength(320).IsRequired(); b.Property(x => x.NormalizedLoginName).HasMaxLength(320).IsRequired();
        b.HasIndex(x => x.NormalizedLoginName).IsUnique(); b.HasIndex(x => x.EmployeeId).IsUnique();
        b.Property(x => x.PasswordHash).HasMaxLength(2000); b.Property(x => x.PasswordHashFormat).HasMaxLength(100);
        b.Property(x => x.MobileAccessEnabled).IsRequired(); b.Property(x => x.WebAccessEnabled).IsRequired();
        b.Property(x => x.StatusReason).HasMaxLength(1000); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.Status, x.Id });
    }
}

public sealed class AccountSetupInvitationConfiguration : IEntityTypeConfiguration<AccountSetupInvitation>
{
    public void Configure(EntityTypeBuilder<AccountSetupInvitation> b)
    {
        b.ToTable("AccountSetupInvitations", "auth", t =>
        {
            t.HasCheckConstraint("CK_AccountSetupInvitations_Dates", "[ExpiresAt] > [IssuedAt] AND ([ConsumedAt] IS NULL OR ([ConsumedAt] >= [IssuedAt] AND [ConsumedAt] < [ExpiresAt])) AND ([RevokedAt] IS NULL OR [RevokedAt] >= [IssuedAt])");
            t.HasCheckConstraint("CK_AccountSetupInvitations_Delivery", "[DeliveryStatus] BETWEEN 0 AND 2 AND [DeliveryAttempts] >= 0");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.UserAccountId, x.Id });
        b.HasOne(x => x.UserAccount).WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Cosmix.Domain.HR.Onboarding.EmployeeOnboardingDecision>().WithMany().HasForeignKey(x => x.ApprovalDecisionId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.TokenHash).HasMaxLength(256).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").IsRequired(); b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.Channel).HasMaxLength(60).IsRequired(); b.Property(x => x.ProtectedDestinationReference).HasMaxLength(500).IsRequired();
        b.Property(x => x.LastDeliveryFailureCode).HasMaxLength(100); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.UserAccountId, x.IssuedAt }); b.HasIndex(x => new { x.DeliveryStatus, x.NextDeliveryAttemptAt });
    }
}

public sealed class PasswordResetChallengeConfiguration : IEntityTypeConfiguration<PasswordResetChallenge>
{
    public void Configure(EntityTypeBuilder<PasswordResetChallenge> b)
    {
        b.ToTable("PasswordResetChallenges", "auth", t =>
        {
            t.HasCheckConstraint("CK_PasswordResetChallenges_Dates", "[ExpiresAt] > [IssuedAt] AND ([ConsumedAt] IS NULL OR ([ConsumedAt] >= [IssuedAt] AND [ConsumedAt] < [ExpiresAt])) AND ([RevokedAt] IS NULL OR [RevokedAt] >= [IssuedAt])");
            t.HasCheckConstraint("CK_PasswordResetChallenges_Counters", "[SecurityVersion] > 0 AND [FailedVerificationCount] >= 0");
        });
        b.HasKey(x => x.Id); b.HasOne(x => x.UserAccount).WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.TokenHash).HasMaxLength(256).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").IsRequired(); b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.Channel).HasMaxLength(60).IsRequired(); b.Property(x => x.ProtectedDestinationReference).HasMaxLength(500).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => new { x.UserAccountId, x.IssuedAt });
    }
}

public sealed class RegisteredDeviceConfiguration : IEntityTypeConfiguration<RegisteredDevice>
{
    public void Configure(EntityTypeBuilder<RegisteredDevice> b)
    {
        b.ToTable("RegisteredDevices", "auth", t => t.HasCheckConstraint("CK_RegisteredDevices_Dates",
            "([VerifiedAt] IS NULL OR [VerifiedAt] >= [RegisteredAt]) AND ([LastSeenAt] IS NULL OR [LastSeenAt] >= [RegisteredAt]) AND ([RevokedAt] IS NULL OR [RevokedAt] >= [RegisteredAt])"));
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.UserAccountId, x.Id });
        b.HasOne(x => x.UserAccount).WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.DeviceIdentifierHash).HasMaxLength(256).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").IsRequired(); b.Property(x => x.Platform).HasMaxLength(40).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(150).IsRequired(); b.Property(x => x.RevocationReason).HasMaxLength(1000);
        b.Property(x => x.IntegrityAssessmentCode).HasMaxLength(100); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.UserAccountId, x.DeviceIdentifierHash }).IsUnique().HasFilter("[RevokedAt] IS NULL");
    }
}
